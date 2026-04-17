using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Services;

public sealed class VaultArcFacade(
    IArchiveService archiveService,
    IHashingService hashingService,
    IJobQueueService jobQueueService,
    IRecentArchivesStore recentArchivesStore,
    IAppSettingsStore appSettingsStore,
    IArchiveLaunchService archiveLaunchService,
    ITempSessionService tempSessionService)
{
    public IJobQueueService JobQueue => jobQueueService;

    public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken) =>
        appSettingsStore.LoadAsync(cancellationToken);

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken) =>
        appSettingsStore.SaveAsync(settings, cancellationToken);

    public Task<IReadOnlyList<string>> GetRecentArchivesAsync(CancellationToken cancellationToken) =>
        recentArchivesStore.GetRecentAsync(cancellationToken);

    public async Task<OperationResult<ArchiveSummary>> OpenArchiveAsync(ArchiveOpenRequest request, CancellationToken cancellationToken)
    {
        var result = await archiveService.OpenAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await recentArchivesStore.AddRecentAsync(request.ArchivePath, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public Task<OperationResult<ArchivePreviewResult>> PreviewEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken) =>
        archiveService.PreviewEntryAsync(request, entryPath, cancellationToken);

    public Task<OperationResult<ArchiveEditSession>> CreateEditSessionAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken) =>
        archiveService.CreateEditSessionAsync(request, entryPath, cancellationToken);

    public Task<OperationResult> SaveEditedEntryAsync(ArchiveEditSession session, CancellationToken cancellationToken) =>
        archiveService.SaveEditedEntryAsync(session, cancellationToken);

    public Task<OperationResult> CleanupEditSessionAsync(ArchiveEditSession session) =>
        archiveService.CleanupEditSessionAsync(session);

    public Task<OperationResult> RenameEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        string newEntryPath,
        CancellationToken cancellationToken) =>
        archiveService.RenameEntryAsync(request, entryPath, newEntryPath, cancellationToken);

    public Task<OperationResult> DeleteEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken) =>
        archiveService.DeleteEntryAsync(request, entryPath, cancellationToken);

    public Task<OperationResult> QueueExtractionAsync(ArchiveExtractRequest request, CancellationToken cancellationToken) =>
        QueueArchiveWorkAsync(
            $"Extract: {Path.GetFileName(request.ArchivePath)}",
            (progress, ct) => archiveService.ExtractAsync(request, progress, ct),
            cancellationToken);

    public Task<OperationResult> QueueArchiveCreationAsync(ArchiveCreateRequest request, CancellationToken cancellationToken) =>
        QueueArchiveWorkAsync(
            $"Create: {Path.GetFileName(request.DestinationArchivePath)}",
            (progress, ct) => archiveService.CreateArchiveAsync(request, progress, ct),
            cancellationToken);

    public Task<OperationResult> QueueIntegrityTestAsync(ArchiveOpenRequest request, CancellationToken cancellationToken) =>
        QueueArchiveWorkAsync(
            $"Integrity Test: {Path.GetFileName(request.ArchivePath)}",
            async (_, ct) => await archiveService.TestIntegrityAsync(request, ct).ConfigureAwait(false),
            cancellationToken);

    public Task<OperationResult<HashReportItem>> HashFileAsync(
        string filePath,
        VaultArcHashAlgorithm algorithm,
        CancellationToken cancellationToken) =>
        hashingService.HashFileAsync(filePath, algorithm, cancellationToken);

    public Task<OperationResult<HashComparisonResult>> CompareHashesAsync(
        string leftPath,
        string rightPath,
        VaultArcHashAlgorithm algorithm,
        CancellationToken cancellationToken) =>
        hashingService.CompareFilesAsync(leftPath, rightPath, algorithm, cancellationToken);

    public Task<OperationResult<string>> ExportHashReportAsync(
        IEnumerable<HashReportItem> items,
        string outputPath,
        CancellationToken cancellationToken) =>
        hashingService.ExportReportAsync(items, outputPath, cancellationToken);

    public Task<OperationResult<ArchiveLaunchResult>> LaunchEntryAsync(
        ArchiveOpenRequest request,
        ArchiveItem entry,
        ArchiveLaunchMode mode,
        CancellationToken cancellationToken) =>
        archiveLaunchService.LaunchEntryAsync(request, entry, mode, cancellationToken);

    public Task<OperationResult> OpenSessionFolderAsync(string sessionId, CancellationToken cancellationToken) =>
        tempSessionService.OpenSessionFolderAsync(sessionId, cancellationToken);

    private async Task<OperationResult> QueueArchiveWorkAsync(
        string title,
        Func<IProgress<JobProgressUpdate>, CancellationToken, Task<OperationResult>> work,
        CancellationToken cancellationToken)
    {
        await jobQueueService.EnqueueAsync(title, work, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success();
    }
}
