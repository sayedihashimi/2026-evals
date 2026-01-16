using System.Text.RegularExpressions;

namespace LogFileUploader;

/// <summary>
/// Configuration options for blob upload operations.
/// </summary>
public partial class BlobUploadSettings
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

    /// <summary>
    /// Maximum file size in bytes. Files larger than this will be skipped.
    /// Default is 100 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Minimum file size in bytes. Files smaller than this (including empty files) will be skipped.
    /// Default is 1 byte (skip empty files).
    /// </summary>
    public long MinFileSizeBytes { get; set; } = 1;

    /// <summary>
    /// Number of retry attempts for transient failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Validates the BlobPrefix to ensure it doesn't contain invalid characters.
    /// </summary>
    public bool IsValidBlobPrefix()
    {
        if (string.IsNullOrEmpty(BlobPrefix))
        {
            return true; // Empty prefix is valid
        }

        // Blob names can contain alphanumeric, hyphen, underscore, and forward slash
        // Cannot start with a slash or contain consecutive slashes
        if (BlobPrefix.StartsWith('/') || BlobPrefix.Contains("//"))
        {
            return false;
        }

        return ValidBlobPrefixRegex().IsMatch(BlobPrefix);
    }

    [GeneratedRegex(@"^[\w\-./]+$")]
    private static partial Regex ValidBlobPrefixRegex();
}
