using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.App.ViewModels;

public sealed class JobQueueViewModel : ViewModelBase
{
    private readonly VaultArcFacade _facade;
    private readonly SynchronizationContext? _uiContext;
    private readonly object _refreshGate = new();
    private readonly Dictionary<Guid, JobQueueItemView> _jobLookup = new();
    private bool _isRefreshScheduled;
    private bool _hasPendingRefresh;

    public JobQueueViewModel(VaultArcFacade facade)
    {
        _facade = facade;
        _uiContext = SynchronizationContext.Current;
        _facade.JobQueue.QueueChanged += JobQueueOnQueueChanged;
        ScheduleRefresh();
    }

    public ObservableCollection<JobQueueItemView> Jobs { get; } = [];

    public void Refresh() => ScheduleRefresh();

    private void RefreshCore()
    {
        var snapshot = _facade.JobQueue.Snapshot.ToList();
        var seen = new HashSet<Guid>();
        foreach (var job in snapshot)
        {
            seen.Add(job.JobId);
            if (_jobLookup.TryGetValue(job.JobId, out var existing))
            {
                existing.UpdateFrom(job);
                continue;
            }

            var created = JobQueueItemView.From(job);
            _jobLookup[job.JobId] = created;
            Jobs.Add(created);
        }

        var staleIds = _jobLookup.Keys.Where(id => !seen.Contains(id)).ToList();
        foreach (var staleId in staleIds)
        {
            if (!_jobLookup.Remove(staleId, out var staleItem))
            {
                continue;
            }

            _ = Jobs.Remove(staleItem);
        }

        for (var targetIndex = 0; targetIndex < snapshot.Count; targetIndex++)
        {
            var targetJobId = snapshot[targetIndex].JobId;
            if (!_jobLookup.TryGetValue(targetJobId, out var targetItem))
            {
                continue;
            }

            var currentIndex = Jobs.IndexOf(targetItem);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                Jobs.Move(currentIndex, targetIndex);
            }
        }

        StatusMessage = $"Jobs: {Jobs.Count}";
    }

    private void ScheduleRefresh()
    {
        lock (_refreshGate)
        {
            _hasPendingRefresh = true;
            if (_isRefreshScheduled)
            {
                return;
            }

            _isRefreshScheduled = true;
        }

        if (_uiContext is not null)
        {
            _uiContext.Post(_ => ProcessRefreshQueue(), null);
            return;
        }

        ProcessRefreshQueue();
    }

    private void ProcessRefreshQueue()
    {
        while (true)
        {
            lock (_refreshGate)
            {
                _hasPendingRefresh = false;
            }

            RefreshCore();

            lock (_refreshGate)
            {
                if (_hasPendingRefresh)
                {
                    continue;
                }

                _isRefreshScheduled = false;
                return;
            }
        }
    }

    public bool Cancel(Guid jobId)
    {
        var cancelled = _facade.JobQueue.Cancel(jobId);
        if (cancelled)
        {
            ScheduleRefresh();
        }

        return cancelled;
    }

    private void JobQueueOnQueueChanged(object? sender, EventArgs e) => ScheduleRefresh();
}

public sealed partial class JobQueueItemView : ObservableObject
{
    public Guid JobId { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _stateLabel = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _isInProgress;

    [ObservableProperty]
    private string _progressText = "0.0%";

    [ObservableProperty]
    private string _elapsedText = "Elapsed: 00:00";

    [ObservableProperty]
    private string _etaText = "ETA: --";

    public JobQueueItemView(Guid jobId)
    {
        JobId = jobId;
    }

    public static JobQueueItemView From(VaultArcJob job)
    {
        var view = new JobQueueItemView(job.JobId);
        view.UpdateFrom(job);
        return view;
    }

    public void UpdateFrom(VaultArcJob job)
    {
        var state = job.State switch
        {
            JobState.Pending => "Pending",
            JobState.Running => "Running",
            JobState.Completed => "Completed",
            JobState.Failed => "Failed",
            JobState.Cancelled => "Cancelled",
            _ => job.State.ToString()
        };

        Title = job.Title;
        StateLabel = state;
        StatusMessage = job.StatusMessage;
        ProgressPercent = job.ProgressPercent;
        IsInProgress = job.State is JobState.Pending or JobState.Running;
        ProgressText = $"{job.ProgressPercent:0.0}%";
        ElapsedText = $"Elapsed: {FormatDuration(job.Elapsed)}";
        EtaText = job.EstimatedRemaining is null ? "ETA: --" : $"ETA: {FormatDuration(job.EstimatedRemaining.Value)}";
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalHours >= 1)
        {
            return value.ToString(@"hh\:mm\:ss");
        }

        return value.ToString(@"mm\:ss");
    }
}
