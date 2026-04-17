namespace VaultArc.Models;

public enum VaultArcHashAlgorithm
{
    Md5,
    Sha256,
    Sha512
}

public enum CompressionPresetKind
{
    Fast,
    Balanced,
    MaximumCompression,
    EncryptedTransfer,
    BackupArchive,
    SourceCodeBundle
}

public enum ArcEncryptionProfileKind
{
    XChaCha20Argon2id,
    AesGcmPbkdf2
}

public enum JobState
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record ArchiveItem(
    string FullPath,
    string Name,
    long Size,
    bool IsDirectory,
    DateTimeOffset? ModifiedUtc,
    long? CompressedSize = null,
    string? ItemType = null);

public sealed record ArchiveSummary(
    string ArchivePath,
    IReadOnlyList<ArchiveItem> Items,
    long TotalUncompressedBytes);

public sealed record ArchiveBreadcrumb(string DisplayName, string FolderPath);

public sealed record ArchiveOpenRequest(string ArchivePath, string? Password = null);

public sealed record ArchiveExtractRequest(
    string ArchivePath,
    string DestinationDirectory,
    bool OverwriteExisting,
    string? Password = null);

public sealed record ArchiveCreateRequest(
    string DestinationArchivePath,
    IReadOnlyList<string> InputPaths,
    CompressionPresetKind Preset,
    string? Password = null,
    ArcEncryptionProfileKind EncryptionProfile = ArcEncryptionProfileKind.XChaCha20Argon2id);

public sealed record ArchivePreviewResult(string EntryPath, string MimeType, byte[] Data, bool IsTruncated = false);

public sealed record ArchiveEditSession(
    string ArchivePath,
    string EntryPath,
    string TempFilePath,
    string TempSessionDirectory,
    string OriginalHash,
    bool IsWriteBackSupported,
    string? Password = null);

public enum ArchiveEntryKind
{
    Folder,
    Openable,
    Runnable,
    DangerousScript,
    Unknown
}

public enum ArchiveLaunchMode
{
    Open,
    Run
}

public enum SessionCleanupPolicy
{
    Auto,
    KeepPinned
}

public sealed record ArchiveEntryClassification(
    string EntryPath,
    ArchiveEntryKind Kind,
    bool RequiresStrongWarning,
    bool IsRunnable,
    bool IsOpenable);

public sealed record TempSessionInfo(
    string SessionId,
    string ArchivePath,
    string RootPath,
    DateTimeOffset CreatedUtc,
    DateTimeOffset LastAccessUtc,
    bool IsPinned,
    SessionCleanupPolicy CleanupPolicy,
    IReadOnlyList<int> ProcessIds);

public sealed record ArchiveLaunchResult(
    TempSessionInfo Session,
    string ExtractedTargetPath,
    ArchiveEntryClassification Classification,
    int? ProcessId);

public sealed record HashReportItem(
    string FilePath,
    VaultArcHashAlgorithm Algorithm,
    string HashHex,
    long FileSizeBytes);

public sealed record HashComparisonResult(
    bool Matches,
    string LeftHash,
    string RightHash,
    VaultArcHashAlgorithm Algorithm);

public sealed record VaultArcJob(
    Guid JobId,
    string Title,
    DateTimeOffset QueuedAtUtc,
    JobState State,
    double ProgressPercent,
    string StatusMessage,
    TimeSpan Elapsed,
    TimeSpan? EstimatedRemaining);

public sealed record AppSettings(
    bool IsDarkTheme,
    bool ConfirmOverwrite,
    bool SafeExtractionMode,
    int MaxConcurrentJobs,
    bool UseClassicLayout = false,
    bool FollowSystemTheme = true,
    bool ShellContextMenuEnabled = false,
    ExtractionPolicyKind ExtractionPolicyKind = ExtractionPolicyKind.Standard,
    bool CheckForUpdates = true,
    bool SendCrashReports = false);

public sealed record SecretFinding(string EntryPath, int LineNumber, string PatternName, string Snippet);
public sealed record SecretScanReport(IReadOnlyList<SecretFinding> Findings, int FilesScanned, TimeSpan Elapsed);

public sealed record DuplicateGroup(string Hash, long FileSize, IReadOnlyList<string> Paths);
public sealed record DuplicateReport(IReadOnlyList<DuplicateGroup> Groups, long TotalWastedBytes, int TotalFilesScanned);

public enum DiffEntryKind { Added, Removed, Modified, Unchanged }
public sealed record DiffEntry(string Path, DiffEntryKind Kind, long? LeftSize, long? RightSize);
public sealed record ArchiveDiffResult(
    IReadOnlyList<DiffEntry> Entries,
    int AddedCount, int RemovedCount, int ModifiedCount, int UnchangedCount);

public sealed record ExtractionReportEntry(string EntryPath, bool Succeeded, string? ErrorMessage);
public sealed record ExtractionReport(IReadOnlyList<ExtractionReportEntry> Entries, int SucceededCount, int FailedCount);

public sealed record IntegrityReportEntry(string EntryPath, bool IsValid, string? ErrorMessage);
public sealed record IntegrityReport(IReadOnlyList<IntegrityReportEntry> Entries, int ValidCount, int InvalidCount);

public enum ExtractionPolicyKind { Permissive, Standard, Strict, Custom }

public sealed record ExtractionPolicy(
    ExtractionPolicyKind Kind,
    bool BlockExecutables = false,
    int MaxFileSizeMB = 0,
    bool BlockHiddenFiles = false,
    string? AllowedExtensions = null)
{
    public static ExtractionPolicy Permissive => new(ExtractionPolicyKind.Permissive);
    public static ExtractionPolicy Standard => new(ExtractionPolicyKind.Standard, BlockExecutables: false, MaxFileSizeMB: 2048);
    public static ExtractionPolicy Strict => new(ExtractionPolicyKind.Strict, BlockExecutables: true, MaxFileSizeMB: 512, BlockHiddenFiles: true);
}
