using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class CreateArchivePage : UserControl
{
    private CreateArchiveViewModel ViewModel { get; }

    public CreateArchivePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CreateArchiveViewModel>();
        DataContext = ViewModel;

        BrowseDestBtn.Click += BrowseDestClicked;
        BrowseFolderBtn.Click += BrowseFolderClicked;
        BrowseFilesBtn.Click += BrowseFilesClicked;
        QueueBtn.Click += QueueClicked;
    }

    private async void BrowseDestClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Archive",
            SuggestedFileName = "vaultarc-output",
            FileTypeChoices =
            [
                new FilePickerFileType("ZIP Archive") { Patterns = ["*.zip"] },
                new FilePickerFileType("TAR Archive") { Patterns = ["*.tar"] },
                new FilePickerFileType("GZip Tarball") { Patterns = ["*.tgz"] },
                new FilePickerFileType("XZ Tarball") { Patterns = ["*.txz"] },
                new FilePickerFileType("VaultArc Encrypted") { Patterns = ["*.arc"] }
            ]
        });
        if (file?.TryGetLocalPath() is { } path) ViewModel.DestinationPath = path;
    }

    private async void BrowseFolderClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Add Folder", AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path) ViewModel.AddInputPath(path);
    }

    private async void BrowseFilesClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Add Files", AllowMultiple = true });
        foreach (var f in files)
            if (f.TryGetLocalPath() is { } path) ViewModel.AddInputPath(path);
    }

    private async void QueueClicked(object? sender, RoutedEventArgs e) => await ViewModel.QueueCreateAsync();
}
