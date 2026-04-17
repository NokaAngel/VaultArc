using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.App.Services;

public sealed class JobNotificationService(IJobQueueService jobQueueService)
{
    private readonly Dictionary<Guid, JobState> _knownStates = new();
    private bool _started;
    private bool _notificationsAvailable;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _notificationsAvailable = TryInitializeNotificationPlatform();
        jobQueueService.QueueChanged += OnQueueChanged;
        RefreshStateSnapshot();
    }

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        foreach (var job in jobQueueService.Snapshot)
        {
            if (!_knownStates.TryGetValue(job.JobId, out var previous))
            {
                _knownStates[job.JobId] = job.State;
                if (job.State == JobState.Running)
                {
                    Notify("VaultArc", $"Started: {job.Title}");
                }

                continue;
            }

            if (previous == job.State)
            {
                continue;
            }

            _knownStates[job.JobId] = job.State;
            switch (job.State)
            {
                case JobState.Running:
                    Notify("VaultArc", $"Running: {job.Title}");
                    break;
                case JobState.Completed:
                    Notify("VaultArc", $"Completed: {job.Title}");
                    break;
                case JobState.Failed:
                    Notify("VaultArc", $"Failed: {job.Title}\n{job.StatusMessage}");
                    break;
                case JobState.Cancelled:
                    Notify("VaultArc", $"Cancelled: {job.Title}");
                    break;
            }
        }
    }

    private void Notify(string title, string message)
    {
        if (!_notificationsAvailable)
        {
            return;
        }

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            _notificationsAvailable = false;
        }
    }

    private static bool TryInitializeNotificationPlatform()
    {
        try
        {
            AppNotificationManager.Default.Register();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RefreshStateSnapshot()
    {
        foreach (var job in jobQueueService.Snapshot)
        {
            _knownStates[job.JobId] = job.State;
        }
    }
}
