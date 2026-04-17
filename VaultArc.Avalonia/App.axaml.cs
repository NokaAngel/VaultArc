using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VaultArc.Archive;
using VaultArc.Avalonia.Services;
using VaultArc.Avalonia.ViewModels;
using VaultArc.Avalonia.Views;
using VaultArc.Core;
using VaultArc.Hashing;
using VaultArc.Infrastructure;
using VaultArc.Jobs;
using VaultArc.Models;
using VaultArc.Security;
using VaultArc.Services;

namespace VaultArc.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static AppSettings _currentSettings = new(true, true, true, 2);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        Services = ConfigureServices();

        try
        {
            var facade = Services.GetRequiredService<VaultArcFacade>();
            _currentSettings = await facade.LoadSettingsAsync(CancellationToken.None);
            var tempSessions = Services.GetRequiredService<ITempSessionService>();
            _ = await tempSessions.CleanupOldSessionsAsync(TimeSpan.FromHours(24), CancellationToken.None);
        }
        catch { }

        ApplyTheme(_currentSettings);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(AppSettings settings)
    {
        _currentSettings = settings;

        var isDark = settings.FollowSystemTheme
            ? DetectSystemIsDark()
            : settings.IsDarkTheme;

        if (Current is App app)
        {
            app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    public static void ApplyTheme(bool isDark)
    {
        ApplyTheme(new AppSettings(isDark, true, true, 2));
    }

    private static bool DetectSystemIsDark()
    {
        if (Current is App app)
        {
            return app.ActualThemeVariant == ThemeVariant.Dark;
        }
        return true;
    }

    private static IServiceProvider ConfigureServices()
    {
        var collection = new ServiceCollection();

        collection.AddSingleton(typeof(ILogger<>), typeof(VaultArcLogger<>));
        collection.AddSingleton<IExtractionSafetyService, ExtractionSafetyService>();
        collection.AddSingleton<IArchiveSecurityService, ExtractionSafetyService>();
        collection.AddSingleton<IArchiveService, SharpCompressArchiveService>();
        collection.AddSingleton<IHashingService, FileHashingService>();
        collection.AddSingleton<IJobQueueService, InMemoryJobQueueService>();
        collection.AddSingleton<IRecentArchivesStore, JsonRecentArchivesStore>();
        collection.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        collection.AddSingleton<IFileTypeClassificationService, FileTypeClassificationService>();
        collection.AddSingleton<ITempSessionService, TempSessionService>();
        collection.AddSingleton<IArchiveBrowseService, ArchiveBrowseService>();
        collection.AddSingleton<IArchiveNavigationService, ArchiveNavigationService>();
        collection.AddSingleton<IArchiveLaunchService, ArchiveLaunchService>();
        collection.AddSingleton<IPlatformService, CrossPlatformService>();
        collection.AddSingleton<VaultArcFacade>();

        collection.AddTransient<HomeViewModel>();
        collection.AddSingleton<OpenArchiveViewModel>();
        collection.AddTransient<CreateArchiveViewModel>();
        collection.AddTransient<ExtractViewModel>();
        collection.AddTransient<JobQueueViewModel>();
        collection.AddTransient<HashToolsViewModel>();
        collection.AddTransient<SettingsViewModel>();
        collection.AddTransient<AboutViewModel>();

        return collection.BuildServiceProvider();
    }
}
