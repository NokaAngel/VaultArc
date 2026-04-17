using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class ExtractPage : UserControl
{
    private ExtractViewModel ViewModel { get; }

    public ExtractPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ExtractViewModel>();
        DataContext = ViewModel;

        BrowseArchiveBtn.Click += BrowseArchiveClicked;
        BrowseDestBtn.Click += BrowseDestClicked;
        QueueBtn.Click += QueueClicked;
    }

    private async void BrowseArchiveClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open Archive", AllowMultiple = false });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) ViewModel.ArchivePath = path;
    }

    private async void BrowseDestClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Extract Destination", AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path) ViewModel.DestinationPath = path;
    }

    private async void QueueClicked(object? sender, RoutedEventArgs e) => await ViewModel.QueueExtractAsync();
}
