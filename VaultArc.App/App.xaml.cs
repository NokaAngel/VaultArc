using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using VaultArc.Archive;
using VaultArc.App.Services;
using VaultArc.App.ViewModels;
using VaultArc.Core;
using VaultArc.Hashing;
using VaultArc.Infrastructure;
using VaultArc.Jobs;
using VaultArc.Models;
using VaultArc.Security;
using VaultArc.Services;

namespace VaultArc.App;

public partial class App : Application
{
    private Window? _window;
    public static Window MainWindowInstance { get; private set; } = null!;
    public static IServiceProvider Services { get; } = ConfigureServices();

    private static SystemThemeWatcher? _themeWatcher;
    private static TaskbarProgressService? _taskbarProgress;
    private static AppSettings _currentSettings = new(true, true, true, 2);

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = new AppSettings(true, true, true, 2);
        try
        {
            var facade = Services.GetRequiredService<VaultArcFacade>();
            settings = await facade.LoadSettingsAsync(CancellationToken.None);
            var tempSessions = Services.GetRequiredService<ITempSessionService>();
            _ = await tempSessions.CleanupOldSessionsAsync(TimeSpan.FromHours(24), CancellationToken.None);
            Services.GetRequiredService<JobNotificationService>().Start();
        }
        catch (COMException) { }
        catch (Exception) { }

        _currentSettings = settings;

        _window = new MainWindow();
        MainWindowInstance = _window;
        _window.Activate();
        ApplyTheme(settings);

        _themeWatcher = new SystemThemeWatcher(DispatcherQueue.GetForCurrentThread());
        _themeWatcher.SystemThemeChanged += OnSystemThemeChanged;

        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            _taskbarProgress = new TaskbarProgressService(Services.GetRequiredService<IJobQueueService>(), hwnd);
            _taskbarProgress.Start();
        }
        catch { }

        HandleCommandLineArgs();
    }

    private static void OnSystemThemeChanged(bool isDark)
    {
        if (!_currentSettings.FollowSystemTheme)
        {
            return;
        }

        _currentSettings = _currentSettings with { IsDarkTheme = isDark };
        ApplyTheme(_currentSettings);
    }

    public static void ApplyTheme(bool isDarkTheme)
    {
        ApplyTheme(new AppSettings(isDarkTheme, true, true, 2));
    }

    public static void ApplyTheme(AppSettings settings)
    {
        _currentSettings = settings;

        var isDark = settings.FollowSystemTheme
            ? SystemThemeWatcher.GetSystemIsDark()
            : settings.IsDarkTheme;

        var elementTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        var appTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

        try { Current.RequestedTheme = appTheme; }
        catch (COMException) { }

        if (MainWindowInstance?.Content is FrameworkElement root)
        {
            try { root.RequestedTheme = elementTheme; }
            catch (COMException) { }
        }

        if (MainWindowInstance is MainWindow mainWindow)
        {
            mainWindow.ApplyWindowChromeTheme(isDark);
        }

        ClassicArchiveExplorerWindow.ApplyRootTheme(isDark);
    }

    public static void SyncShellContextMenu(bool enabled)
    {
        try
        {
            if (enabled)
            {
                ShellContextMenuService.Register();
                FileAssociationService.Register();
            }
            else
            {
                ShellContextMenuService.Unregister();
                FileAssociationService.Unregister();
            }
        }
        catch { }
    }

    private static void HandleCommandLineArgs()
    {
        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Length < 2)
        {
            return;
        }

        var verb = cmdArgs[1].ToLowerInvariant();
        var filePath = cmdArgs.Length > 2 ? cmdArgs[2] : null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        if (MainWindowInstance is not MainWindow mainWindow)
        {
            return;
        }

        switch (verb)
        {
            case "--open":
                var openVm = Services.GetRequiredService<OpenArchiveViewModel>();
                openVm.ArchivePath = filePath;
                mainWindow.NavigateMainFrame(typeof(Pages.OpenArchivePage));
                break;

            case "--extract":
            case "--extract-here":
                var extractVm = Services.GetRequiredService<OpenArchiveViewModel>();
                extractVm.ArchivePath = filePath;
                if (verb == "--extract-here")
                {
                    extractVm.ExtractDestination = Path.GetDirectoryName(filePath) ?? "";
                }
                mainWindow.NavigateMainFrame(typeof(Pages.OpenArchivePage));
                break;

            case "--add":
                var createVm = Services.GetRequiredService<CreateArchiveViewModel>();
                createVm.InputPaths = filePath;
                mainWindow.NavigateMainFrame(typeof(Pages.CreateArchivePage));
                break;
        }
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
        collection.AddSingleton<VaultArcFacade>();
        collection.AddSingleton<JobNotificationService>();

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
