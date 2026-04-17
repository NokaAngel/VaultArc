using System.Runtime.InteropServices;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.App.Services;

/// <summary>
/// Shows aggregate job progress on the Windows taskbar icon.
/// </summary>
internal sealed class TaskbarProgressService : IDisposable
{
    private readonly IJobQueueService _jobQueue;
    private readonly nint _hwnd;
    private bool _started;

    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(nint hwnd);
        void DeleteTab(nint hwnd);
        void ActivateTab(nint hwnd);
        void SetActiveAlt(nint hwnd);
        void MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
        void SetProgressValue(nint hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(nint hwnd, int tbpFlags);
    }

    [ComImport]
    [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarInstance { }

    private const int TBPF_NOPROGRESS = 0;
    private const int TBPF_NORMAL = 2;

    private ITaskbarList3? _taskbar;

    public TaskbarProgressService(IJobQueueService jobQueue, nint hwnd)
    {
        _jobQueue = jobQueue;
        _hwnd = hwnd;
    }

    public void Start()
    {
        if (_started) return;
        _started = true;

        try
        {
            _taskbar = (ITaskbarList3)new TaskbarInstance();
            _taskbar.HrInit();
        }
        catch
        {
            _taskbar = null;
            return;
        }

        _jobQueue.QueueChanged += OnQueueChanged;
    }

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        if (_taskbar is null) return;

        try
        {
            var running = _jobQueue.Snapshot.Where(j => j.State == JobState.Running).ToList();
            if (running.Count == 0)
            {
                _taskbar.SetProgressState(_hwnd, TBPF_NOPROGRESS);
                return;
            }

            var avg = running.Average(j => j.ProgressPercent);
            _taskbar.SetProgressState(_hwnd, TBPF_NORMAL);
            _taskbar.SetProgressValue(_hwnd, (ulong)avg, 100);
        }
        catch { }
    }

    public void Dispose()
    {
        _jobQueue.QueueChanged -= OnQueueChanged;
        try { _taskbar?.SetProgressState(_hwnd, TBPF_NOPROGRESS); } catch { }
    }
}
