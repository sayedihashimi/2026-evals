using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ImageQueueProcessor.Services;

/// <summary>
/// Implementation of IEnqueueService that sends images to Azure Storage Queue.
/// </summary>
public class EnqueueService : IEnqueueService
{
    private readonly QueueProcessingSettings _settings;
    private readonly ILogger<EnqueueService> _logger;
    
    // Azure Storage Queue message size limit is 64KB for Base64 encoded content
    // The raw limit is 64KB, but Base64 encoding increases size by ~33%
    // So we limit the raw file size to ~48KB to be safe
    private const int MaxMessageSizeBytes = 48 * 1024;

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
        _logger.LogInformation("Queue name: {QueueName}", _settings.QueueName);

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
            return true;
        }

        QueueClient? queueClient = null;
        if (!dryRun)
        {
            queueClient = new QueueClient(_settings.ConnectionString, _settings.QueueName);
            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Queue ensured to exist: {QueueName}", _settings.QueueName);
        }

        int filesFound = files.Count;
        int filesEnqueued = 0;
        int filesDeleted = 0;
        int filesFailed = 0;

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileName = Path.GetFileName(filePath);
            
            try
            {
                var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                
                // Check file size limit
                if (fileBytes.Length > MaxMessageSizeBytes)
                {
                    _logger.LogError("File '{FileName}' is too large for queue message ({Size} bytes, max {MaxSize} bytes). Skipping.",
                        fileName, fileBytes.Length, MaxMessageSizeBytes);
                    filesFailed++;
                    continue;
                }

                var contentType = GetContentType(filePath);
                var message = new ImageMessage
                {
                    FileName = fileName,
                    ImageData = Convert.ToBase64String(fileBytes),
                    ContentType = contentType,
                    EnqueuedAt = DateTimeOffset.UtcNow
                };

                var messageJson = JsonSerializer.Serialize(message);

                if (dryRun)
                {
                    _logger.LogInformation("[DRY-RUN] Would enqueue: {FileName} ({Size} bytes)", fileName, fileBytes.Length);
                    _logger.LogInformation("[DRY-RUN] Would delete local file: {FilePath}", filePath);
                    filesEnqueued++;
                    filesDeleted++;
                }
                else
                {
                    await queueClient!.SendMessageAsync(messageJson, cancellationToken);
                    _logger.LogInformation("Enqueued: {FileName} ({Size} bytes)", fileName, fileBytes.Length);
                    filesEnqueued++;

                    // Delete local file after successful enqueue
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted local file: {FilePath}", filePath);
                    filesDeleted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process file: {FileName}", fileName);
                filesFailed++;
            }
        }

        _logger.LogInformation("Enqueue summary: Found={Found}, Enqueued={Enqueued}, Deleted={Deleted}, Failed={Failed}",
            filesFound, filesEnqueued, filesDeleted, filesFailed);

        return filesFailed == 0;
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
