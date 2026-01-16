namespace ImageQueueProcessor.Services;

/// <summary>
/// Service for enqueuing images to Azure Storage Queue.
/// </summary>
public interface IEnqueueService
{
    /// <summary>
    /// Enqueues all matching image files from the specified folder.
    /// </summary>
    /// <param name="folder">The folder containing images.</param>
    /// <param name="pattern">The file pattern to match.</param>
    /// <param name="dryRun">If true, only logs what would happen without making changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all files were processed successfully, false otherwise.</returns>
    Task<bool> EnqueueImagesAsync(string folder, string pattern, bool dryRun, CancellationToken cancellationToken = default);
}
