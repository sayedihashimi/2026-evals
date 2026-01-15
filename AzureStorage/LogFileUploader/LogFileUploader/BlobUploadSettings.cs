namespace LogFileUploader;

/// <summary>
/// Configuration options for blob upload operations.
/// </summary>
public class BlobUploadSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "BlobUpload";

    /// <summary>
    /// Azure Storage connection string.
    /// Should be stored in appsettings.json.user or environment variables.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Name of the blob container to upload files to.
    /// </summary>
    public string ContainerName { get; set; } = "logs";

    /// <summary>
    /// Prefix to prepend to blob names (e.g., "ingest/").
    /// </summary>
    public string BlobPrefix { get; set; } = "ingest/";

    /// <summary>
    /// Content type to set for uploaded blobs.
    /// </summary>
    public string ContentType { get; set; } = "text/plain";

    /// <summary>
    /// Whether to overwrite existing blobs with the same name.
    /// </summary>
    public bool Overwrite { get; set; } = false;
}
