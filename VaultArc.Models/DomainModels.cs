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

public sealed record ArchivePreviewResult(string EntryPath, string MimeType, byte[] Data);

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
    bool ShellContextMenuEnabled = false);
