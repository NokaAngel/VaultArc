using VaultArc.Models;

namespace VaultArc.Core;

public sealed record OperationError(string Code, string Message, Exception? Exception = null);

public sealed class OperationResult
{
    private OperationResult(bool isSuccess, OperationError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public OperationError? Error { get; }

    public static OperationResult Success() => new(true, null);

    public static OperationResult Failure(string code, string message, Exception? exception = null) =>
        new(false, new OperationError(code, message, exception));
}

public sealed class OperationResult<T>
{
    private OperationResult(T? value, bool isSuccess, OperationError? error)
    {
        Value = value;
        IsSuccess = isSuccess;
        Error = error;
    }

    public T? Value { get; }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public OperationError? Error { get; }

    public static OperationResult<T> Success(T value) => new(value, true, null);

    public static OperationResult<T> Failure(string code, string message, Exception? exception = null) =>
        new(default, false, new OperationError(code, message, exception));
}

public sealed record JobProgressUpdate(double Percent, string Message, TimeSpan Elapsed, TimeSpan? Eta = null);

public interface IArchiveService
{
    Task<OperationResult<ArchiveSummary>> OpenAsync(ArchiveOpenRequest request, CancellationToken cancellationToken);
    Task<OperationResult> ExtractAsync(ArchiveExtractRequest request, IProgress<JobProgressUpdate>? progress, CancellationToken cancellationToken);
    Task<OperationResult> CreateArchiveAsync(ArchiveCreateRequest request, IProgress<JobProgressUpdate>? progress, CancellationToken cancellationToken);
    Task<OperationResult<ArchivePreviewResult>> PreviewEntryAsync(ArchiveOpenRequest request, string entryPath, CancellationToken cancellationToken);
    Task<OperationResult> TestIntegrityAsync(ArchiveOpenRequest request, CancellationToken cancellationToken);
    Task<OperationResult<IntegrityReport>> TestIntegrityDetailedAsync(ArchiveOpenRequest request, CancellationToken cancellationToken);
    Task<OperationResult<IntegrityReport>> FastIntegrityScanAsync(ArchiveOpenRequest request, CancellationToken cancellationToken);
    Task<OperationResult<ArchiveEditSession>> CreateEditSessionAsync(ArchiveOpenRequest request, string entryPath, CancellationToken cancellationToken);
    Task<OperationResult> SaveEditedEntryAsync(ArchiveEditSession session, CancellationToken cancellationToken);
    Task<OperationResult> CleanupEditSessionAsync(ArchiveEditSession session);
    Task<OperationResult> RenameEntryAsync(ArchiveOpenRequest request, string entryPath, string newEntryPath, CancellationToken cancellationToken);
    Task<OperationResult> DeleteEntryAsync(ArchiveOpenRequest request, string entryPath, CancellationToken cancellationToken);
}

public interface IArchiveSecurityService
{
    OperationResult<string> ValidateEntryTargetPath(string rootDirectory, string entryPath);
    bool IsSensitiveLocation(string path);
}

public interface IFileTypeClassificationService
{
    ArchiveEntryClassification Classify(string entryPath, bool isDirectory);
    bool IsRunnableExtension(string entryPath);
}

public interface ITempSessionService
{
    Task<OperationResult<TempSessionInfo>> CreateSessionAsync(
        string archivePath,
        string sessionReason,
        bool isPinned,
        SessionCleanupPolicy cleanupPolicy,
        CancellationToken cancellationToken);

    Task<OperationResult> RegisterProcessAsync(string sessionId, int processId, CancellationToken cancellationToken);
    Task<OperationResult> TouchSessionAsync(string sessionId, CancellationToken cancellationToken);
    Task<OperationResult> PinSessionAsync(string sessionId, bool isPinned, CancellationToken cancellationToken);
    Task<OperationResult> OpenSessionFolderAsync(string sessionId, CancellationToken cancellationToken);
    Task<OperationResult<IReadOnlyList<TempSessionInfo>>> GetSessionsAsync(CancellationToken cancellationToken);
    Task<OperationResult> CleanupOldSessionsAsync(TimeSpan maxAge, CancellationToken cancellationToken);
    Task<OperationResult> CleanupSessionAsync(string sessionId, bool force, CancellationToken cancellationToken);
}

public interface IArchiveLaunchService
{
    Task<OperationResult<ArchiveLaunchResult>> LaunchEntryAsync(
        ArchiveOpenRequest request,
        ArchiveItem entry,
        ArchiveLaunchMode mode,
        CancellationToken cancellationToken);
}

public interface IArchiveBrowseService
{
    IReadOnlyList<ArchiveItem> GetChildren(IReadOnlyList<ArchiveItem> allItems, string currentFolderPath);
    string? GetParentFolder(string currentFolderPath);
}

public interface IArchiveNavigationService
{
    string CurrentFolderPath { get; }
    bool CanGoUp { get; }
    void NavigateTo(string folderPath);
    bool NavigateUp();
    void Reset();
}

public interface IHashingService
{
    Task<OperationResult<HashReportItem>> HashFileAsync(string filePath, VaultArcHashAlgorithm algorithm, CancellationToken cancellationToken);
    Task<OperationResult<HashComparisonResult>> CompareFilesAsync(string leftPath, string rightPath, VaultArcHashAlgorithm algorithm, CancellationToken cancellationToken);
    Task<OperationResult<string>> ExportReportAsync(IEnumerable<HashReportItem> items, string outputPath, CancellationToken cancellationToken);
}

public interface IExtractionSafetyService
{
    OperationResult<string> ValidateExtractionTarget(string extractionRoot, string entryPath);
    bool IsSensitiveLocation(string path);
    OperationResult ValidateAgainstPolicy(string entryPath, long entrySize, ExtractionPolicy policy);
}

public interface IRecentArchivesStore
{
    Task<IReadOnlyList<string>> GetRecentAsync(CancellationToken cancellationToken);
    Task AddRecentAsync(string archivePath, CancellationToken cancellationToken);
}

public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public interface IJobQueueService
{
    IReadOnlyCollection<VaultArcJob> Snapshot { get; }
    event EventHandler? QueueChanged;
    Task<Guid> EnqueueAsync(string title, Func<IProgress<JobProgressUpdate>, CancellationToken, Task<OperationResult>> work, CancellationToken cancellationToken);
    bool Cancel(Guid jobId);
}

public interface ISecretScannerService
{
    Task<OperationResult<SecretScanReport>> ScanArchiveAsync(ArchiveOpenRequest request, CancellationToken cancellationToken);
}

public interface IPlatformService
{
    void OpenFolder(string path);
    void OpenFile(string path);
    void ShowNotification(string title, string message);
    bool IsWindows { get; }
    bool IsMacOS { get; }
    bool IsLinux { get; }
}

public interface IDuplicateDetectionService
{
    Task<OperationResult<DuplicateReport>> ScanForDuplicatesAsync(IReadOnlyList<string> inputPaths, CancellationToken cancellationToken);
}

public interface IArchiveDiffService
{
    ArchiveDiffResult ComputeDiff(ArchiveSummary left, ArchiveSummary right);
}
