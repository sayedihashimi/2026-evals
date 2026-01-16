namespace LogFileUploader;

/// <summary>
/// Service interface for uploading log files to Azure Blob Storage.
/// </summary>
public interface ILogFileUploader
{
    /// <summary>
    /// Uploads all matching files from the specified directory to Azure Blob Storage.
    /// </summary>
    /// <param name="directoryPath">Path to the directory containing log files.</param>
    /// <param name="pattern">File pattern to match (e.g., "*.log").</param>
    /// <param name="deleteAfterUpload">Whether to delete files after successful upload.</param>
    /// <param name="dryRun">If true, simulate the operation without actually uploading or deleting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status and counts.</returns>
    Task<UploadResult> UploadFilesAsync(
        string directoryPath,
        string pattern,
        bool deleteAfterUpload,
        bool dryRun,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of the upload operation.
/// </summary>
/// <param name="Success">Whether the operation completed without failures.</param>
/// <param name="FilesProcessed">Total number of files processed.</param>
/// <param name="FilesUploaded">Number of files successfully uploaded.</param>
/// <param name="FilesSkipped">Number of files skipped (empty, too large, or in use).</param>
/// <param name="FilesDeleted">Number of files deleted after upload.</param>
/// <param name="FilesFailed">Number of files that failed to upload.</param>
/// <param name="Errors">List of error messages for failed operations.</param>
public record UploadResult(
    bool Success,
    int FilesProcessed,
    int FilesUploaded,
    int FilesSkipped,
    int FilesDeleted,
    int FilesFailed,
    IReadOnlyList<string> Errors);
