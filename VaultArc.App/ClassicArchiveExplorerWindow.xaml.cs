using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using VaultArc.App.ArchiveExplorer;
using VaultArc.App.Helpers;
using VaultArc.App.Pages;
using VaultArc.App.ViewModels;
using VaultArc.Core;
using WinRT.Interop;

namespace VaultArc.App;

public sealed partial class ClassicArchiveExplorerWindow : Window
{
    public static ClassicArchiveExplorerWindow? Instance { get; private set; }

    public OpenArchiveViewModel ViewModel { get; } = App.Services.GetRequiredService<OpenArchiveViewModel>();

    private IFileTypeClassificationService Classifier { get; } =
        App.Services.GetRequiredService<IFileTypeClassificationService>();

    private ArchiveExplorerPresenter? _presenter;

    public ClassicArchiveExplorerWindow()
    {
        InitializeComponent();
        Instance = this;
        Closed += (_, _) => Instance = null;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootGrid.Loaded += (_, _) =>
        {
            _presenter = new ArchiveExplorerPresenter(
                ViewModel,
                Classifier,
                () => RootGrid.XamlRoot,
                () => WindowNative.GetWindowHandle(this));
        };
    }

    public static void EnsureVisible()
    {
        if (Instance is null)
        {
            var window = new ClassicArchiveExplorerWindow();
            window.Activate();
        }
        else
        {
            Instance.Activate();
        }
    }

    public static void ApplyRootTheme(bool isDark)
    {
        if (Instance?.Content is FrameworkElement root)
        {
            root.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        }
    }

    private ArchiveExplorerPresenter P =>
        _presenter ?? throw new InvalidOperationException("Presenter not ready.");

    private async void BrowseClicked(object sender, RoutedEventArgs e) => await P.BrowseAsync();

    private async void OpenClicked(object sender, RoutedEventArgs e) => await P.OpenArchiveAsync();

    private async void PreviewClicked(object sender, RoutedEventArgs e) => await P.PreviewAsync();

    private async void IntegrityClicked(object sender, RoutedEventArgs e) => await P.IntegrityAsync();

    private async void OpenSelectedClicked(object sender, RoutedEventArgs e) => await P.OpenSelectedAsync();

    private async void RunSelectedClicked(object sender, RoutedEventArgs e) => await P.RunSelectedAsync();

    private void NavigateUpClicked(object sender, RoutedEventArgs e) => ViewModel.NavigateUp();

    private void NavigateRootClicked(object sender, RoutedEventArgs e) => ViewModel.NavigateToRoot();

    private void RefreshClicked(object sender, RoutedEventArgs e) => ViewModel.RefreshVisibleItems();

    private async void ViewTempFolderClicked(object sender, RoutedEventArgs e) => await P.ViewTempFolderAsync();

    private void CopyPathClicked(object sender, RoutedEventArgs e) => P.CopyPath();

    private async void EntriesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        await P.EntriesDoubleTappedAsync();

    private async void EntriesList_KeyDown(object sender, KeyRoutedEventArgs e) => await P.EntriesKeyDownAsync(e);

    private async void RenameEntryClicked(object sender, RoutedEventArgs e) => await P.RenameSelectedAsync();

    private async void DeleteEntryClicked(object sender, RoutedEventArgs e) => await P.DeleteSelectedAsync();

    private void FindClicked(object sender, RoutedEventArgs e) =>
        _ = ClassicSearchBox.Focus(FocusState.Programmatic);

    private void CreateArchiveMenuClicked(object sender, RoutedEventArgs e)
    {
        P.NavigateMainTo(typeof(CreateArchivePage));
    }

    private async void ExtractMenuClicked(object sender, RoutedEventArgs e) => await P.BrowseExtractDestinationAsync();

    private async void BrowseExtractDestinationClicked(object sender, RoutedEventArgs e) => await P.BrowseExtractDestinationAsync();

    private async void ExtractAllClicked(object sender, RoutedEventArgs e) => await P.ExtractAllAsync();

    private async void HelpClicked(object sender, RoutedEventArgs e) => await P.ShowClassicHelpAsync();

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();

    public static string GetEntryIcon(string fullPath, bool isDirectory) =>
        EntryIconHelper.GetGlyph(fullPath, isDirectory);

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Open in VaultArc";
        }
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        if (_presenter is not null)
            await _presenter.HandleDroppedFilesAsync(items);
    }
}
