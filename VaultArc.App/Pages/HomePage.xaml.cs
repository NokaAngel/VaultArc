using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using VaultArc.App.ViewModels;

namespace VaultArc.App.Pages;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; } = App.Services.GetRequiredService<HomeViewModel>();

    public HomePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    public Visibility HasRecentArchives(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void RecentArchiveClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            var vm = App.Services.GetRequiredService<OpenArchiveViewModel>();
            vm.ArchivePath = path;
            if (App.MainWindowInstance is MainWindow main)
                main.NavigateMainFrame(typeof(OpenArchivePage));
        }
    }

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
        if (items.Count == 0) return;

        var file = items[0];
        if (file is Windows.Storage.StorageFile storageFile)
        {
            var ext = Path.GetExtension(storageFile.Path).ToLowerInvariant();
            if (ext is ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".xz" or ".tgz" or ".txz" or ".arc")
            {
                var vm = App.Services.GetRequiredService<OpenArchiveViewModel>();
                vm.ArchivePath = storageFile.Path;
                if (App.MainWindowInstance is MainWindow main)
                    main.NavigateMainFrame(typeof(OpenArchivePage));
            }
        }
    }
}
