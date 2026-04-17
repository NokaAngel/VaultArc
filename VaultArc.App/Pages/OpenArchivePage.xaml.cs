using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using VaultArc.App.ArchiveExplorer;
using VaultArc.App.Helpers;
using VaultArc.App.ViewModels;
using VaultArc.Core;
using WinRT.Interop;

namespace VaultArc.App.Pages;

public sealed partial class OpenArchivePage : Page
{
    public OpenArchiveViewModel ViewModel { get; } = App.Services.GetRequiredService<OpenArchiveViewModel>();
    private IFileTypeClassificationService Classifier { get; } = App.Services.GetRequiredService<IFileTypeClassificationService>();
    private ArchiveExplorerPresenter? _presenter;

    public OpenArchivePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _presenter = new ArchiveExplorerPresenter(
                ViewModel,
                Classifier,
                () => Content.XamlRoot,
                () => WindowNative.GetWindowHandle(App.MainWindowInstance));
        };
    }

    public static string GetEntryIcon(string fullPath, bool isDirectory) =>
        EntryIconHelper.GetGlyph(fullPath, isDirectory);

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

    private void BreadcrumbClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string folderPath)
            ViewModel.NavigateToBreadcrumb(folderPath);
    }

    private void RefreshClicked(object sender, RoutedEventArgs e) => ViewModel.RefreshVisibleItems();
    private async void ViewTempFolderClicked(object sender, RoutedEventArgs e) => await P.ViewTempFolderAsync();
    private void CopyPathClicked(object sender, RoutedEventArgs e) => P.CopyPath();
    private async void EntriesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => await P.EntriesDoubleTappedAsync();
    private async void RenameEntryClicked(object sender, RoutedEventArgs e) => await P.RenameSelectedAsync();
    private async void DeleteEntryClicked(object sender, RoutedEventArgs e) => await P.DeleteSelectedAsync();
    private async void EntriesList_KeyDown(object sender, KeyRoutedEventArgs e) => await P.EntriesKeyDownAsync(e);
    private async void BrowseExtractDestinationClicked(object sender, RoutedEventArgs e) => await P.BrowseExtractDestinationAsync();
    private async void ExtractAllClicked(object sender, RoutedEventArgs e) => await P.ExtractAllAsync();

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Open in VaultArc";
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        if (_presenter is not null)
            await _presenter.HandleDroppedFilesAsync(items);
    }

    private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_presenter is not null)
        {
            await _presenter.HandleGlobalKeyAsync(e);
            if (e.Handled && e.Key == Windows.System.VirtualKey.F)
                SearchBox.Focus(FocusState.Programmatic);
        }
    }
}
