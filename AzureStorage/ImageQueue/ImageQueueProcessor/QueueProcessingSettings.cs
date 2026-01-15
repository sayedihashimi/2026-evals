namespace ImageQueueProcessor;

/// <summary>
/// Configuration settings for queue processing operations.
/// </summary>
public class QueueProcessingSettings
{
    /// <summary>
    /// The Azure Storage connection string used for both Queue and Blob storage.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The name of the Azure Storage Queue. Default: "images"
    /// </summary>
    public string QueueName { get; set; } = "images";

    /// <summary>
    /// The name of the blob container for staging source images. Default: "sourceimages"
    /// </summary>
    public string SourceImagesContainer { get; set; } = "sourceimages";

    /// <summary>
    /// The name of the blob container for resized images. Default: "resizedimages"
    /// </summary>
    public string ResizedImagesContainer { get; set; } = "resizedimages";

    /// <summary>
    /// Maximum file size in bytes for images. Default: 5MB
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
}
