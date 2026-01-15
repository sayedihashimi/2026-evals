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
    /// The image bytes encoded as Base64.
    /// </summary>
    [JsonPropertyName("imageData")]
    public string ImageData { get; set; } = string.Empty;

    /// <summary>
    /// The content type of the image (e.g., "image/png", "image/jpeg").
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the image was enqueued.
    /// </summary>
    [JsonPropertyName("enqueuedAt")]
    public DateTimeOffset EnqueuedAt { get; set; }
}
