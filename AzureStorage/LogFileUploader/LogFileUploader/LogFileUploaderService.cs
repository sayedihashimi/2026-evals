using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace LogFileUploader;

/// <summary>
/// Service that uploads log files to Azure Blob Storage.
/// </summary>
public class LogFileUploaderService : ILogFileUploader
{
    private readonly BlobUploadSettings _options;
    private readonly ILogger<LogFileUploaderService> _logger;
    private readonly BlobContainerClient _containerClient;
    private readonly SemaphoreSlim _containerEnsureLock = new(1, 1);
    private bool _containerEnsured = false;
    private readonly ResiliencePipeline _retryPipeline;

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

        if (!_options.IsValidBlobPrefix())
        {
            throw new InvalidOperationException(
                $"Invalid blob prefix: '{_options.BlobPrefix}'. " +
                "Prefix cannot start with '/' or contain consecutive slashes.");
        }

        var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);

        // Configure retry pipeline for transient failures
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                Delay = TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<RequestFailedException>(ex =>
                    ex.Status is 408 or 429 or 500 or 502 or 503 or 504),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry attempt {Attempt} after {Delay}ms due to: {Message}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
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
        int filesSkipped = 0;
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
                _logger.LogDebug("Processing file: {FileName}", fileName);

                // Validate file before upload
                var validationResult = ValidateFile(filePath, fileName);
                if (!validationResult.IsValid)
                {
                    filesSkipped++;
                    _logger.LogInformation("Skipping {FileName}: {Reason}", fileName, validationResult.Reason);
                    continue;
                }

                if (dryRun)
                {
                    _logger.LogInformation("[DRY RUN] Would upload {FileName} ({Size:N0} bytes) to blob {BlobName}", 
                        fileName, validationResult.FileSize, blobName);
                    filesUploaded++;

                    if (deleteAfterUpload)
                    {
                        _logger.LogInformation("[DRY RUN] Would delete {FileName}", fileName);
                        filesDeleted++;
                    }
                }
                else
                {
                    // Upload the file with retry logic
                    await _retryPipeline.ExecuteAsync(
                        async ct => await UploadFileAsync(filePath, blobName, ct),
                        cancellationToken);
                    
                    filesUploaded++;
                    _logger.LogInformation("Successfully uploaded {FileName} ({Size:N0} bytes) to blob {BlobName}", 
                        fileName, validationResult.FileSize, blobName);

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
            catch (IOException ex) when (IsFileLocked(ex))
            {
                filesSkipped++;
                _logger.LogWarning("Skipping {FileName}: File is in use by another process", fileName);
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
            "Upload complete. Processed: {Processed}, Uploaded: {Uploaded}, Skipped: {Skipped}, Deleted: {Deleted}, Failed: {Failed}",
            filesProcessed, filesUploaded, filesSkipped, filesDeleted, filesFailed);

        return new UploadResult(success, filesProcessed, filesUploaded, filesSkipped, filesDeleted, filesFailed, errors);
    }

    /// <summary>
    /// Validates a file before upload based on size and age criteria.
    /// </summary>
    private FileValidationResult ValidateFile(string filePath, string fileName)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            // Check if file exists
            if (!fileInfo.Exists)
            {
                return FileValidationResult.Invalid("File no longer exists");
            }

            // Check minimum size (skip empty files)
            if (fileInfo.Length < _options.MinFileSizeBytes)
            {
                return FileValidationResult.Invalid(
                    $"File is too small ({fileInfo.Length} bytes, minimum is {_options.MinFileSizeBytes} bytes)");
            }

            // Check maximum size
            if (fileInfo.Length > _options.MaxFileSizeBytes)
            {
                return FileValidationResult.Invalid(
                    $"File is too large ({fileInfo.Length:N0} bytes, maximum is {_options.MaxFileSizeBytes:N0} bytes)");
            }

            return FileValidationResult.Valid(fileInfo.Length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error validating file {FileName}", fileName);
            return FileValidationResult.Invalid($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines if an IOException is due to a file being locked.
    /// </summary>
    private static bool IsFileLocked(IOException ex)
    {
        // Common HRESULT values for file locking errors
        const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
        const int ERROR_LOCK_VIOLATION = unchecked((int)0x80070021);

        return ex.HResult == ERROR_SHARING_VIOLATION || ex.HResult == ERROR_LOCK_VIOLATION;
    }

    private async Task UploadFileAsync(string filePath, string blobName, CancellationToken cancellationToken)
    {
        // Ensure container exists (thread-safe)
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

        await _containerEnsureLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_containerEnsured)
            {
                return;
            }

            _logger.LogInformation("Ensuring container '{ContainerName}' exists...", _options.ContainerName);
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _containerEnsured = true;
            _logger.LogInformation("Container '{ContainerName}' is ready", _options.ContainerName);
        }
        finally
        {
            _containerEnsureLock.Release();
        }
    }

    /// <summary>
    /// Result of file validation.
    /// </summary>
    private readonly record struct FileValidationResult(bool IsValid, string? Reason, long FileSize)
    {
        public static FileValidationResult Valid(long fileSize) => new(true, null, fileSize);
        public static FileValidationResult Invalid(string reason) => new(false, reason, 0);
    }
}
