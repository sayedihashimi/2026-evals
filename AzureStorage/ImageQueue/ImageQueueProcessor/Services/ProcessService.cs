using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ImageQueueProcessor.Services;

/// <summary>
/// Implementation of IProcessService that processes images from Azure Storage Queue.
/// Downloads source images from blob storage, resizes them, and uploads to the output container.
/// </summary>
public class ProcessService : IProcessService
{
    private readonly QueueProcessingSettings _settings;
    private readonly ILogger<ProcessService> _logger;

    public ProcessService(IOptions<QueueProcessingSettings> settings, ILogger<ProcessService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> ProcessQueueAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting process operation. DryRun: {DryRun}", dryRun);
        _logger.LogInformation("Queue name: {QueueName}, Source container: {SourceContainer}, Output container: {OutputContainer}", 
            _settings.QueueName, _settings.SourceImagesContainer, _settings.ResizedImagesContainer);

        var queueClient = new QueueClient(_settings.ConnectionString, _settings.QueueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Queue ensured to exist: {QueueName}", _settings.QueueName);

        var blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
        var sourceContainerClient = blobServiceClient.GetBlobContainerClient(_settings.SourceImagesContainer);
        BlobContainerClient? outputContainerClient = null;
        
        if (!dryRun)
        {
            outputContainerClient = blobServiceClient.GetBlobContainerClient(_settings.ResizedImagesContainer);
            await outputContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Output container ensured to exist: {Container}", _settings.ResizedImagesContainer);
        }

        // Track results for summary
        var processedFiles = new List<(string FileName, int OrigWidth, int OrigHeight, int NewWidth, int NewHeight, long OriginalSize)>();
        var skippedFiles = new List<(string FileName, string Reason)>();
        var failedFiles = new List<(string FileName, string Error)>();

        while (!cancellationToken.IsCancellationRequested)
        {
            QueueMessage[] messages;
            
            if (dryRun)
            {
                // In dry-run, peek instead of dequeue so we don't hide messages
                var peekedMessages = await queueClient.PeekMessagesAsync(maxMessages: 32, cancellationToken: cancellationToken);
                
                if (peekedMessages.Value.Length == 0)
                {
                    _logger.LogInformation("No more messages in queue.");
                    break;
                }

                foreach (var peeked in peekedMessages.Value)
                {
                    try
                    {
                        var imageMessage = JsonSerializer.Deserialize<ImageMessage>(peeked.MessageText);
                        if (imageMessage == null)
                        {
                            _logger.LogWarning("[DRY-RUN] Could not deserialize message");
                            failedFiles.Add(("Unknown", "Could not deserialize message"));
                            continue;
                        }

                        // Download source image from blob
                        var sourceBlobClient = sourceContainerClient.GetBlobClient(imageMessage.BlobName);
                        using var downloadStream = new MemoryStream();
                        await sourceBlobClient.DownloadToAsync(downloadStream, cancellationToken);
                        downloadStream.Position = 0;

                        using var image = await Image.LoadAsync(downloadStream, cancellationToken);
                        var newWidth = image.Width / 2;
                        var newHeight = image.Height / 2;

                        var outputFileName = GetOutputFileName(imageMessage.FileName);
                        
                        _logger.LogInformation("[DRY-RUN] Would process: {FileName} ({Width}x{Height} -> {NewWidth}x{NewHeight})",
                            imageMessage.FileName, image.Width, image.Height, newWidth, newHeight);
                        _logger.LogInformation("[DRY-RUN] Would upload as: {OutputFileName}", outputFileName);
                        _logger.LogInformation("[DRY-RUN] Would delete source blob: {BlobName}", imageMessage.BlobName);
                        _logger.LogInformation("[DRY-RUN] Would delete queue message for: {FileName}", imageMessage.FileName);
                        
                        processedFiles.Add((imageMessage.FileName, image.Width, image.Height, newWidth, newHeight, imageMessage.SizeBytes));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[DRY-RUN] Error processing message");
                        failedFiles.Add(("Unknown", ex.Message));
                    }
                }
                
                // In dry-run, we've peeked all messages once, so exit
                break;
            }
            else
            {
                // In real mode, receive and delete messages
                var response = await queueClient.ReceiveMessagesAsync(maxMessages: 32, cancellationToken: cancellationToken);
                messages = response.Value;

                if (messages.Length == 0)
                {
                    _logger.LogInformation("No more messages in queue.");
                    break;
                }

                foreach (var message in messages)
                {
                    string currentFileName = "Unknown";
                    try
                    {
                        var imageMessage = JsonSerializer.Deserialize<ImageMessage>(message.MessageText);
                        if (imageMessage == null)
                        {
                            _logger.LogWarning("Could not deserialize message {MessageId}", message.MessageId);
                            failedFiles.Add(("Unknown", "Could not deserialize message"));
                            continue;
                        }

                        currentFileName = imageMessage.FileName;
                        _logger.LogInformation("Processing: {FileName} (blob: {BlobName})", imageMessage.FileName, imageMessage.BlobName);

                        // Download source image from blob
                        var sourceBlobClient = sourceContainerClient.GetBlobClient(imageMessage.BlobName);
                        
                        // Check if blob exists (handles orphaned queue messages)
                        if (!await sourceBlobClient.ExistsAsync(cancellationToken))
                        {
                            _logger.LogWarning("Source blob not found: {BlobName}. Deleting orphaned queue message.", imageMessage.BlobName);
                            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                            skippedFiles.Add((imageMessage.FileName, "Source blob not found (orphaned message)"));
                            continue;
                        }
                        
                        using var downloadStream = new MemoryStream();
                        await sourceBlobClient.DownloadToAsync(downloadStream, cancellationToken);
                        downloadStream.Position = 0;
                        
                        // Resize image
                        using var image = await Image.LoadAsync(downloadStream, cancellationToken);
                        
                        var originalWidth = image.Width;
                        var originalHeight = image.Height;
                        var newWidth = originalWidth / 2;
                        var newHeight = originalHeight / 2;
                        
                        if (newWidth < 1) newWidth = 1;
                        if (newHeight < 1) newHeight = 1;

                        image.Mutate(x => x.Resize(newWidth, newHeight));
                        
                        _logger.LogInformation("Resized: {FileName} ({OldW}x{OldH} -> {NewW}x{NewH})",
                            imageMessage.FileName, originalWidth, originalHeight, newWidth, newHeight);

                        // Save to memory stream with appropriate format
                        using var outputStream = new MemoryStream();
                        var encoder = GetEncoder(imageMessage.ContentType);
                        await image.SaveAsync(outputStream, encoder, cancellationToken);
                        outputStream.Position = 0;

                        // Upload to output container
                        var outputFileName = GetOutputFileName(imageMessage.FileName);
                        var outputBlobClient = outputContainerClient!.GetBlobClient(outputFileName);
                        
                        await outputBlobClient.UploadAsync(outputStream, new BlobHttpHeaders
                        {
                            ContentType = imageMessage.ContentType
                        }, cancellationToken: cancellationToken);
                        
                        _logger.LogInformation("Uploaded: {OutputFileName}", outputFileName);

                        // Delete source blob after successful processing
                        await sourceBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                        _logger.LogInformation("Deleted source blob: {BlobName}", imageMessage.BlobName);

                        // Delete message only after successful upload
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                        _logger.LogInformation("Deleted queue message for: {FileName}", imageMessage.FileName);
                        
                        processedFiles.Add((imageMessage.FileName, originalWidth, originalHeight, newWidth, newHeight, imageMessage.SizeBytes));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
                        failedFiles.Add((currentFileName, ex.Message));
                        // Do not delete the message on failure - it will become visible again
                    }
                }
            }
        }

        PrintSummary(processedFiles, skippedFiles, failedFiles, dryRun);

        return failedFiles.Count == 0;
    }

    private static void PrintSummary(
        List<(string FileName, int OrigWidth, int OrigHeight, int NewWidth, int NewHeight, long OriginalSize)> processed,
        List<(string FileName, string Reason)> skipped,
        List<(string FileName, string Error)> failed,
        bool dryRun)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine(dryRun ? "  PROCESS SUMMARY (DRY RUN)" : "  PROCESS SUMMARY");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();

        // Processed files
        Console.WriteLine($"✓ PROCESSED: {processed.Count} image(s)");
        if (processed.Count > 0)
        {
            var totalSize = processed.Sum(f => f.OriginalSize);
            foreach (var (fileName, origW, origH, newW, newH, size) in processed)
            {
                Console.WriteLine($"    • {fileName}");
                Console.WriteLine($"      {origW}x{origH} → {newW}x{newH} ({FormatFileSize(size)})");
            }
            Console.WriteLine($"    Total original size: {FormatFileSize(totalSize)}");
        }
        Console.WriteLine();

        // Skipped files (orphaned messages, etc.)
        Console.WriteLine($"⊘ SKIPPED: {skipped.Count} image(s)");
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
        Console.WriteLine($"✗ FAILED: {failed.Count} image(s)");
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

    private static string GetOutputFileName(string originalFileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        var extension = Path.GetExtension(originalFileName);
        return $"{nameWithoutExtension}-50{extension}";
    }

    private static IImageEncoder GetEncoder(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => new PngEncoder(),
            "image/jpeg" => new JpegEncoder { Quality = 90 },
            "image/gif" => new GifEncoder(),
            "image/bmp" => new BmpEncoder(),
            "image/webp" => new WebpEncoder(),
            _ => new PngEncoder()
        };
    }
}
