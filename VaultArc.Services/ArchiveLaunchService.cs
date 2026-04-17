using System.Diagnostics;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Services;

public sealed class ArchiveLaunchService(
    IArchiveService archiveService,
    ITempSessionService tempSessionService,
    IFileTypeClassificationService fileTypeClassificationService,
    IArchiveSecurityService archiveSecurityService) : IArchiveLaunchService
{
    public async Task<OperationResult<ArchiveLaunchResult>> LaunchEntryAsync(
        ArchiveOpenRequest request,
        ArchiveItem entry,
        ArchiveLaunchMode mode,
        CancellationToken cancellationToken)
    {
        if (entry.IsDirectory)
        {
            return OperationResult<ArchiveLaunchResult>.Failure("launch.folder_not_supported", "Folders cannot be launched.");
        }

        var classification = fileTypeClassificationService.Classify(entry.FullPath, entry.IsDirectory);
        if (mode == ArchiveLaunchMode.Run && !classification.IsRunnable)
        {
            return OperationResult<ArchiveLaunchResult>.Failure("launch.not_runnable", "The selected entry is not runnable.");
        }

        if (mode == ArchiveLaunchMode.Open && !classification.IsOpenable)
        {
            return OperationResult<ArchiveLaunchResult>.Failure("launch.not_openable", "The selected entry cannot be opened directly.");
        }

        var sessionResult = await tempSessionService.CreateSessionAsync(
            request.ArchivePath,
            sessionReason: mode.ToString(),
            isPinned: false,
            cleanupPolicy: SessionCleanupPolicy.Auto,
            cancellationToken).ConfigureAwait(false);

        if (sessionResult.IsFailure || sessionResult.Value is null)
        {
            return OperationResult<ArchiveLaunchResult>.Failure(
                sessionResult.Error?.Code ?? "session.create_failed",
                sessionResult.Error?.Message ?? "Failed to create launch session.",
                sessionResult.Error?.Exception);
        }

        var session = sessionResult.Value;
        var extractionResult = await archiveService.ExtractAsync(
            new ArchiveExtractRequest(request.ArchivePath, session.RootPath, true, request.Password),
            progress: null,
            cancellationToken).ConfigureAwait(false);

        if (extractionResult.IsFailure)
        {
            return OperationResult<ArchiveLaunchResult>.Failure(
                extractionResult.Error?.Code ?? "launch.extract_failed",
                extractionResult.Error?.Message ?? "Failed to extract archive for launch.",
                extractionResult.Error?.Exception);
        }

        var resolvedTarget = archiveSecurityService.ValidateEntryTargetPath(session.RootPath, entry.FullPath);
        if (resolvedTarget.IsFailure || string.IsNullOrWhiteSpace(resolvedTarget.Value))
        {
            return OperationResult<ArchiveLaunchResult>.Failure(
                resolvedTarget.Error?.Code ?? "launch.target_invalid",
                resolvedTarget.Error?.Message ?? "Unable to resolve extracted target path safely.");
        }

        if (!File.Exists(resolvedTarget.Value))
        {
            return OperationResult<ArchiveLaunchResult>.Failure(
                "launch.target_missing",
                "The extracted target file could not be found.");
        }

        if (mode == ArchiveLaunchMode.Open && classification.IsRunnable)
        {
            return OperationResult<ArchiveLaunchResult>.Success(
                new ArchiveLaunchResult(session, resolvedTarget.Value, classification, null));
        }

        var launchResult = await StartProcessAsync(
            resolvedTarget.Value,
            classification,
            mode,
            session,
            cancellationToken).ConfigureAwait(false);

        return launchResult;
    }

    private async Task<OperationResult<ArchiveLaunchResult>> StartProcessAsync(
        string extractedTargetPath,
        ArchiveEntryClassification classification,
        ArchiveLaunchMode mode,
        TempSessionInfo session,
        CancellationToken cancellationToken)
    {
        try
        {
            var extension = Path.GetExtension(extractedTargetPath).ToLowerInvariant();
            ProcessStartInfo psi;

            if (mode == ArchiveLaunchMode.Run && extension == ".ps1")
            {
                psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-File \"{extractedTargetPath}\"",
                    WorkingDirectory = Path.GetDirectoryName(extractedTargetPath),
                    UseShellExecute = true
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = extractedTargetPath,
                    WorkingDirectory = Path.GetDirectoryName(extractedTargetPath),
                    UseShellExecute = true
                };
            }

            var process = Process.Start(psi);
            int? pid = process?.Id;
            if (pid.HasValue)
            {
                await tempSessionService.RegisterProcessAsync(session.SessionId, pid.Value, cancellationToken).ConfigureAwait(false);
            }

            return OperationResult<ArchiveLaunchResult>.Success(
                new ArchiveLaunchResult(session, extractedTargetPath, classification, pid));
        }
        catch (Exception ex)
        {
            return OperationResult<ArchiveLaunchResult>.Failure(
                "launch.start_failed",
                "The selected program was extracted, but launching it failed.",
                ex);
        }
    }
}
