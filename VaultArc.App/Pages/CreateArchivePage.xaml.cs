using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using VaultArc.App.Helpers;
using VaultArc.App.ViewModels;

namespace VaultArc.App.Pages;

public sealed partial class CreateArchivePage : Page
{
    public CreateArchiveViewModel ViewModel { get; } = App.Services.GetRequiredService<CreateArchiveViewModel>();

    public CreateArchivePage()
    {
        InitializeComponent();
    }

    private async void BrowseDestinationClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("ZIP Archive", [".zip"]);
        picker.FileTypeChoices.Add("TAR Archive", [".tar"]);
        picker.FileTypeChoices.Add("GZip Tarball", [".tgz"]);
        picker.FileTypeChoices.Add("XZ Tarball", [".txz"]);
        picker.FileTypeChoices.Add("GZip Archive", [".gz"]);
        picker.FileTypeChoices.Add("XZ Archive", [".xz"]);
        picker.FileTypeChoices.Add("VaultArc Encrypted Archive", [".arc"]);
        picker.SuggestedFileName = "vaultarc-output";
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            ViewModel.DestinationPath = file.Path;
            try
            {
                if (File.Exists(file.Path))
                {
                    var info = new FileInfo(file.Path);
                    if (info.Length == 0) File.Delete(file.Path);
                }
            }
            catch { }
        }
    }

    private async void BrowseInputFolderClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            ViewModel.AddInputPath(folder.Path);
    }

    private async void BrowseInputFilesClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var files = await picker.PickMultipleFilesAsync();
        foreach (var f in files)
            ViewModel.AddInputPath(f.Path);
    }

    private async void QueueClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.DestinationPath.EndsWith(".arc", StringComparison.OrdinalIgnoreCase))
        {
            var password = await PromptForPasswordWithStrengthAsync("Enter a password for this encrypted .arc archive.");
            if (string.IsNullOrWhiteSpace(password))
            {
                ViewModel.StatusMessage = "Password is required for .arc archives.";
                return;
            }

            ViewModel.Password = password;
        }

        await ViewModel.QueueCreateAsync();
        ViewModel.Password = string.Empty;
        PasswordStrengthPanel.Visibility = Visibility.Collapsed;
    }

    private async Task<string?> PromptForPasswordWithStrengthAsync(string message)
    {
        var passwordBox = new PasswordBox { PlaceholderText = "Archive password" };
        var strengthBar = new ProgressBar { Maximum = 100, Height = 6, Margin = new Thickness(0, 4, 0, 0) };
        var strengthLabel = new TextBlock { FontSize = 12, Opacity = 0.7 };

        passwordBox.PasswordChanged += (_, _) =>
        {
            var (_, label, percent) = PasswordStrengthHelper.Evaluate(passwordBox.Password);
            var level = PasswordStrengthHelper.Evaluate(passwordBox.Password).Level;
            strengthBar.Value = percent;
            strengthLabel.Text = label;
            var colorHex = PasswordStrengthHelper.GetColor(level);
            try
            {
                var r = Convert.ToByte(colorHex[1..3], 16);
                var g = Convert.ToByte(colorHex[3..5], 16);
                var b = Convert.ToByte(colorHex[5..7], 16);
                strengthBar.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
            }
            catch { }
        };

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Archive Password",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
                    passwordBox,
                    strengthBar,
                    strengthLabel
                }
            },
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? passwordBox.Password : null;
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Add to archive";
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        foreach (var item in items)
            ViewModel.AddInputPath(item.Path);
    }
}
