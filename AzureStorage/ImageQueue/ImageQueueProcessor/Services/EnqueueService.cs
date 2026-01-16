using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ImageQueueProcessor.Services;

/// <summary>
/// Implementation of IEnqueueService that sends images to Azure Storage Queue.
/// Images are uploaded to a staging blob container, and a reference is sent in the queue message.
/// </summary>
public class EnqueueService : IEnqueueService
{
    private readonly QueueProcessingSettings _settings;
    private readonly ILogger<EnqueueService> _logger;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"
    };

    public EnqueueService(IOptions<QueueProcessingSettings> settings, ILogger<EnqueueService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> EnqueueImagesAsync(string folder, string pattern, bool dryRun, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting enqueue operation. Folder: {Folder}, Pattern: {Pattern}, DryRun: {DryRun}", 
            folder, pattern, dryRun);
        _logger.LogInformation("Queue name: {QueueName}, Source container: {Container}, Max size: {MaxSize:N0} bytes", 
            _settings.QueueName, _settings.SourceImagesContainer, _settings.MaxFileSizeBytes);

        if (!Directory.Exists(folder))
        {
            _logger.LogError("Folder does not exist: {Folder}", folder);
            return false;
        }

        // Get matching files
        var files = GetMatchingFiles(folder, pattern).ToList();
        _logger.LogInformation("Found {Count} matching file(s)", files.Count);

        if (files.Count == 0)
        {
            _logger.LogInformation("No files to process.");
            PrintSummary([], [], [], dryRun);
            return true;
        }

        QueueClient? queueClient = null;
        BlobContainerClient? containerClient = null;
        
        if (!dryRun)
        {
            queueClient = new QueueClient(_settings.ConnectionString, _settings.QueueName);
            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Queue ensured to exist: {QueueName}", _settings.QueueName);

            var blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
            containerClient = blobServiceClient.GetBlobContainerClient(_settings.SourceImagesContainer);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Container ensured to exist: {Container}", _settings.SourceImagesContainer);
        }

        // Track results for summary
        var uploadedFiles = new List<(string FileName, long SizeBytes)>();
        var skippedFiles = new List<(string FileName, string Reason)>();
        var failedFiles = new List<(string FileName, string Error)>();

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileName = Path.GetFileName(filePath);
            
            try
            {
                var fileInfo = new FileInfo(filePath);
                
                // Check file size limit
                if (fileInfo.Length > _settings.MaxFileSizeBytes)
                {
                    var reason = $"File too large ({FormatFileSize(fileInfo.Length)}, max {FormatFileSize(_settings.MaxFileSizeBytes)})";
                    _logger.LogError("File '{FileName}' is too large ({Size:N0} bytes, max {MaxSize:N0} bytes). Skipping.",
                        fileName, fileInfo.Length, _settings.MaxFileSizeBytes);
                    skippedFiles.Add((fileName, reason));
                    continue;
                }

                var contentType = GetContentType(filePath);
                var blobName = $"{Guid.NewGuid():N}-{fileName}";

                if (dryRun)
                {
                    _logger.LogInformation("[DRY-RUN] Would upload to blob: {BlobName} ({Size:N0} bytes)", blobName, fileInfo.Length);
                    _logger.LogInformation("[DRY-RUN] Would enqueue: {FileName}", fileName);
                    _logger.LogInformation("[DRY-RUN] Would delete local file: {FilePath}", filePath);
                    uploadedFiles.Add((fileName, fileInfo.Length));
                }
                else
                {
                    // Upload to blob storage first
                    var blobClient = containerClient!.GetBlobClient(blobName);
                    
                    // Use a scoped block to ensure the file stream is disposed before we try to delete
                    {
                        await using var fileStream = File.OpenRead(filePath);
                        await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
                        {
                            ContentType = contentType
                        }, cancellationToken: cancellationToken);
                    }
                    
                    _logger.LogInformation("Uploaded to blob: {BlobName} ({Size:N0} bytes)", blobName, fileInfo.Length);

                    // Create queue message with blob reference
                    var message = new ImageMessage
                    {
                        FileName = fileName,
                        BlobName = blobName,
                        ContentType = contentType,
                        SizeBytes = fileInfo.Length,
                        EnqueuedAt = DateTimeOffset.UtcNow
                    };

                    var messageJson = JsonSerializer.Serialize(message);
                    await queueClient!.SendMessageAsync(messageJson, cancellationToken);
                    _logger.LogInformation("Enqueued: {FileName}", fileName);

                    // Delete local file after successful enqueue (stream is now closed)
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted local file: {FilePath}", filePath);
                    
                    uploadedFiles.Add((fileName, fileInfo.Length));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process file: {FileName}", fileName);
                failedFiles.Add((fileName, ex.Message));
            }
        }

        PrintSummary(uploadedFiles, skippedFiles, failedFiles, dryRun);

        return failedFiles.Count == 0 && skippedFiles.Count == 0;
    }

    private static void PrintSummary(
        List<(string FileName, long SizeBytes)> uploaded,
        List<(string FileName, string Reason)> skipped,
        List<(string FileName, string Error)> failed,
        bool dryRun)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine(dryRun ? "  ENQUEUE SUMMARY (DRY RUN)" : "  ENQUEUE SUMMARY");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();

        // Uploaded files
        Console.WriteLine($"✓ UPLOADED: {uploaded.Count} file(s)");
        if (uploaded.Count > 0)
        {
            var totalSize = uploaded.Sum(f => f.SizeBytes);
            foreach (var (fileName, sizeBytes) in uploaded)
            {
                Console.WriteLine($"    • {fileName} ({FormatFileSize(sizeBytes)})");
            }
            Console.WriteLine($"    Total size: {FormatFileSize(totalSize)}");
        }
        Console.WriteLine();

        // Skipped files
        Console.WriteLine($"⊘ SKIPPED: {skipped.Count} file(s)");
        if (skipped.Count > 0)
        {
            foreach (var (fileName, reason) in skipped)
            {
                Console.WriteLine($"    • {fileName}");
                Console.WriteLine($"      Reason: {reason}");
            }
        }
        Console.WriteLine();

        // Failed files
        Console.WriteLine($"✗ FAILED: {failed.Count} file(s)");
        if (failed.Count > 0)
        {
            foreach (var (fileName, error) in failed)
            {
                Console.WriteLine($"    • {fileName}");
                Console.WriteLine($"      Error: {error}");
            }
        }
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private IEnumerable<string> GetMatchingFiles(string folder, string pattern)
    {
        // Split pattern by semicolon to support multiple patterns like "*.png;*.jpg;*.jpeg"
        var patterns = pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var p in patterns)
        {
            try
            {
                var matchedFiles = Directory.EnumerateFiles(folder, p, SearchOption.TopDirectoryOnly);
                foreach (var file in matchedFiles)
                {
                    var ext = Path.GetExtension(file);
                    if (SupportedExtensions.Contains(ext))
                    {
                        allFiles.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error matching pattern '{Pattern}' in folder '{Folder}'", p, folder);
            }
        }

        return allFiles.OrderBy(f => f);
    }

    private static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
