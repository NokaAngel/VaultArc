using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using VaultArc.App.ViewModels;

namespace VaultArc.App.Pages;

public sealed partial class HashToolsPage : Page
{
    public HashToolsViewModel ViewModel { get; } = App.Services.GetRequiredService<HashToolsViewModel>();

    public HashToolsPage()
    {
        InitializeComponent();
    }

    private async void BrowseLeftClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await PickFileAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ViewModel.LeftFilePath = path;
        }
    }

    private async void BrowseRightClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await PickFileAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ViewModel.RightFilePath = path;
        }
    }

    private async void HashClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.HashSingleAsync(ViewModel.LeftFilePath);
    }

    private async void CompareClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.CompareAsync();
    }

    private async void ExportClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("CSV", [".csv"]);
        picker.SuggestedFileName = "vaultarc-hashes";
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            await ViewModel.ExportAsync(file.Path);
        }
    }

    private static async Task<string?> PickFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
