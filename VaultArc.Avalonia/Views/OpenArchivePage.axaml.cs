using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class OpenArchivePage : UserControl
{
    private OpenArchiveViewModel ViewModel { get; }

    public OpenArchivePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<OpenArchiveViewModel>();
        DataContext = ViewModel;

        BrowseBtn.Click += BrowseClicked;
        OpenBtn.Click += OpenClicked;
        IntegrityBtn.Click += IntegrityClicked;
        RootBtn.Click += (_, _) => ViewModel.NavigateToRoot();
        UpBtn.Click += (_, _) => ViewModel.NavigateUp();
        OpenSelBtn.Click += OpenSelectedClicked;
        RunSelBtn.Click += RunSelectedClicked;
        TempBtn.Click += TempClicked;
        RefreshBtn.Click += (_, _) => ViewModel.RefreshVisibleItems();
        PreviewBtn.Click += PreviewClicked;
        BrowseExtractBtn.Click += BrowseExtractClicked;
        ExtractAllBtn.Click += ExtractAllClicked;
        EntriesList.DoubleTapped += EntriesDoubleTapped;
    }

    private async void BrowseClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Archive",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Archives") { Patterns = ["*.zip", "*.7z", "*.rar", "*.tar", "*.gz", "*.xz", "*.tgz", "*.txz", "*.arc"] }, FilePickerFileTypes.All]
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            ViewModel.ArchivePath = path;
    }

    private async void OpenClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.OpenAsync(ViewModel.ArchivePath);

    private async void IntegrityClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.QueueIntegrityTestAsync();

    private async void OpenSelectedClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.OpenSelectedAsync();

    private async void RunSelectedClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.RunSelectedAsync();

    private async void TempClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.OpenLastSessionFolderAsync();

    private async void PreviewClicked(object? sender, RoutedEventArgs e)
    {
        await ViewModel.PreviewSelectedAsync();

        var truncWarn = this.FindControl<TextBlock>("TruncationWarning");
        var previewImg = this.FindControl<Image>("PreviewImage");
        var previewTxt = this.FindControl<TextBox>("PreviewTextBox");
        if (truncWarn != null) truncWarn.IsVisible = ViewModel.IsPreviewTruncated;
        if (previewImg != null && previewTxt != null)
        {
            if (ViewModel.IsPreviewImage && ViewModel.PreviewData is { Length: > 0 })
            {
                try
                {
                    using var ms = new System.IO.MemoryStream(ViewModel.PreviewData);
                    previewImg.Source = new Bitmap(ms);
                    previewImg.IsVisible = true;
                    previewTxt.IsVisible = false;
                }
                catch
                {
                    previewImg.IsVisible = false;
                    previewTxt.IsVisible = true;
                }
            }
            else
            {
                previewImg.IsVisible = false;
                previewTxt.IsVisible = true;
            }
        }
    }

    private async void BrowseExtractClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Extract Destination", AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            ViewModel.ExtractDestination = path;
    }

    private async void ExtractAllClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.QueueExtractAllAsync();

    private async void EntriesDoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (ViewModel.SelectedItem?.IsDirectory is true)
            ViewModel.NavigateIntoSelectedFolder();
        else
            await ViewModel.OpenSelectedAsync();
    }
}
