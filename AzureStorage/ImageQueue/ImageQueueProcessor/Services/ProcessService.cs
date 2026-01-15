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
        _logger.LogInformation("Queue name: {QueueName}, Container: {Container}", 
            _settings.QueueName, _settings.ResizedImagesContainer);

        var queueClient = new QueueClient(_settings.ConnectionString, _settings.QueueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Queue ensured to exist: {QueueName}", _settings.QueueName);

        BlobContainerClient? containerClient = null;
        if (!dryRun)
        {
            var blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
            containerClient = blobServiceClient.GetBlobContainerClient(_settings.ResizedImagesContainer);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Container ensured to exist: {Container}", _settings.ResizedImagesContainer);
        }

        int messagesProcessed = 0;
        int blobsUploaded = 0;
        int messagesFailed = 0;

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
                            messagesFailed++;
                            continue;
                        }

                        var imageBytes = Convert.FromBase64String(imageMessage.ImageData);
                        using var image = Image.Load(imageBytes);
                        var newWidth = image.Width / 2;
                        var newHeight = image.Height / 2;

                        var outputFileName = GetOutputFileName(imageMessage.FileName);
                        
                        _logger.LogInformation("[DRY-RUN] Would process: {FileName} ({Width}x{Height} -> {NewWidth}x{NewHeight})",
                            imageMessage.FileName, image.Width, image.Height, newWidth, newHeight);
                        _logger.LogInformation("[DRY-RUN] Would upload as: {OutputFileName}", outputFileName);
                        _logger.LogInformation("[DRY-RUN] Would delete queue message for: {FileName}", imageMessage.FileName);
                        
                        messagesProcessed++;
                        blobsUploaded++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[DRY-RUN] Error processing message");
                        messagesFailed++;
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
                    try
                    {
                        var imageMessage = JsonSerializer.Deserialize<ImageMessage>(message.MessageText);
                        if (imageMessage == null)
                        {
                            _logger.LogWarning("Could not deserialize message {MessageId}", message.MessageId);
                            messagesFailed++;
                            continue;
                        }

                        _logger.LogInformation("Processing: {FileName}", imageMessage.FileName);

                        var imageBytes = Convert.FromBase64String(imageMessage.ImageData);
                        
                        // Resize image
                        using var inputStream = new MemoryStream(imageBytes);
                        using var image = await Image.LoadAsync(inputStream, cancellationToken);
                        
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

                        // Upload to blob
                        var outputFileName = GetOutputFileName(imageMessage.FileName);
                        var blobClient = containerClient!.GetBlobClient(outputFileName);
                        
                        await blobClient.UploadAsync(outputStream, new BlobHttpHeaders
                        {
                            ContentType = imageMessage.ContentType
                        }, cancellationToken: cancellationToken);
                        
                        _logger.LogInformation("Uploaded: {OutputFileName}", outputFileName);
                        blobsUploaded++;

                        // Delete message only after successful upload
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
                        _logger.LogInformation("Deleted queue message for: {FileName}", imageMessage.FileName);
                        messagesProcessed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
                        messagesFailed++;
                        // Do not delete the message on failure - it will become visible again
                    }
                }
            }
        }

        _logger.LogInformation("Process summary: Processed={Processed}, Uploaded={Uploaded}, Failed={Failed}",
            messagesProcessed, blobsUploaded, messagesFailed);

        return messagesFailed == 0;
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
