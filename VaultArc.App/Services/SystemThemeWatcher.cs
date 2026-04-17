using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace VaultArc.App.Services;

/// <summary>
/// Monitors the Windows system color theme and raises an event whenever
/// the user switches between Light and Dark in Windows Settings.
/// </summary>
internal sealed class SystemThemeWatcher : IDisposable
{
    private readonly UISettings _uiSettings = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _lastIsDark;

    public event Action<bool>? SystemThemeChanged;

    public SystemThemeWatcher(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _lastIsDark = GetSystemIsDark();
        _uiSettings.ColorValuesChanged += OnColorValuesChanged;
    }

    public static bool GetSystemIsDark()
    {
        var uiSettings = new UISettings();
        var bg = uiSettings.GetColorValue(UIColorType.Background);
        return bg.R < 128;
    }

    private void OnColorValuesChanged(UISettings sender, object args)
    {
        var isDark = GetSystemIsDark();
        if (isDark == _lastIsDark)
        {
            return;
        }

        _lastIsDark = isDark;
        _dispatcherQueue.TryEnqueue(() => SystemThemeChanged?.Invoke(isDark));
    }

    public void Dispose()
    {
        _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
    }
}
