using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogFileUploader;

/// <summary>
/// Service that uploads log files to Azure Blob Storage.
/// </summary>
public class LogFileUploaderService : ILogFileUploader
{
    private readonly BlobUploadSettings _options;
    private readonly ILogger<LogFileUploaderService> _logger;
    private readonly BlobContainerClient _containerClient;
    private bool _containerEnsured = false;

    public LogFileUploaderService(
        IOptions<BlobUploadSettings> options,
        ILogger<LogFileUploaderService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Azure Storage connection string is not configured. " +
                "Set it in appsettings.json.user or via environment variable BlobUpload__ConnectionString.");
        }

        var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
    }

    public async Task<UploadResult> UploadFilesAsync(
        string directoryPath,
        string pattern,
        bool deleteAfterUpload,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        int filesProcessed = 0;
        int filesUploaded = 0;
        int filesDeleted = 0;
        int filesFailed = 0;

        _logger.LogInformation(
            "Starting upload from {Directory} with pattern {Pattern} (DryRun: {DryRun}, Delete: {Delete})",
            directoryPath, pattern, dryRun, deleteAfterUpload);

        // Get all matching files
        var files = Directory.EnumerateFiles(directoryPath, pattern, SearchOption.TopDirectoryOnly).ToList();
        
        _logger.LogInformation("Found {Count} files matching pattern {Pattern}", files.Count, pattern);

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filesProcessed++;

            var fileName = Path.GetFileName(filePath);
            var blobName = $"{_options.BlobPrefix}{fileName}";

            try
            {
                _logger.LogInformation("Processing file: {FileName}", fileName);

                if (dryRun)
                {
                    _logger.LogInformation("[DRY RUN] Would upload {FileName} to blob {BlobName}", fileName, blobName);
                    filesUploaded++;

                    if (deleteAfterUpload)
                    {
                        _logger.LogInformation("[DRY RUN] Would delete {FileName}", fileName);
                        filesDeleted++;
                    }
                }
                else
                {
                    // Upload the file
                    await UploadFileAsync(filePath, blobName, cancellationToken);
                    filesUploaded++;
                    _logger.LogInformation("Successfully uploaded {FileName} to blob {BlobName}", fileName, blobName);

                    // Delete the file if requested
                    if (deleteAfterUpload)
                    {
                        File.Delete(filePath);
                        filesDeleted++;
                        _logger.LogInformation("Deleted local file: {FileName}", fileName);
                    }
                }
            }
            catch (RequestFailedException ex)
            {
                filesFailed++;
                var errorMessage = $"Failed to upload {fileName}: {ex.Message} (Status: {ex.Status})";
                errors.Add(errorMessage);
                _logger.LogError(ex, "Azure Storage error uploading {FileName}", fileName);
            }
            catch (IOException ex)
            {
                filesFailed++;
                var errorMessage = $"IO error processing {fileName}: {ex.Message}";
                errors.Add(errorMessage);
                _logger.LogError(ex, "IO error processing {FileName}", fileName);
            }
            catch (Exception ex)
            {
                filesFailed++;
                var errorMessage = $"Unexpected error processing {fileName}: {ex.Message}";
                errors.Add(errorMessage);
                _logger.LogError(ex, "Unexpected error processing {FileName}", fileName);
            }
        }

        var success = filesFailed == 0;
        
        _logger.LogInformation(
            "Upload complete. Processed: {Processed}, Uploaded: {Uploaded}, Deleted: {Deleted}, Failed: {Failed}",
            filesProcessed, filesUploaded, filesDeleted, filesFailed);

        return new UploadResult(success, filesProcessed, filesUploaded, filesDeleted, filesFailed, errors);
    }

    private async Task UploadFileAsync(string filePath, string blobName, CancellationToken cancellationToken)
    {
        // Ensure container exists (only once per session)
        await EnsureContainerExistsAsync(cancellationToken);

        var blobClient = _containerClient.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = _options.ContentType
            }
        };

        // Check if blob exists and handle overwrite setting
        if (!_options.Overwrite)
        {
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (exists.Value)
            {
                throw new InvalidOperationException(
                    $"Blob {blobName} already exists and Overwrite is set to false.");
            }
        }

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        await blobClient.UploadAsync(fileStream, uploadOptions, cancellationToken);
    }

    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
        {
            return;
        }

        _logger.LogInformation("Ensuring container '{ContainerName}' exists...", _options.ContainerName);
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _containerEnsured = true;
        _logger.LogInformation("Container '{ContainerName}' is ready", _options.ContainerName);
    }
}
