using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using VaultArc.App.Pages;
using VaultArc.App.ViewModels;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.App.ArchiveExplorer;

internal sealed class ArchiveExplorerPresenter
{
    private readonly OpenArchiveViewModel _vm;
    private readonly IFileTypeClassificationService _classifier;
    private readonly Func<XamlRoot> _getXamlRoot;
    private readonly Func<nint> _getPickerWindowHandle;
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);

    public ArchiveExplorerPresenter(
        OpenArchiveViewModel vm,
        IFileTypeClassificationService classifier,
        Func<XamlRoot> getXamlRoot,
        Func<nint> getPickerWindowHandle)
    {
        _vm = vm;
        _classifier = classifier;
        _getXamlRoot = getXamlRoot;
        _getPickerWindowHandle = getPickerWindowHandle;
    }

    public async Task BrowseAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _getPickerWindowHandle());
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            _vm.ArchivePath = file.Path;
        }
    }

    public async Task BrowseExtractDestinationAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _getPickerWindowHandle());
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            _vm.ExtractDestination = folder.Path;
        }
    }

    public async Task OpenArchiveAsync()
    {
        await EnsurePasswordThenAsync(async () =>
        {
            var result = await _vm.OpenAsync(_vm.ArchivePath);
            return IsPasswordRelated(result.Error?.Code);
        });
    }

    public async Task PreviewAsync()
    {
        await EnsurePasswordThenAsync(async () =>
        {
            var result = await _vm.PreviewSelectedAsync();
            return IsPasswordRelated(result.Error?.Code);
        });
    }

    public async Task IntegrityAsync()
    {
        await EnsurePasswordThenAsync(async () =>
        {
            var result = await _vm.QueueIntegrityTestAsync();
            return IsPasswordRelated(result.Error?.Code);
        });
    }

    public async Task OpenSelectedAsync()
    {
        await OpenSelectedWithPromptAsync();
    }

    public async Task RunSelectedAsync()
    {
        await RunSelectedWithPromptAsync();
    }

    public async Task ExtractAllAsync()
    {
        await EnsurePasswordThenAsync(async () =>
        {
            var result = await _vm.QueueExtractAllAsync();
            return IsPasswordRelated(result.Error?.Code);
        });
    }

    public async Task RenameSelectedAsync()
    {
        if (_vm.SelectedItem is null)
        {
            return;
        }

        var newName = await PromptForTextAsync("Rename Entry", "Enter a new file name:", _vm.SelectedItem.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        await EnsurePasswordThenAsync(async () =>
        {
            var result = await _vm.RenameSelectedAsync(newName);
            return IsPasswordRelated(result.Error?.Code);
        });
    }

    public async Task DeleteSelectedAsync()
    {
        if (_vm.SelectedItem is null)
        {
            return;
        }

        var confirmed = await ConfirmAsync("Delete Entry", $"Delete '{_vm.SelectedItem.Name}' from archive?");
        if (!confirmed)
        {
            return;
        }

        await EnsurePasswordThenAsync(async () =>
        {
            var result = await _vm.DeleteSelectedAsync();
            return IsPasswordRelated(result.Error?.Code);
        });
    }

    public async Task ViewTempFolderAsync()
    {
        var result = await _vm.OpenLastSessionFolderAsync();
        if (result.IsFailure)
        {
            _vm.StatusMessage = result.Error?.Message ?? "No temp session to show.";
        }
    }

    public void CopyPath()
    {
        if (_vm.SelectedItem is null)
        {
            return;
        }

        var data = new DataPackage();
        data.SetText(_vm.SelectedItem.FullPath);
        Clipboard.SetContent(data);
        _vm.StatusMessage = "Copied archive entry path.";
    }

    public async Task EntriesDoubleTappedAsync()
    {
        if (_vm.SelectedItem?.IsDirectory is true)
        {
            _vm.NavigateIntoSelectedFolder();
            return;
        }

        await OpenSelectedWithPromptAsync();
    }

    public async Task EntriesKeyDownAsync(KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Enter:
                if (_vm.SelectedItem?.IsDirectory is true)
                    _vm.NavigateIntoSelectedFolder();
                else
                    await OpenSelectedWithPromptAsync();
                break;

            case Windows.System.VirtualKey.Delete:
                await DeleteSelectedAsync();
                break;

            case Windows.System.VirtualKey.F2:
                await RenameSelectedAsync();
                break;

            case Windows.System.VirtualKey.Back:
            case Windows.System.VirtualKey.Left when IsAltDown():
                _vm.NavigateUp();
                break;
        }
    }

    public async Task HandleGlobalKeyAsync(KeyRoutedEventArgs e)
    {
        if (!IsCtrlDown()) return;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.O:
                await BrowseAsync();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.E:
                await BrowseExtractDestinationAsync();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.F:
                // Focus will be handled by the page
                e.Handled = true;
                break;
        }
    }

    public async Task HandleDroppedFilesAsync(IReadOnlyList<Windows.Storage.IStorageItem> items)
    {
        if (items.Count == 0) return;

        var file = items[0];
        if (file is Windows.Storage.StorageFile storageFile)
        {
            var ext = Path.GetExtension(storageFile.Path).ToLowerInvariant();
            if (ext is ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".xz" or ".tgz" or ".txz" or ".arc")
            {
                _vm.ArchivePath = storageFile.Path;
                await OpenArchiveAsync();
            }
        }
    }

    private static bool IsCtrlDown() =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private static bool IsAltDown() =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    public void NavigateMainTo(Type pageType)
    {
        if (App.MainWindowInstance is MainWindow main)
        {
            main.NavigateMainFrame(pageType);
        }
    }

    public async Task ShowClassicHelpAsync()
    {
        await ShowInfoAsync(
            "Classic Archive Explorer",
            "This is the WinRAR-style archive window. Use the main VaultArc window for other tools. Turn this mode off in Settings if you prefer the built-in page.");
    }

    /// <summary>
    /// Core password flow: uses cached password first, only prompts if the operation
    /// returns a password-related error. Caches on success so subsequent operations
    /// never re-prompt for the same archive.
    /// </summary>
    private async Task EnsurePasswordThenAsync(Func<Task<bool>> operationReturnsNeedsPassword)
    {
        var cached = _vm.GetCachedPassword();
        if (cached is not null)
        {
            _vm.Password = cached;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var needsPassword = await operationReturnsNeedsPassword();
            if (!needsPassword)
            {
                if (!string.IsNullOrWhiteSpace(_vm.Password))
                {
                    _vm.CachePassword(_vm.Password);
                }

                _vm.Password = string.Empty;
                return;
            }

            var prompt = attempt == 0 && cached is null
                ? "This archive is encrypted. Enter its password."
                : "Invalid password. Try again.";
            var password = await PromptForPasswordAsync(prompt);
            if (string.IsNullOrWhiteSpace(password))
            {
                _vm.Password = string.Empty;
                return;
            }

            _vm.Password = password;
        }

        _vm.Password = string.Empty;
    }

    private async Task OpenSelectedWithPromptAsync()
    {
        if (_vm.SelectedItem is null)
        {
            return;
        }

        var classification = _classifier.Classify(_vm.SelectedItem.FullPath, _vm.SelectedItem.IsDirectory);
        if (classification.IsRunnable)
        {
            var choice = await PromptRunnableOpenChoiceAsync(_vm.SelectedItem.Name);
            if (choice == RunnableChoice.Cancel)
            {
                return;
            }

            if (choice == RunnableChoice.Run)
            {
                await RunSelectedWithPromptAsync();
                return;
            }

            await EnsurePasswordThenAsync(async () =>
            {
                var r = await _vm.OpenSelectedAsync();
                if (r.IsSuccess && r.Value is not null)
                {
                    _ = await _vm.OpenLastSessionFolderAsync();
                }

                return IsPasswordRelated(r.Error?.Code);
            });
            return;
        }

        await EnsurePasswordThenAsync(async () =>
        {
            var r = await _vm.OpenSelectedAsync();
            return IsPasswordRelated(r.Error?.Code);
        });
    }

    private async Task RunSelectedWithPromptAsync()
    {
        if (_vm.SelectedItem is null)
        {
            return;
        }

        var classification = _classifier.Classify(_vm.SelectedItem.FullPath, _vm.SelectedItem.IsDirectory);
        if (classification.RequiresStrongWarning)
        {
            var warning = await ConfirmAsync(
                "Security Warning",
                $"You are about to run '{_vm.SelectedItem.Name}' from archive '{_vm.ArchivePath}'.\n\nScripts and shortcuts may be unsafe. Continue?");

            if (!warning)
            {
                return;
            }
        }

        await EnsurePasswordThenAsync(async () =>
        {
            var result = await _vm.RunSelectedAsync();
            if (result.IsFailure && result.Error?.Code == "launch.start_failed")
            {
                await ShowInfoAsync("Launch Failed", result.Error?.Message ?? "The selected program could not be started.");
            }

            return IsPasswordRelated(result.Error?.Code);
        });
    }

    private static bool IsPasswordRelated(string? errorCode) =>
        errorCode is "archive.password_required" or "archive.invalid_password";

    private async Task<string?> PromptForPasswordAsync(string message)
    {
        var passwordBox = new PasswordBox { PlaceholderText = "Archive password" };
        var dialog = new ContentDialog
        {
            XamlRoot = _getXamlRoot(),
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

        var result = await ShowDialogSerializedAsync(dialog);
        return result == ContentDialogResult.Primary ? passwordBox.Password : null;
    }

    private async Task<string?> PromptForTextAsync(string title, string message, string defaultValue)
    {
        var textBox = new TextBox { Text = defaultValue };
        var dialog = new ContentDialog
        {
            XamlRoot = _getXamlRoot(),
            Title = title,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
                    textBox
                }
            },
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await ShowDialogSerializedAsync(dialog);
        return result == ContentDialogResult.Primary ? textBox.Text.Trim() : null;
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _getXamlRoot(),
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await ShowDialogSerializedAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _getXamlRoot(),
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
            CloseButtonText = "OK"
        };

        _ = await ShowDialogSerializedAsync(dialog);
    }

    private async Task<RunnableChoice> PromptRunnableOpenChoiceAsync(string fileName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _getXamlRoot(),
            Title = "Runnable File Detected",
            Content = new TextBlock
            {
                Text = $"'{fileName}' is runnable. Do you want to run it, or just open its extracted temp folder?",
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = "Run",
            SecondaryButtonText = "Open Temp Folder",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await ShowDialogSerializedAsync(dialog);
        return result switch
        {
            ContentDialogResult.Primary => RunnableChoice.Run,
            ContentDialogResult.Secondary => RunnableChoice.OpenTempFolder,
            _ => RunnableChoice.Cancel
        };
    }

    private async Task<ContentDialogResult> ShowDialogSerializedAsync(ContentDialog dialog)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    private enum RunnableChoice
    {
        Run,
        OpenTempFolder,
        Cancel
    }
}
