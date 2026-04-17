using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using VaultArc.Core;
using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.App.ViewModels;

public partial class OpenArchiveViewModel(
    VaultArcFacade facade,
    IArchiveBrowseService archiveBrowseService,
    IArchiveNavigationService archiveNavigationService,
    IFileTypeClassificationService fileTypeClassificationService) : ViewModelBase
{
    [ObservableProperty]
    private string _archivePath = string.Empty;

    [ObservableProperty]
    private string _archiveIconPath = "ms-appx:///Assets/VaultArcLogo.png";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _extractDestination = string.Empty;

    [ObservableProperty]
    private bool _extractOverwrite;

    private string? _cachedPassword;
    private string? _cachedPasswordArchive;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ArchiveItem? _selectedItem;

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private bool _showExecutablesOnly;

    [ObservableProperty]
    private string _currentFolderPath = "\\";

    [ObservableProperty]
    private string _breadcrumbPath = "\\";

    [ObservableProperty]
    private bool _canRunSelected;

    [ObservableProperty]
    private bool _canOpenSelected;

    [ObservableProperty]
    private bool _canNavigateUp;

    [ObservableProperty]
    private string _lastSessionId = string.Empty;

    [ObservableProperty]
    private string _lastExtractedTargetPath = string.Empty;

    [ObservableProperty]
    private string _selectedDisplayName = "-";

    [ObservableProperty]
    private string _selectedTypeLabel = "-";

    [ObservableProperty]
    private string _selectedSizeLabel = "-";

    [ObservableProperty]
    private string _selectedModifiedLabel = "-";

    [ObservableProperty]
    private string _selectedFullPath = "-";

    public ObservableCollection<ArchiveItem> ArchiveItems { get; } = [];
    public ObservableCollection<ArchiveItem> VisibleItems { get; } = [];
    public ObservableCollection<ArchiveBreadcrumb> Breadcrumbs { get; } = [];

    public IEnumerable<ArchiveItem> FilteredItems => VisibleItems.Where(item =>
        (!ShowExecutablesOnly || IsExecutablePath(item.FullPath)) &&
        (string.IsNullOrWhiteSpace(SearchText) || item.FullPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

    public string StatusBarSummary => BuildStatusBarSummary();

    public int ExecutableCount => ArchiveItems.Count(item => IsExecutablePath(item.FullPath));
    public int TotalEntryCount => ArchiveItems.Count;
    public bool HasArchiveLoaded => ArchiveItems.Count > 0;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredItems));
    }

    partial void OnShowExecutablesOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(FilteredItems));
        UpdateSelectionCapabilities();
    }

    partial void OnSelectedItemChanged(ArchiveItem? value)
    {
        UpdateSelectionCapabilities();
        RefreshSelectedDetails(value);
        OnPropertyChanged(nameof(StatusBarSummary));
    }

    partial void OnArchivePathChanged(string value)
    {
        ArchiveIconPath = GetArchiveIconForPath(value);
        if (!string.Equals(value, _cachedPasswordArchive, StringComparison.OrdinalIgnoreCase))
        {
            ClearCachedPassword();
        }
    }

    public void CachePassword(string password)
    {
        _cachedPassword = password;
        _cachedPasswordArchive = ArchivePath;
    }

    public string? GetCachedPassword() =>
        string.Equals(ArchivePath, _cachedPasswordArchive, StringComparison.OrdinalIgnoreCase)
            ? _cachedPassword
            : null;

    public void ClearCachedPassword()
    {
        _cachedPassword = null;
        _cachedPasswordArchive = null;
    }

    public string? EffectivePassword =>
        !string.IsNullOrWhiteSpace(Password) ? Password : GetCachedPassword();

    public async Task<OperationResult<ArchiveSummary>> OpenAsync(string archivePath)
    {
        ArchivePath = archivePath;
        IsBusy = true;

        try
        {
            var request = new ArchiveOpenRequest(archivePath, EffectivePassword);
            var result = await facade.OpenArchiveAsync(request, CancellationToken.None);
            if (result.IsFailure || result.Value is null)
            {
                StatusMessage = result.Error?.Message ?? "Failed to open archive.";
                return OperationResult<ArchiveSummary>.Failure(
                    result.Error?.Code ?? "archive.open_failed",
                    result.Error?.Message ?? "Failed to open archive.",
                    result.Error?.Exception);
            }

            ArchiveItems.Clear();
            foreach (var item in result.Value.Items)
            {
                var classification = fileTypeClassificationService.Classify(item.FullPath, item.IsDirectory);
                var typeLabel = item.IsDirectory
                    ? "Folder"
                    : classification.Kind switch
                    {
                        ArchiveEntryKind.Runnable => "Runnable",
                        ArchiveEntryKind.DangerousScript => "Script",
                        ArchiveEntryKind.Openable => "File",
                        _ => Path.GetExtension(item.FullPath).TrimStart('.').ToUpperInvariant()
                    };

                ArchiveItems.Add(item with { ItemType = typeLabel });
            }

            archiveNavigationService.Reset();
            RefreshVisibleItems();
            OnPropertyChanged(nameof(ExecutableCount));
            OnPropertyChanged(nameof(TotalEntryCount));
            OnPropertyChanged(nameof(HasArchiveLoaded));
            OnPropertyChanged(nameof(StatusBarSummary));
            StatusMessage = $"Loaded {ArchiveItems.Count} entries ({ExecutableCount} executable).";
            return OperationResult<ArchiveSummary>.Success(result.Value);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<OperationResult<ArchivePreviewResult>> PreviewSelectedAsync()
    {
        if (SelectedItem is null || SelectedItem.IsDirectory)
        {
            return OperationResult<ArchivePreviewResult>.Failure("archive.no_entry", "Select a file entry to preview.");
        }

        var request = new ArchiveOpenRequest(ArchivePath, EffectivePassword);
        var result = await facade.PreviewEntryAsync(request, SelectedItem.FullPath, CancellationToken.None);
        if (result.IsFailure || result.Value is null)
        {
            PreviewText = result.Error?.Message ?? "Preview failed.";
            return OperationResult<ArchivePreviewResult>.Failure(
                result.Error?.Code ?? "archive.preview_failed",
                result.Error?.Message ?? "Preview failed.",
                result.Error?.Exception);
        }

        if (result.Value.MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            PreviewText = System.Text.Encoding.UTF8.GetString(result.Value.Data);
        }
        else
        {
            PreviewText = $"Preview available for {result.Value.MimeType}, size {result.Value.Data.Length} bytes.";
        }

        return OperationResult<ArchivePreviewResult>.Success(result.Value);
    }

    public void RefreshVisibleItems()
    {
        var children = archiveBrowseService.GetChildren(ArchiveItems.ToList(), archiveNavigationService.CurrentFolderPath);
        VisibleItems.Clear();
        foreach (var child in children)
        {
            VisibleItems.Add(child);
        }

        CurrentFolderPath = string.IsNullOrWhiteSpace(archiveNavigationService.CurrentFolderPath)
            ? "\\"
            : $"\\{archiveNavigationService.CurrentFolderPath.Replace('/', '\\')}";
        BreadcrumbPath = CurrentFolderPath;
        RefreshBreadcrumbs();

        CanNavigateUp = archiveNavigationService.CanGoUp;
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(StatusBarSummary));
        UpdateSelectionCapabilities();
    }

    public void NavigateIntoSelectedFolder()
    {
        if (SelectedItem is null || !SelectedItem.IsDirectory)
        {
            return;
        }

        archiveNavigationService.NavigateTo(SelectedItem.FullPath);
        RefreshVisibleItems();
    }

    public void NavigateUp()
    {
        if (archiveNavigationService.NavigateUp())
        {
            RefreshVisibleItems();
        }
    }

    public void NavigateToRoot()
    {
        archiveNavigationService.Reset();
        RefreshVisibleItems();
    }

    public void NavigateToBreadcrumb(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            NavigateToRoot();
            return;
        }

        archiveNavigationService.NavigateTo(folderPath.Replace('\\', '/'));
        RefreshVisibleItems();
    }

    public async Task<OperationResult<ArchiveLaunchResult>> OpenSelectedAsync()
    {
        if (SelectedItem is null)
        {
            return OperationResult<ArchiveLaunchResult>.Failure("archive.no_selection", "Select an entry first.");
        }

        if (SelectedItem.IsDirectory)
        {
            NavigateIntoSelectedFolder();
            return OperationResult<ArchiveLaunchResult>.Failure("archive.folder_navigation", "Navigated into folder.");
        }

        var request = new ArchiveOpenRequest(ArchivePath, EffectivePassword);
        var result = await facade.LaunchEntryAsync(request, SelectedItem, ArchiveLaunchMode.Open, CancellationToken.None);
        if (result.IsSuccess && result.Value is not null)
        {
            LastSessionId = result.Value.Session.SessionId;
            LastExtractedTargetPath = result.Value.ExtractedTargetPath;
            StatusMessage = $"Opened {SelectedItem.Name} from archive temp session.";
        }
        else
        {
            StatusMessage = result.Error?.Message ?? "Failed to open selected entry.";
        }

        return result;
    }

    public async Task<OperationResult<ArchiveLaunchResult>> RunSelectedAsync()
    {
        if (SelectedItem is null)
        {
            return OperationResult<ArchiveLaunchResult>.Failure("archive.no_selection", "Select an entry first.");
        }

        var request = new ArchiveOpenRequest(ArchivePath, EffectivePassword);
        var result = await facade.LaunchEntryAsync(request, SelectedItem, ArchiveLaunchMode.Run, CancellationToken.None);
        if (result.IsSuccess && result.Value is not null)
        {
            LastSessionId = result.Value.Session.SessionId;
            LastExtractedTargetPath = result.Value.ExtractedTargetPath;
            StatusMessage = $"Launched {SelectedItem.Name} from extracted session.";
        }
        else
        {
            StatusMessage = result.Error?.Message ?? "Failed to run selected entry.";
        }

        return result;
    }

    public async Task<OperationResult> OpenLastSessionFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(LastSessionId))
        {
            return OperationResult.Failure("session.none", "No temp session available yet.");
        }

        return await facade.OpenSessionFolderAsync(LastSessionId, CancellationToken.None);
    }

    public async Task<OperationResult> QueueIntegrityTestAsync()
    {
        var request = new ArchiveOpenRequest(ArchivePath, EffectivePassword);
        var result = await facade.QueueIntegrityTestAsync(request, CancellationToken.None);
        StatusMessage = result.IsSuccess ? "Integrity test queued." : result.Error?.Message ?? "Failed to queue integrity test.";
        return result;
    }

    public async Task<OperationResult<ArchiveEditSession>> CreateEditSessionForSelectedAsync()
    {
        if (SelectedItem is null || SelectedItem.IsDirectory)
        {
            return OperationResult<ArchiveEditSession>.Failure("archive.no_entry", "Select a file entry first.");
        }

        var request = new ArchiveOpenRequest(ArchivePath, EffectivePassword);
        var result = await facade.CreateEditSessionAsync(request, SelectedItem.FullPath, CancellationToken.None);
        StatusMessage = result.IsSuccess
            ? "Opened entry for external edit."
            : result.Error?.Message ?? "Failed to open entry for editing.";
        return result;
    }

    public async Task<bool> HasEntryChangedAsync(ArchiveEditSession session)
    {
        if (!File.Exists(session.TempFilePath))
        {
            return false;
        }

        await using var stream = File.OpenRead(session.TempFilePath);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, CancellationToken.None));
        return !string.Equals(hash, session.OriginalHash, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<OperationResult> SaveEditedSessionAsync(ArchiveEditSession session)
    {
        var result = await facade.SaveEditedEntryAsync(session, CancellationToken.None);
        StatusMessage = result.IsSuccess
            ? "Saved changes back into archive."
            : result.Error?.Message ?? "Failed to save changes into archive.";
        return result;
    }

    public Task<OperationResult> CleanupEditedSessionAsync(ArchiveEditSession session) =>
        facade.CleanupEditSessionAsync(session);

    public async Task<OperationResult> RenameSelectedAsync(string newName)
    {
        if (SelectedItem is null || string.IsNullOrWhiteSpace(newName))
        {
            return OperationResult.Failure("archive.rename_invalid", "Provide a valid new name.");
        }

        var folder = Path.GetDirectoryName(SelectedItem.FullPath)?.Replace('\\', '/');
        var targetPath = string.IsNullOrWhiteSpace(folder)
            ? newName
            : $"{folder}/{newName}".Replace('\\', '/');

        var request = new ArchiveOpenRequest(ArchivePath, EffectivePassword);
        var result = await facade.RenameEntryAsync(request, SelectedItem.FullPath, targetPath, CancellationToken.None);
        if (result.IsSuccess)
        {
            await OpenAsync(ArchivePath);
            StatusMessage = "Entry renamed.";
        }
        else
        {
            StatusMessage = result.Error?.Message ?? "Rename failed.";
        }

        return result;
    }

    public async Task<OperationResult> DeleteSelectedAsync()
    {
        if (SelectedItem is null)
        {
            return OperationResult.Failure("archive.delete_invalid", "Select an entry to delete.");
        }

        var request = new ArchiveOpenRequest(ArchivePath, EffectivePassword);
        var result = await facade.DeleteEntryAsync(request, SelectedItem.FullPath, CancellationToken.None);
        if (result.IsSuccess)
        {
            await OpenAsync(ArchivePath);
            StatusMessage = "Entry deleted.";
        }
        else
        {
            StatusMessage = result.Error?.Message ?? "Delete failed.";
        }

        return result;
    }

    public async Task<OperationResult> QueueExtractSelectedAsync(IReadOnlyList<ArchiveItem> items)
    {
        if (string.IsNullOrWhiteSpace(ArchivePath) || string.IsNullOrWhiteSpace(ExtractDestination))
        {
            StatusMessage = "Set archive path and destination folder.";
            return OperationResult.Failure("extract.missing_paths", "Archive or destination path is empty.");
        }

        if (items.Count == 0)
        {
            StatusMessage = "Select entries to extract.";
            return OperationResult.Failure("extract.no_selection", "No entries selected.");
        }

        var request = new ArchiveExtractRequest(ArchivePath, ExtractDestination, ExtractOverwrite, EffectivePassword);
        var result = await facade.QueueExtractionAsync(request, CancellationToken.None);
        StatusMessage = result.IsSuccess
            ? $"Extraction queued ({items.Count} entries)."
            : result.Error?.Message ?? "Failed to queue extraction.";
        return result;
    }

    public async Task<OperationResult> QueueExtractAllAsync()
    {
        if (string.IsNullOrWhiteSpace(ArchivePath) || string.IsNullOrWhiteSpace(ExtractDestination))
        {
            StatusMessage = "Set archive path and destination folder.";
            return OperationResult.Failure("extract.missing_paths", "Archive or destination path is empty.");
        }

        var request = new ArchiveExtractRequest(ArchivePath, ExtractDestination, ExtractOverwrite, EffectivePassword);
        var result = await facade.QueueExtractionAsync(request, CancellationToken.None);
        StatusMessage = result.IsSuccess ? "Extraction queued." : result.Error?.Message ?? "Failed to queue extraction.";
        return result;
    }

    public bool IsSelectedItemExecutable() =>
        SelectedItem is not null && fileTypeClassificationService.IsRunnableExtension(SelectedItem.FullPath);

    private static bool IsExecutablePath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".exe" or ".com" or ".bat" or ".cmd" or ".msi" or ".ps1" or ".vbs" or ".js";
    }

    private void UpdateSelectionCapabilities()
    {
        if (SelectedItem is null)
        {
            CanOpenSelected = false;
            CanRunSelected = false;
            return;
        }

        if (SelectedItem.IsDirectory)
        {
            CanOpenSelected = true;
            CanRunSelected = false;
            return;
        }

        var classification = fileTypeClassificationService.Classify(SelectedItem.FullPath, SelectedItem.IsDirectory);
        CanOpenSelected = classification.IsOpenable;
        CanRunSelected = classification.IsRunnable;
    }

    private void RefreshBreadcrumbs()
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new ArchiveBreadcrumb("\\", string.Empty));

        var current = archiveNavigationService.CurrentFolderPath;
        if (string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        var segments = current
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var running = string.Empty;
        foreach (var segment in segments)
        {
            running = string.IsNullOrWhiteSpace(running) ? segment : $"{running}/{segment}";
            Breadcrumbs.Add(new ArchiveBreadcrumb(segment, running));
        }
    }

    private void RefreshSelectedDetails(ArchiveItem? item)
    {
        if (item is null)
        {
            SelectedDisplayName = "-";
            SelectedTypeLabel = "-";
            SelectedSizeLabel = "-";
            SelectedModifiedLabel = "-";
            SelectedFullPath = "-";
            return;
        }

        SelectedDisplayName = item.Name;
        SelectedTypeLabel = item.ItemType ?? (item.IsDirectory ? "Folder" : "File");
        SelectedSizeLabel = item.IsDirectory ? "-" : $"{item.Size:N0} bytes";
        SelectedModifiedLabel = item.ModifiedUtc?.ToString("u") ?? "-";
        SelectedFullPath = item.FullPath;
    }

    private string BuildStatusBarSummary()
    {
        var visibleFileCount = FilteredItems.Count(item => !item.IsDirectory);
        var visibleFolderCount = FilteredItems.Count(item => item.IsDirectory);
        var visibleBytes = FilteredItems.Where(item => !item.IsDirectory).Sum(item => item.Size);
        return $"{visibleFolderCount} folders, {visibleFileCount} files | {FormatBytes(visibleBytes)}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.##} {units[unit]}";
    }

    private static string GetArchiveIconForPath(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".arc" or ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".xz" or ".tgz" or ".txz"
            ? "ms-appx:///Assets/VaultArcLogo.png"
            : "ms-appx:///Assets/VaultArcLogo.png";
    }
}
