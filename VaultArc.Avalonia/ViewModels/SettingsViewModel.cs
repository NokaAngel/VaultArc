using CommunityToolkit.Mvvm.ComponentModel;
using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.Avalonia.ViewModels;

public partial class SettingsViewModel(VaultArcFacade facade) : ViewModelBase
{
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private bool _followSystemTheme = true;
    [ObservableProperty] private bool _confirmOverwrite = true;
    [ObservableProperty] private bool _safeExtractionMode = true;
    [ObservableProperty] private int _maxConcurrentJobs = 2;
    [ObservableProperty] private bool _useClassicLayout;
    [ObservableProperty] private ExtractionPolicyKind _extractionPolicy;
    [ObservableProperty] private bool _checkForUpdates;
    [ObservableProperty] private bool _sendCrashReports;

    public ExtractionPolicyKind[] ExtractionPolicies { get; } = Enum.GetValues<ExtractionPolicyKind>();

    public async Task LoadAsync()
    {
        var settings = await facade.LoadSettingsAsync(CancellationToken.None);
        IsDarkTheme = settings.IsDarkTheme;
        FollowSystemTheme = settings.FollowSystemTheme;
        ConfirmOverwrite = settings.ConfirmOverwrite;
        SafeExtractionMode = settings.SafeExtractionMode;
        MaxConcurrentJobs = settings.MaxConcurrentJobs;
        UseClassicLayout = settings.UseClassicLayout;
        ExtractionPolicy = settings.ExtractionPolicyKind;
        CheckForUpdates = settings.CheckForUpdates;
        SendCrashReports = settings.SendCrashReports;
    }

    public AppSettings BuildSettings() =>
        new(IsDarkTheme, ConfirmOverwrite, SafeExtractionMode, MaxConcurrentJobs,
            UseClassicLayout, FollowSystemTheme, ExtractionPolicyKind: ExtractionPolicy,
            CheckForUpdates: CheckForUpdates, SendCrashReports: SendCrashReports);

    public async Task SaveAsync()
    {
        var settings = BuildSettings();
        await facade.SaveSettingsAsync(settings, CancellationToken.None);
        StatusMessage = "Settings saved.";
    }
}
