using System.Text.Json.Serialization;

namespace ImageQueueProcessor;

/// <summary>
/// Represents the message payload for an image in the queue.
/// </summary>
public class ImageMessage
{
    /// <summary>
    /// The original filename of the image.
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The blob name where the source image is stored.
    /// </summary>
    [JsonPropertyName("blobName")]
    public string BlobName { get; set; } = string.Empty;

    /// <summary>
    /// The content type of the image (e.g., "image/png", "image/jpeg").
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// The size of the image in bytes.
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    /// <summary>
    /// Timestamp when the image was enqueued.
    /// </summary>
    [JsonPropertyName("enqueuedAt")]
    public DateTimeOffset EnqueuedAt { get; set; }
}
