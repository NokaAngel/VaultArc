using System.Collections.Concurrent;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Jobs;

public sealed class InMemoryJobQueueService : IJobQueueService
{
    private readonly ConcurrentDictionary<Guid, JobRuntime> _jobs = new();

    public IReadOnlyCollection<VaultArcJob> Snapshot => _jobs.Values.Select(static j => j.Job).OrderByDescending(static j => j.QueuedAtUtc).ToList();

    public event EventHandler? QueueChanged;

    public async Task<Guid> EnqueueAsync(
        string title,
        Func<IProgress<JobProgressUpdate>, CancellationToken, Task<OperationResult>> work,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runtime = new JobRuntime(
            new VaultArcJob(jobId, title, DateTimeOffset.UtcNow, JobState.Pending, 0, "Queued", TimeSpan.Zero, null),
            linkedCts);

        _jobs[jobId] = runtime;
        RaiseChanged();

        _ = Task.Run(async () =>
        {
            try
            {
                var startedAt = DateTimeOffset.UtcNow;
                Update(jobId, job => job with { State = JobState.Running, StatusMessage = "Running..." });

                var progress = new Progress<JobProgressUpdate>(update =>
                {
                    var eta = update.Eta ?? EstimateEta(update.Percent, update.Elapsed);
                    Update(jobId, job => job with
                    {
                        ProgressPercent = update.Percent,
                        StatusMessage = update.Message,
                        Elapsed = update.Elapsed,
                        EstimatedRemaining = eta
                    });
                });

                var result = await work(progress, runtime.CancellationTokenSource.Token).ConfigureAwait(false);

                var elapsed = DateTimeOffset.UtcNow - startedAt;
                Update(jobId, job => job with
                {
                    State = result.IsSuccess ? JobState.Completed : JobState.Failed,
                    ProgressPercent = result.IsSuccess ? 100 : job.ProgressPercent,
                    StatusMessage = result.IsSuccess ? "Completed" : result.Error?.Message ?? "Failed",
                    Elapsed = elapsed,
                    EstimatedRemaining = null
                });
            }
            catch (OperationCanceledException)
            {
                Update(jobId, job => job with
                {
                    State = JobState.Cancelled,
                    StatusMessage = "Cancelled",
                    EstimatedRemaining = null
                });
            }
            catch (Exception ex)
            {
                Update(jobId, job => job with
                {
                    State = JobState.Failed,
                    StatusMessage = ex.Message,
                    EstimatedRemaining = null
                });
            }
            finally
            {
                runtime.CancellationTokenSource.Dispose();
            }
        }, CancellationToken.None);

        return await Task.FromResult(jobId).ConfigureAwait(false);
    }

    public bool Cancel(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var runtime))
        {
            return false;
        }

        var currentState = runtime.Job.State;
        if (currentState is JobState.Completed or JobState.Failed or JobState.Cancelled)
        {
            return false;
        }

        try
        {
            if (!runtime.CancellationTokenSource.IsCancellationRequested)
            {
                runtime.CancellationTokenSource.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Job already finished and token source got disposed.
        }

        Update(jobId, job => job with { State = JobState.Cancelled, StatusMessage = "Cancelled", EstimatedRemaining = null });
        return true;
    }

    private void Update(Guid jobId, Func<VaultArcJob, VaultArcJob> mutator)
    {
        if (!_jobs.TryGetValue(jobId, out var runtime))
        {
            return;
        }

        runtime.Job = mutator(runtime.Job);
        RaiseChanged();
    }

    private void RaiseChanged() => QueueChanged?.Invoke(this, EventArgs.Empty);

    private static TimeSpan? EstimateEta(double percent, TimeSpan elapsed)
    {
        if (percent <= 0 || percent >= 100 || elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        var remainingRatio = (100d - percent) / percent;
        var estimatedTicks = (long)(elapsed.Ticks * remainingRatio);
        if (estimatedTicks <= 0)
        {
            return null;
        }

        return TimeSpan.FromTicks(estimatedTicks);
    }

    private sealed class JobRuntime(VaultArcJob job, CancellationTokenSource cancellationTokenSource)
    {
        public VaultArcJob Job { get; set; } = job;

        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;
    }
}
