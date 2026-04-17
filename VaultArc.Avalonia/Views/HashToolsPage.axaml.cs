using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class HashToolsPage : UserControl
{
    private HashToolsViewModel ViewModel { get; }

    public HashToolsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<HashToolsViewModel>();
        DataContext = ViewModel;

        BrowseLeftBtn.Click += BrowseLeftClicked;
        BrowseRightBtn.Click += BrowseRightClicked;
        HashBtn.Click += HashClicked;
        CompareBtn.Click += CompareClicked;
        ExportBtn.Click += ExportClicked;
    }

    private async void BrowseLeftClicked(object? sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync();
        if (!string.IsNullOrWhiteSpace(path)) ViewModel.LeftFilePath = path;
    }

    private async void BrowseRightClicked(object? sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync();
        if (!string.IsNullOrWhiteSpace(path)) ViewModel.RightFilePath = path;
    }

    private async void HashClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.HashSingleAsync(ViewModel.LeftFilePath);

    private async void CompareClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.CompareAsync();

    private async void ExportClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Hash Report",
            SuggestedFileName = "vaultarc-hashes",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
        });
        if (file?.TryGetLocalPath() is { } path) await ViewModel.ExportAsync(path);
    }

    private async Task<string?> PickFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Select File", AllowMultiple = false });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
