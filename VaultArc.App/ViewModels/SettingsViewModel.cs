using CommunityToolkit.Mvvm.ComponentModel;
using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.App.ViewModels;

public partial class SettingsViewModel(VaultArcFacade facade) : ViewModelBase
{
    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private bool _followSystemTheme = true;

    [ObservableProperty]
    private bool _confirmOverwrite = true;

    [ObservableProperty]
    private bool _safeExtractionMode = true;

    [ObservableProperty]
    private int _maxConcurrentJobs = 2;

    [ObservableProperty]
    private bool _useClassicLayout;

    [ObservableProperty]
    private bool _shellContextMenuEnabled;

    public async Task LoadAsync()
    {
        var settings = await facade.LoadSettingsAsync(CancellationToken.None);
        IsDarkTheme = settings.IsDarkTheme;
        FollowSystemTheme = settings.FollowSystemTheme;
        ConfirmOverwrite = settings.ConfirmOverwrite;
        SafeExtractionMode = settings.SafeExtractionMode;
        MaxConcurrentJobs = settings.MaxConcurrentJobs;
        UseClassicLayout = settings.UseClassicLayout;
        ShellContextMenuEnabled = settings.ShellContextMenuEnabled;
    }

    public AppSettings BuildSettings() =>
        new(IsDarkTheme, ConfirmOverwrite, SafeExtractionMode, MaxConcurrentJobs,
            UseClassicLayout, FollowSystemTheme, ShellContextMenuEnabled);

    public async Task SaveAsync()
    {
        var settings = BuildSettings();
        await facade.SaveSettingsAsync(settings, CancellationToken.None);
        App.SyncShellContextMenu(settings.ShellContextMenuEnabled);
        StatusMessage = "Settings saved.";
    }
}
