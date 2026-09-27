using System.Diagnostics;

namespace FitLife.Api.Observability;

/// <summary>Records start/finish/failure, owner, duration, and users processed for one worker batch.</summary>
internal static class WorkerRun
{
    public static async Task ExecuteAsync(
        string worker,
        ILogger logger,
        FitLifeMetrics? metrics,
        Func<Task<(int Succeeded, int Failed)>> batch,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Worker run started: {Worker} on {Owner}", worker, FitLifeMetrics.ProcessOwner);
        try
        {
            var (succeeded, failed) = await batch();
            metrics?.WorkerUsersProcessed(worker, succeeded, failed);
            metrics?.WorkerRunCompleted(worker, success: true, stopwatch.Elapsed);
            logger.LogInformation(
                "Worker run finished: {Worker} on {Owner} in {DurationSeconds:F2}s; {Succeeded} users succeeded, {Failed} failed",
                worker, FitLifeMetrics.ProcessOwner, stopwatch.Elapsed.TotalSeconds, succeeded, failed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            metrics?.WorkerRunCompleted(worker, success: false, stopwatch.Elapsed);
            logger.LogError(ex, "Worker run failed: {Worker} on {Owner} after {DurationSeconds:F2}s",
                worker, FitLifeMetrics.ProcessOwner, stopwatch.Elapsed.TotalSeconds);
            throw;
        }
    }
}
