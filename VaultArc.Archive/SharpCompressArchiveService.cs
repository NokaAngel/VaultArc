using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using VaultArc.Archive.Arc;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Archive;

public sealed class SharpCompressArchiveService(IExtractionSafetyService safetyService) : IArchiveService
{
    public async Task<OperationResult<ArchiveSummary>> OpenAsync(ArchiveOpenRequest request, CancellationToken cancellationToken)
    {
        if (ArcArchiveService.IsArcPath(request.ArchivePath))
        {
            return await ArcArchiveService.OpenAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (!File.Exists(request.ArchivePath))
        {
            return OperationResult<ArchiveSummary>.Failure("archive.not_found", $"Archive not found: {request.ArchivePath}");
        }

        try
        {
            using var stream = File.OpenRead(request.ArchivePath);
            using var archive = ArchiveFactory.Open(stream, new ReaderOptions { Password = request.Password });

            var entries = archive.Entries.ToList();
            var isEncrypted = entries.Any(IsEntryEncrypted);
            if (isEncrypted && string.IsNullOrWhiteSpace(request.Password))
            {
                return OperationResult<ArchiveSummary>.Failure(
                    "archive.password_required",
                    "This archive is encrypted. Enter a password to open it.");
            }

            if (isEncrypted && !string.IsNullOrWhiteSpace(request.Password))
            {
                var passwordProbe = await ValidatePasswordProbeAsync(entries, cancellationToken).ConfigureAwait(false);
                if (passwordProbe.IsFailure)
                {
                    return OperationResult<ArchiveSummary>.Failure(
                        passwordProbe.Error?.Code ?? "archive.invalid_password",
                        passwordProbe.Error?.Message ?? "The password appears invalid for this archive.",
                        passwordProbe.Error?.Exception);
                }
            }

            var items = entries
                .Select(entry => new ArchiveItem(
                    entry.Key ?? string.Empty,
                    Path.GetFileName((entry.Key ?? string.Empty).TrimEnd('/', '\\')),
                    entry.Size,
                    entry.IsDirectory,
                    entry.LastModifiedTime,
                    entry.CompressedSize))
                .OrderBy(static item => item.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var totalBytes = items.Where(static i => !i.IsDirectory).Sum(static i => i.Size);
            return OperationResult<ArchiveSummary>.Success(new ArchiveSummary(request.ArchivePath, items, totalBytes));
        }
        catch (global::System.Security.Cryptography.CryptographicException ex)
        {
            return OperationResult<ArchiveSummary>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (InvalidOperationException ex) when (LooksLikePasswordError(ex))
        {
            return OperationResult<ArchiveSummary>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (Exception ex)
        {
            return OperationResult<ArchiveSummary>.Failure("archive.open_failed", "Failed to open archive.", ex);
        }
    }

    public async Task<OperationResult> ExtractAsync(
        ArchiveExtractRequest request,
        IProgress<JobProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        if (ArcArchiveService.IsArcPath(request.ArchivePath))
        {
            return await ArcArchiveService.ExtractAsync(request, safetyService, progress, cancellationToken).ConfigureAwait(false);
        }

        if (!File.Exists(request.ArchivePath))
        {
            return OperationResult.Failure("archive.not_found", $"Archive not found: {request.ArchivePath}");
        }

        if (safetyService.IsSensitiveLocation(request.DestinationDirectory))
        {
            return OperationResult.Failure(
                "security.sensitive_destination",
                $"Extraction destination is sensitive and blocked by default: {request.DestinationDirectory}");
        }

        try
        {
            Directory.CreateDirectory(request.DestinationDirectory);
            using var stream = File.OpenRead(request.ArchivePath);
            using var archive = ArchiveFactory.Open(stream, new ReaderOptions { Password = request.Password });

            var entries = archive.Entries.Where(static entry => !entry.IsDirectory).ToList();
            var totalEntries = entries.Count;
            var startedAt = DateTimeOffset.UtcNow;

            for (var i = 0; i < totalEntries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = entries[i];
                var validatedPath = safetyService.ValidateExtractionTarget(request.DestinationDirectory, entry.Key ?? string.Empty);
                if (validatedPath.IsFailure || validatedPath.Value is null)
                {
                    return OperationResult.Failure(
                        validatedPath.Error?.Code ?? "security.invalid_entry_path",
                        validatedPath.Error?.Message ?? $"Unsafe archive entry: {entry.Key}");
                }

                var targetPath = validatedPath.Value;
                var folder = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                if (File.Exists(targetPath) && !request.OverwriteExisting)
                {
                    return OperationResult.Failure(
                        "archive.overwrite_blocked",
                        $"Target file already exists: {targetPath}");
                }

                using var source = entry.OpenEntryStream();
                await using var destination = File.Create(targetPath);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

                var elapsed = DateTimeOffset.UtcNow - startedAt;
                var percent = totalEntries == 0 ? 100 : ((i + 1) / (double)totalEntries) * 100;
                progress?.Report(new JobProgressUpdate(percent, $"Extracted {entry.Key}", elapsed));
            }

            return OperationResult.Success();
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Failure("job.cancelled", "Extraction cancelled.");
        }
        catch (global::System.Security.Cryptography.CryptographicException ex)
        {
            return OperationResult.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (InvalidOperationException ex) when (LooksLikePasswordError(ex))
        {
            return OperationResult.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("archive.extract_failed", "Failed to extract archive.", ex);
        }
    }

    public async Task<OperationResult> CreateArchiveAsync(
        ArchiveCreateRequest request,
        IProgress<JobProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        if (request.InputPaths.Count == 0)
        {
            return OperationResult.Failure("archive.no_inputs", "At least one input path is required.");
        }

        var format = GetCreateFormat(request.DestinationArchivePath);
        if (format == CreateArchiveFormat.RarUnsupported)
        {
            return OperationResult.Failure(
                "archive.rar_write_unsupported",
                "RAR creation is not supported by the current archive engine. Use .zip, .tar, .tar.gz, .tar.xz, or .arc.");
        }

        if (format == CreateArchiveFormat.Unsupported)
        {
            return OperationResult.Failure(
                "archive.format_unsupported",
                "Unsupported archive extension. Use .zip, .tar, .gz, .xz, .tar.gz, .tar.xz, or .arc.");
        }

        var files = ExpandFiles(request.InputPaths).ToList();
        if (files.Count == 0)
        {
            return OperationResult.Failure("archive.no_files", "No files were found to add into the archive.");
        }

        var outputDirectory = Path.GetDirectoryName(request.DestinationArchivePath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        try
        {
            return format switch
            {
                CreateArchiveFormat.Zip => await CreateZipArchiveAsync(request, files, progress, cancellationToken).ConfigureAwait(false),
                CreateArchiveFormat.SevenZip => OperationResult.Failure(
                    "archive.7z_write_unsupported",
                    "7z creation is not supported by the current archive writer. Use .zip, .tar, .tar.gz, .tar.xz, or .arc."),
                CreateArchiveFormat.Tar => CreateTarArchive(request, files, CompressionType.None, progress, cancellationToken),
                CreateArchiveFormat.GZip or CreateArchiveFormat.TarGZip => CreateTarArchive(request, files, CompressionType.GZip, progress, cancellationToken),
                CreateArchiveFormat.Xz or CreateArchiveFormat.TarXz => CreateTarArchive(request, files, CompressionType.Xz, progress, cancellationToken),
                CreateArchiveFormat.Arc => await ArcArchiveService.CreateAsync(request, files, progress, cancellationToken).ConfigureAwait(false),
                _ => OperationResult.Failure("archive.format_unsupported", "Unsupported archive extension.")
            };
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Failure("job.cancelled", "Compression cancelled.");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("archive.create_failed", "Failed to create archive.", ex);
        }
    }

    private static async Task<OperationResult> CreateZipArchiveAsync(
        ArchiveCreateRequest request,
        IReadOnlyList<(string FullPath, string RelativePath)> files,
        IProgress<JobProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        var compression = request.Preset == CompressionPresetKind.MaximumCompression
            ? CompressionLevel.SmallestSize
            : request.Preset == CompressionPresetKind.Fast
                ? CompressionLevel.Fastest
                : CompressionLevel.Optimal;

        var startedAt = DateTimeOffset.UtcNow;
        await using var output = File.Create(request.DestinationArchivePath);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false);

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            var entry = zip.CreateEntry(file.RelativePath, compression);
            await using var source = File.OpenRead(file.FullPath);
            await using var destination = entry.Open();
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            var percent = ((i + 1) / (double)files.Count) * 100d;
            progress?.Report(new JobProgressUpdate(percent, $"Compressed {file.RelativePath}", DateTimeOffset.UtcNow - startedAt));
        }

        return OperationResult.Success();
    }

    private static Task<OperationResult> CreateSevenZipArchiveAsync(
        ArchiveCreateRequest request,
        IReadOnlyList<(string FullPath, string RelativePath)> files,
        IProgress<JobProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        var compression = request.Preset == CompressionPresetKind.Fast
            ? CompressionType.Deflate
            : CompressionType.LZMA;

        var startedAt = DateTimeOffset.UtcNow;
        using var output = File.Create(request.DestinationArchivePath);
        using var writer = WriterFactory.Open(output, ArchiveType.SevenZip, new WriterOptions(compression));

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            writer.Write(file.RelativePath, file.FullPath);
            var percent = ((i + 1) / (double)files.Count) * 100d;
            progress?.Report(new JobProgressUpdate(percent, $"Compressed {file.RelativePath}", DateTimeOffset.UtcNow - startedAt));
        }

        return Task.FromResult(OperationResult.Success());
    }

    private static OperationResult CreateTarArchive(
        ArchiveCreateRequest request,
        IReadOnlyList<(string FullPath, string RelativePath)> files,
        CompressionType compression,
        IProgress<JobProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        using var output = File.Create(request.DestinationArchivePath);
        using var writer = WriterFactory.Open(output, ArchiveType.Tar, new WriterOptions(compression));

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            writer.Write(file.RelativePath, file.FullPath);
            var percent = ((i + 1) / (double)files.Count) * 100d;
            progress?.Report(new JobProgressUpdate(percent, $"Compressed {file.RelativePath}", DateTimeOffset.UtcNow - startedAt));
        }

        return OperationResult.Success();
    }

    private static CreateArchiveFormat GetCreateFormat(string path)
    {
        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        if (normalized.EndsWith(".tar.gz", StringComparison.Ordinal) || normalized.EndsWith(".tgz", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.TarGZip;
        }

        if (normalized.EndsWith(".tar.xz", StringComparison.Ordinal) || normalized.EndsWith(".txz", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.TarXz;
        }

        if (normalized.EndsWith(".zip", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.Zip;
        }

        if (normalized.EndsWith(".7z", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.SevenZip;
        }

        if (normalized.EndsWith(".tar", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.Tar;
        }

        if (normalized.EndsWith(".gz", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.GZip;
        }

        if (normalized.EndsWith(".xz", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.Xz;
        }

        if (normalized.EndsWith(".arc", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.Arc;
        }

        if (normalized.EndsWith(".rar", StringComparison.Ordinal))
        {
            return CreateArchiveFormat.RarUnsupported;
        }

        return CreateArchiveFormat.Unsupported;
    }

    private enum CreateArchiveFormat
    {
        Unsupported = 0,
        Zip,
        SevenZip,
        Tar,
        GZip,
        Xz,
        TarGZip,
        TarXz,
        Arc,
        RarUnsupported
    }

    public async Task<OperationResult<ArchivePreviewResult>> PreviewEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken)
    {
        if (ArcArchiveService.IsArcPath(request.ArchivePath))
        {
            return await ArcArchiveService.PreviewEntryAsync(request, entryPath, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            using var stream = File.OpenRead(request.ArchivePath);
            using var archive = ArchiveFactory.Open(stream, new ReaderOptions { Password = request.Password });
            var entry = archive.Entries.FirstOrDefault(candidate =>
                !candidate.IsDirectory &&
                string.Equals(candidate.Key, entryPath, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return OperationResult<ArchivePreviewResult>.Failure("archive.entry_missing", $"Archive entry not found: {entryPath}");
            }

            await using var memory = new MemoryStream();
            using var entryStream = entry.OpenEntryStream();
            await entryStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);

            var mime = GuessMime(entryPath);
            return OperationResult<ArchivePreviewResult>.Success(new ArchivePreviewResult(entryPath, mime, memory.ToArray()));
        }
        catch (global::System.Security.Cryptography.CryptographicException ex)
        {
            return OperationResult<ArchivePreviewResult>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (InvalidOperationException ex) when (LooksLikePasswordError(ex))
        {
            return OperationResult<ArchivePreviewResult>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (Exception ex)
        {
            return OperationResult<ArchivePreviewResult>.Failure("archive.preview_failed", "Failed to preview entry.", ex);
        }
    }

    public async Task<OperationResult> TestIntegrityAsync(ArchiveOpenRequest request, CancellationToken cancellationToken)
    {
        if (ArcArchiveService.IsArcPath(request.ArchivePath))
        {
            return await ArcArchiveService.TestIntegrityAsync(request, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            using var stream = File.OpenRead(request.ArchivePath);
            using var archive = ArchiveFactory.Open(stream, new ReaderOptions { Password = request.Password });

            foreach (var entry in archive.Entries.Where(static e => !e.IsDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var entryStream = entry.OpenEntryStream();
                await entryStream.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
            }

            return OperationResult.Success();
        }
        catch (global::System.Security.Cryptography.CryptographicException ex)
        {
            return OperationResult.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (InvalidOperationException ex) when (LooksLikePasswordError(ex))
        {
            return OperationResult.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("archive.integrity_failed", "Archive integrity test failed.", ex);
        }
    }

    public async Task<OperationResult<ArchiveEditSession>> CreateEditSessionAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken)
    {
        if (ArcArchiveService.IsArcPath(request.ArchivePath))
        {
            return await ArcArchiveService.CreateEditSessionAsync(request, entryPath, cancellationToken).ConfigureAwait(false);
        }

        if (!File.Exists(request.ArchivePath))
        {
            return OperationResult<ArchiveEditSession>.Failure("archive.not_found", $"Archive not found: {request.ArchivePath}");
        }

        try
        {
            using var stream = File.OpenRead(request.ArchivePath);
            using var archive = ArchiveFactory.Open(stream, new ReaderOptions { Password = request.Password });
            var entry = archive.Entries.FirstOrDefault(candidate =>
                !candidate.IsDirectory &&
                string.Equals(candidate.Key, entryPath, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return OperationResult<ArchiveEditSession>.Failure("archive.entry_missing", $"Archive entry not found: {entryPath}");
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "VaultArc", "edit-sessions", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var fileName = Path.GetFileName(entryPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "entry.bin";
            }

            var tempFilePath = Path.Combine(tempRoot, fileName);
            await using (var output = File.Create(tempFilePath))
            using (var entryStream = entry.OpenEntryStream())
            {
                await entryStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            var originalHash = await ComputeFileHashAsync(tempFilePath, cancellationToken).ConfigureAwait(false);
            var writeBackSupported = request.ArchivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                     && string.IsNullOrWhiteSpace(request.Password);

            var session = new ArchiveEditSession(
                request.ArchivePath,
                entryPath,
                tempFilePath,
                tempRoot,
                originalHash,
                writeBackSupported,
                request.Password);

            return OperationResult<ArchiveEditSession>.Success(session);
        }
        catch (global::System.Security.Cryptography.CryptographicException ex)
        {
            return OperationResult<ArchiveEditSession>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (InvalidOperationException ex) when (LooksLikePasswordError(ex))
        {
            return OperationResult<ArchiveEditSession>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (Exception ex)
        {
            return OperationResult<ArchiveEditSession>.Failure("archive.edit_session_failed", "Failed to create editable entry session.", ex);
        }
    }

    public async Task<OperationResult> SaveEditedEntryAsync(ArchiveEditSession session, CancellationToken cancellationToken)
    {
        if (ArcArchiveService.IsArcPath(session.ArchivePath))
        {
            return await ArcArchiveService.SaveEditedEntryAsync(session, cancellationToken).ConfigureAwait(false);
        }

        if (!session.IsWriteBackSupported)
        {
            return OperationResult.Failure(
                "archive.write_unsupported",
                "Saving changes back is currently supported for non-encrypted ZIP archives only.");
        }

        if (!File.Exists(session.ArchivePath))
        {
            return OperationResult.Failure("archive.not_found", $"Archive not found: {session.ArchivePath}");
        }

        if (!File.Exists(session.TempFilePath))
        {
            return OperationResult.Failure("archive.edit_file_missing", "The temporary edited file no longer exists.");
        }

        var archiveDirectory = Path.GetDirectoryName(session.ArchivePath);
        if (string.IsNullOrWhiteSpace(archiveDirectory))
        {
            return OperationResult.Failure("archive.invalid_path", "Invalid archive path.");
        }

        var normalizedTarget = NormalizeEntryPath(session.EntryPath);
        var tempArchivePath = Path.Combine(archiveDirectory, $"{Path.GetFileName(session.ArchivePath)}.vaultarc.tmp");

        try
        {
            await using var sourceStream = File.OpenRead(session.ArchivePath);
            using var sourceZip = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: false);

            await using var targetStream = File.Create(tempArchivePath);
            using var targetZip = new ZipArchive(targetStream, ZipArchiveMode.Create, leaveOpen: false);

            var replaced = false;
            foreach (var sourceEntry in sourceZip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalizedExisting = NormalizeEntryPath(sourceEntry.FullName);
                var isTarget = string.Equals(normalizedExisting, normalizedTarget, StringComparison.OrdinalIgnoreCase);

                if (isTarget)
                {
                    var replacement = targetZip.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                    replacement.LastWriteTime = DateTimeOffset.Now;
                    await using var replacementStream = replacement.Open();
                    await using var editedFile = File.OpenRead(session.TempFilePath);
                    await editedFile.CopyToAsync(replacementStream, cancellationToken).ConfigureAwait(false);
                    replaced = true;
                    continue;
                }

                var copiedEntry = targetZip.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                copiedEntry.LastWriteTime = sourceEntry.LastWriteTime;

                await using var copiedStream = copiedEntry.Open();
                await using var originalStream = sourceEntry.Open();
                await originalStream.CopyToAsync(copiedStream, cancellationToken).ConfigureAwait(false);
            }

            if (!replaced)
            {
                var newEntry = targetZip.CreateEntry(session.EntryPath, CompressionLevel.Optimal);
                await using var newEntryStream = newEntry.Open();
                await using var editedFile = File.OpenRead(session.TempFilePath);
                await editedFile.CopyToAsync(newEntryStream, cancellationToken).ConfigureAwait(false);
            }

            targetZip.Dispose();
            targetStream.Dispose();
            sourceZip.Dispose();
            sourceStream.Dispose();

            File.Copy(tempArchivePath, session.ArchivePath, overwrite: true);
            File.Delete(tempArchivePath);
            return OperationResult.Success();
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Failure("job.cancelled", "Save to archive cancelled.");
        }
        catch (Exception ex)
        {
            if (File.Exists(tempArchivePath))
            {
                File.Delete(tempArchivePath);
            }

            return OperationResult.Failure("archive.save_failed", "Failed to save edited file back into archive.", ex);
        }
    }

    public Task<OperationResult> CleanupEditSessionAsync(ArchiveEditSession session)
    {
        try
        {
            if (Directory.Exists(session.TempSessionDirectory))
            {
                Directory.Delete(session.TempSessionDirectory, recursive: true);
            }

            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Failure("archive.cleanup_failed", "Failed to clean temporary edit session files.", ex));
        }
    }

    public Task<OperationResult> RenameEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        string newEntryPath,
        CancellationToken cancellationToken)
    {
        if (ArcArchiveService.IsArcPath(request.ArchivePath))
        {
            return ArcArchiveService.RenameEntryAsync(request, entryPath, newEntryPath, cancellationToken);
        }

        return Task.FromResult(OperationResult.Failure(
            "archive.rename_unsupported",
            "Rename is currently supported for .arc archives only."));
    }

    public Task<OperationResult> DeleteEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken)
    {
        if (ArcArchiveService.IsArcPath(request.ArchivePath))
        {
            return ArcArchiveService.DeleteEntryAsync(request, entryPath, cancellationToken);
        }

        return Task.FromResult(OperationResult.Failure(
            "archive.delete_unsupported",
            "Delete is currently supported for .arc archives only."));
    }

    private static async Task<OperationResult> ValidatePasswordProbeAsync(IEnumerable<IArchiveEntry> entries, CancellationToken cancellationToken)
    {
        var probeEntry = entries.FirstOrDefault(entry => !entry.IsDirectory);
        if (probeEntry is null)
        {
            return OperationResult.Success();
        }

        try
        {
            await using var sink = new MemoryStream();
            using var stream = probeEntry.OpenEntryStream();
            var buffer = new byte[32];
            _ = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (global::System.Security.Cryptography.CryptographicException ex)
        {
            return OperationResult.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (InvalidOperationException ex) when (LooksLikePasswordError(ex))
        {
            return OperationResult.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
    }

    private static bool IsEntryEncrypted(IArchiveEntry entry)
    {
        var property = entry.GetType().GetProperty("IsEncrypted");
        if (property is null || property.PropertyType != typeof(bool))
        {
            return false;
        }

        return property.GetValue(entry) is true;
    }

    private static bool LooksLikePasswordError(Exception ex) =>
        ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<(string FullPath, string RelativePath)> ExpandFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                yield return (path, Path.GetFileName(path));
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            var rootName = new DirectoryInfo(path).Name;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(path, file);
                yield return (file, Path.Combine(rootName, relative).Replace('\\', '/'));
            }
        }
    }

    private static string GuessMime(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".txt" or ".md" or ".json" or ".xml" or ".yml" or ".yaml" or ".cs" or ".ts" or ".js" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static string NormalizeEntryPath(string path) => path.Replace('\\', '/');

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await global::System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
