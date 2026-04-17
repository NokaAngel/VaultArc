using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using VaultArc.App.ViewModels;

namespace VaultArc.App.Pages;

public sealed partial class ExtractPage : Page
{
    public ExtractViewModel ViewModel { get; } = App.Services.GetRequiredService<ExtractViewModel>();

    public ExtractPage()
    {
        InitializeComponent();
    }

    private async void BrowseArchiveClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            ViewModel.ArchivePath = file.Path;
        }
    }

    private async void BrowseDestinationClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            ViewModel.DestinationPath = folder.Path;
        }
    }

    private async void QueueClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await ViewModel.QueueExtractAsync();
            if (!IsPasswordRelated(result.Error?.Code))
            {
                ClearPassword();
                return;
            }

            var message = attempt == 0
                ? "This archive requires a password before extraction."
                : "Invalid password. Enter the archive password again.";
            var password = await PromptForPasswordAsync(message);
            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            ViewModel.Password = password;
        }

        ClearPassword();
    }

    private static bool IsPasswordRelated(string? errorCode) =>
        errorCode is "archive.password_required" or "archive.invalid_password";

    private async Task<string?> PromptForPasswordAsync(string message)
    {
        var passwordBox = new PasswordBox
        {
            PlaceholderText = "Archive password"
        };

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Password Required",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
                    passwordBox
                }
            },
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? passwordBox.Password : null;
    }

    private void ClearPassword()
    {
        ViewModel.Password = string.Empty;
    }
}
