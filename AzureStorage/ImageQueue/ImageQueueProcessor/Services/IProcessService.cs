namespace ImageQueueProcessor.Services;

/// <summary>
/// Service for processing images from Azure Storage Queue.
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Processes all messages in the queue until empty.
    /// </summary>
    /// <param name="dryRun">If true, only logs what would happen without making changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all messages were processed successfully, false otherwise.</returns>
    Task<bool> ProcessQueueAsync(bool dryRun, CancellationToken cancellationToken = default);
}
