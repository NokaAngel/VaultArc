using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Archive.Arc;

internal static class ArcArchiveService
{
    private static readonly byte[] Magic = "VAC1"u8.ToArray();
    private const byte Version = 1;
    private static readonly byte[] RecoveryMarker = "VRCV"u8.ToArray();
    private const int RecoveryBlockLength = 17;

    internal static bool IsArcPath(string path) =>
        path.EndsWith(".arc", StringComparison.OrdinalIgnoreCase);

    internal static async Task<OperationResult> CreateAsync(
        ArchiveCreateRequest request,
        IReadOnlyList<(string FullPath, string RelativePath)> files,
        IProgress<JobProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return OperationResult.Failure("archive.password_required", "Password is required for .arc archives.");
        }

        var profile = request.EncryptionProfile;
        var nonceLength = ArcCrypto.GetNonceLength(profile);
        var header = new ArcHeader(
            profile,
            ArcCrypto.CreateRandom(ArcCrypto.SaltLength),
            profile == ArcEncryptionProfileKind.XChaCha20Argon2id ? ArcCrypto.Argon2Iterations : ArcCrypto.Pbkdf2Iterations,
            profile == ArcEncryptionProfileKind.XChaCha20Argon2id ? ArcCrypto.Argon2MemoryKb : 0,
            profile == ArcEncryptionProfileKind.XChaCha20Argon2id ? ArcCrypto.Argon2Parallelism : 0,
            ArcCrypto.CreateRandom(nonceLength));

        var key = ArcCrypto.DeriveKey(request.Password, profile, header.Salt, header.Iterations, header.MemoryKb, header.Parallelism);
        var entries = new List<ArcManifestEntry>(files.Count);
        var tempPath = request.DestinationArchivePath + ".creating";
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            await using var output = File.Create(tempPath);
            var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);

            // Reserve header and manifest pointer area.
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write((byte)profile);
            writer.Write((byte)header.Salt.Length);
            writer.Write((byte)header.ManifestNonce.Length);
            writer.Write(header.Iterations);
            writer.Write(header.MemoryKb);
            writer.Write(header.Parallelism);
            writer.Write(header.Salt);
            writer.Write(header.ManifestNonce);
            var manifestLengthPosition = output.Position;
            writer.Write((long)0);
            var manifestBytesPosition = output.Position;
            writer.Write(Array.Empty<byte>());

            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = files[i];
                var plain = await File.ReadAllBytesAsync(file.FullPath, cancellationToken).ConfigureAwait(false);
                var dataNonce = ArcCrypto.CreateRandom(nonceLength);
                var aad = Encoding.UTF8.GetBytes(file.RelativePath);
                var cipher = ArcCrypto.Encrypt(plain, key, profile, dataNonce, aad);
                var offset = output.Position;
                await output.WriteAsync(cipher, cancellationToken).ConfigureAwait(false);

                var hash = Convert.ToHexString(SHA256.HashData(plain));
                entries.Add(new ArcManifestEntry(
                    file.RelativePath.Replace('\\', '/'),
                    false,
                    plain.LongLength,
                    File.GetLastWriteTimeUtc(file.FullPath),
                    offset,
                    cipher.Length,
                    Convert.ToBase64String(dataNonce),
                    hash));

                var percent = ((i + 1d) / files.Count) * 100d;
                progress?.Report(new JobProgressUpdate(percent, $"Compressed {file.RelativePath}", DateTimeOffset.UtcNow - startedAt));
            }

            var manifest = new ArcManifest(DateTimeOffset.UtcNow, entries);
            var manifestPlain = JsonSerializer.SerializeToUtf8Bytes(manifest, ArcJsonContext.Default.ArcManifest);
            var manifestCipher = ArcCrypto.Encrypt(manifestPlain, key, profile, header.ManifestNonce, "manifest"u8.ToArray());

            // Write recovery block before manifest for disaster recovery.
            var recoveryPayload = new byte[13];
            RecoveryMarker.CopyTo(recoveryPayload.AsSpan(0, 4));
            BitConverter.TryWriteBytes(recoveryPayload.AsSpan(4, 8), output.Position + RecoveryBlockLength);
            recoveryPayload[12] = Version;
            var recoveryChecksum = ComputeRecoveryChecksum(recoveryPayload);
            await output.WriteAsync(recoveryPayload, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(recoveryChecksum, cancellationToken).ConfigureAwait(false);

            // Append manifest at end and store its length in header slot.
            var manifestOffset = output.Position;
            await output.WriteAsync(manifestCipher, cancellationToken).ConfigureAwait(false);
            output.Position = manifestLengthPosition;
            writer.Write((long)manifestCipher.Length);
            output.Position = manifestBytesPosition;
            // Keep legacy reserved slot stable: no-op byte sequence currently.
            output.Position = manifestOffset + manifestCipher.Length;
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            return OperationResult.Failure("archive.create_failed", "Failed to create .arc archive.", ex);
        }

        File.Copy(tempPath, request.DestinationArchivePath, overwrite: true);
        File.Delete(tempPath);
        return OperationResult.Success();
    }

    internal static async Task<OperationResult<ArchiveSummary>> OpenAsync(
        ArchiveOpenRequest request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(request, includeData: false, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || loaded.Value is null)
        {
            return OperationResult<ArchiveSummary>.Failure(
                loaded.Error?.Code ?? "archive.open_failed",
                loaded.Error?.Message ?? "Failed to open archive.",
                loaded.Error?.Exception);
        }

        var items = loaded.Value.Manifest.Entries
            .Select(entry => new ArchiveItem(
                entry.Path,
                Path.GetFileName(entry.Path),
                entry.Size,
                entry.IsDirectory,
                entry.ModifiedUtc,
                null))
            .OrderBy(static item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalBytes = items.Sum(static i => i.Size);
        return OperationResult<ArchiveSummary>.Success(new ArchiveSummary(request.ArchivePath, items, totalBytes));
    }

    internal static async Task<OperationResult> ExtractAsync(
        ArchiveExtractRequest request,
        IExtractionSafetyService safetyService,
        IProgress<JobProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        if (safetyService.IsSensitiveLocation(request.DestinationDirectory))
        {
            return OperationResult.Failure(
                "security.sensitive_destination",
                $"Extraction destination is sensitive and blocked by default: {request.DestinationDirectory}");
        }

        var loaded = await LoadAsync(new ArchiveOpenRequest(request.ArchivePath, request.Password), includeData: true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || loaded.Value is null)
        {
            return OperationResult.Failure(
                loaded.Error?.Code ?? "archive.extract_failed",
                loaded.Error?.Message ?? "Failed to extract archive.",
                loaded.Error?.Exception);
        }

        Directory.CreateDirectory(request.DestinationDirectory);
        var files = loaded.Value.Manifest.Entries.Where(static e => !e.IsDirectory).ToList();
        var startedAt = DateTimeOffset.UtcNow;

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = files[i];
            var validated = safetyService.ValidateExtractionTarget(request.DestinationDirectory, entry.Path);
            if (validated.IsFailure || string.IsNullOrWhiteSpace(validated.Value))
            {
                return OperationResult.Failure(
                    validated.Error?.Code ?? "security.invalid_entry_path",
                    validated.Error?.Message ?? $"Unsafe archive entry path: {entry.Path}");
            }

            if (!request.OverwriteExisting && File.Exists(validated.Value))
            {
                return OperationResult.Failure("archive.overwrite_blocked", $"Target file already exists: {validated.Value}");
            }

            var folder = Path.GetDirectoryName(validated.Value);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (!loaded.Value.DataByPath.TryGetValue(entry.Path, out var bytes))
            {
                return OperationResult.Failure("archive.entry_missing", $"Archive entry not found: {entry.Path}");
            }

            await File.WriteAllBytesAsync(validated.Value, bytes, cancellationToken).ConfigureAwait(false);
            var percent = ((i + 1d) / files.Count) * 100d;
            progress?.Report(new JobProgressUpdate(percent, $"Extracted {entry.Path}", DateTimeOffset.UtcNow - startedAt));
        }

        return OperationResult.Success();
    }

    internal static async Task<OperationResult<ArchivePreviewResult>> PreviewEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(request, includeData: true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || loaded.Value is null)
        {
            return OperationResult<ArchivePreviewResult>.Failure(
                loaded.Error?.Code ?? "archive.preview_failed",
                loaded.Error?.Message ?? "Failed to preview entry.",
                loaded.Error?.Exception);
        }

        if (!loaded.Value.DataByPath.TryGetValue(entryPath, out var data))
        {
            return OperationResult<ArchivePreviewResult>.Failure("archive.entry_missing", $"Archive entry not found: {entryPath}");
        }

        bool truncated = data.Length > 524_288;
        var previewData = truncated ? data[..524_288] : data;
        var mime = GuessMime(entryPath);
        return OperationResult<ArchivePreviewResult>.Success(new ArchivePreviewResult(entryPath, mime, previewData, truncated));
    }

    internal static async Task<OperationResult> TestIntegrityAsync(
        ArchiveOpenRequest request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(request, includeData: true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || loaded.Value is null)
        {
            return OperationResult.Failure(
                loaded.Error?.Code ?? "archive.integrity_failed",
                loaded.Error?.Message ?? "Integrity verification failed.",
                loaded.Error?.Exception);
        }

        foreach (var entry in loaded.Value.Manifest.Entries.Where(static e => !e.IsDirectory))
        {
            if (!loaded.Value.DataByPath.TryGetValue(entry.Path, out var data))
            {
                return OperationResult.Failure("archive.integrity_failed", $"Missing data for entry {entry.Path}.");
            }

            var hash = Convert.ToHexString(SHA256.HashData(data));
            if (!string.Equals(hash, entry.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Failure("archive.integrity_failed", $"Integrity mismatch for entry {entry.Path}.");
            }
        }

        return OperationResult.Success();
    }

    internal static async Task<OperationResult<IntegrityReport>> TestIntegrityDetailedAsync(
        ArchiveOpenRequest request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(request, includeData: true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || loaded.Value is null)
        {
            return OperationResult<IntegrityReport>.Failure(
                loaded.Error?.Code ?? "archive.integrity_failed",
                loaded.Error?.Message ?? "Integrity verification failed.",
                loaded.Error?.Exception);
        }

        var entries = new List<IntegrityReportEntry>();
        int valid = 0, invalid = 0;

        foreach (var entry in loaded.Value.Manifest.Entries.Where(static e => !e.IsDirectory))
        {
            if (!loaded.Value.DataByPath.TryGetValue(entry.Path, out var data))
            {
                entries.Add(new IntegrityReportEntry(entry.Path, false, $"Missing data for entry {entry.Path}."));
                invalid++;
                continue;
            }

            var hash = Convert.ToHexString(SHA256.HashData(data));
            if (!string.Equals(hash, entry.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(new IntegrityReportEntry(entry.Path, false, $"Hash mismatch for entry {entry.Path}."));
                invalid++;
            }
            else
            {
                entries.Add(new IntegrityReportEntry(entry.Path, true, null));
                valid++;
            }
        }

        return OperationResult<IntegrityReport>.Success(new IntegrityReport(entries, valid, invalid));
    }

    internal static async Task<OperationResult<IntegrityReport>> FastIntegrityScanAsync(
        ArchiveOpenRequest request,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(request.ArchivePath))
        {
            return OperationResult<IntegrityReport>.Failure("archive.not_found", $"Archive not found: {request.ArchivePath}");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return OperationResult<IntegrityReport>.Failure("archive.password_required", "This .arc archive requires a password.");
        }

        try
        {
            await using var stream = File.OpenRead(request.ArchivePath);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var fileSize = stream.Length;

            var magic = reader.ReadBytes(4);
            if (!magic.SequenceEqual(Magic))
            {
                return OperationResult<IntegrityReport>.Failure("archive.invalid_format", "Invalid .arc file format.");
            }

            var version = reader.ReadByte();
            if (version != Version)
            {
                return OperationResult<IntegrityReport>.Failure("archive.invalid_format", $"Unsupported .arc version: {version}");
            }

            var profile = (ArcEncryptionProfileKind)reader.ReadByte();
            var saltLength = reader.ReadByte();
            var nonceLength = reader.ReadByte();
            var iterations = reader.ReadInt32();
            var memoryKb = reader.ReadInt32();
            var parallelism = reader.ReadInt32();
            var salt = reader.ReadBytes(saltLength);
            var manifestNonce = reader.ReadBytes(nonceLength);
            var manifestLength = reader.ReadInt64();
            var dataRegionStart = stream.Position;

            if (!Enum.IsDefined(profile))
            {
                return OperationResult<IntegrityReport>.Failure("archive.crypto_profile_unsupported", "Unsupported encryption profile.");
            }

            var key = ArcCrypto.DeriveKey(request.Password, profile, salt, iterations, memoryKb, parallelism);

            var manifestOffset = fileSize - manifestLength;
            if (manifestOffset < dataRegionStart)
            {
                return OperationResult<IntegrityReport>.Failure("archive.invalid_format", "Archive manifest pointer is invalid.");
            }

            stream.Position = manifestOffset;
            var manifestCipher = reader.ReadBytes((int)manifestLength);
            var manifestPlain = ArcCrypto.Decrypt(manifestCipher, key, profile, manifestNonce, "manifest"u8.ToArray());
            var manifest = JsonSerializer.Deserialize(manifestPlain, ArcJsonContext.Default.ArcManifest);

            if (manifest is null)
            {
                return OperationResult<IntegrityReport>.Failure("archive.invalid_format", "Archive manifest is missing.");
            }

            var entries = new List<IntegrityReportEntry>();
            int valid = 0, invalid = 0;

            foreach (var entry in manifest.Entries.Where(static e => !e.IsDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var blobEnd = entry.Offset + entry.CipherLength;
                if (entry.Offset < dataRegionStart || blobEnd > fileSize)
                {
                    entries.Add(new IntegrityReportEntry(entry.Path, false,
                        $"Blob bounds [{entry.Offset}..{blobEnd}) exceed file bounds [{dataRegionStart}..{fileSize})."));
                    invalid++;
                }
                else
                {
                    entries.Add(new IntegrityReportEntry(entry.Path, true, null));
                    valid++;
                }
            }

            return OperationResult<IntegrityReport>.Success(new IntegrityReport(entries, valid, invalid));
        }
        catch (CryptographicException ex)
        {
            return OperationResult<IntegrityReport>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (ArgumentException ex)
        {
            return OperationResult<IntegrityReport>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (Exception ex)
        {
            return OperationResult<IntegrityReport>.Failure("archive.fast_scan_failed", "Fast integrity scan failed.", ex);
        }
    }

    internal static async Task<OperationResult<ArchiveEditSession>> CreateEditSessionAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken)
    {
        var preview = await PreviewEntryAsync(request, entryPath, cancellationToken).ConfigureAwait(false);
        if (preview.IsFailure || preview.Value is null)
        {
            return OperationResult<ArchiveEditSession>.Failure(
                preview.Error?.Code ?? "archive.edit_session_failed",
                preview.Error?.Message ?? "Failed to create edit session.",
                preview.Error?.Exception);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "VaultArc", "edit-sessions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var fileName = Path.GetFileName(entryPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "entry.bin";
        }

        var tempFilePath = Path.Combine(tempRoot, fileName);
        await File.WriteAllBytesAsync(tempFilePath, preview.Value.Data, cancellationToken).ConfigureAwait(false);
        var originalHash = Convert.ToHexString(SHA256.HashData(preview.Value.Data));

        var session = new ArchiveEditSession(
            request.ArchivePath,
            entryPath,
            tempFilePath,
            tempRoot,
            originalHash,
            true,
            request.Password);

        return OperationResult<ArchiveEditSession>.Success(session);
    }

    internal static async Task<OperationResult> SaveEditedEntryAsync(ArchiveEditSession session, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.Password))
        {
            return OperationResult.Failure("archive.password_required", "Password is required to update encrypted .arc entries.");
        }

        if (!File.Exists(session.TempFilePath))
        {
            return OperationResult.Failure("archive.edit_file_missing", "The temporary edited file no longer exists.");
        }

        var loaded = await LoadAsync(new ArchiveOpenRequest(session.ArchivePath, session.Password), includeData: true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || loaded.Value is null)
        {
            return OperationResult.Failure(
                loaded.Error?.Code ?? "archive.save_failed",
                loaded.Error?.Message ?? "Failed to load archive for save.",
                loaded.Error?.Exception);
        }

        var editedData = await File.ReadAllBytesAsync(session.TempFilePath, cancellationToken).ConfigureAwait(false);
        var mutable = loaded.Value.Manifest.Entries
            .Select(entry =>
            {
                if (entry.IsDirectory)
                {
                    return new ArcMutableEntry(entry.Path, true, entry.ModifiedUtc, null);
                }

                if (string.Equals(entry.Path, session.EntryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new ArcMutableEntry(entry.Path, false, DateTimeOffset.UtcNow, editedData);
                }

                return new ArcMutableEntry(entry.Path, false, entry.ModifiedUtc, loaded.Value.DataByPath[entry.Path]);
            })
            .ToList();

        return await RewriteArchiveAsync(session.ArchivePath, session.Password, loaded.Value.Header.Profile, mutable, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<OperationResult> RenameEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        string newEntryPath,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(request, includeData: true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || loaded.Value is null)
        {
            return OperationResult.Failure(
                loaded.Error?.Code ?? "archive.rename_failed",
                loaded.Error?.Message ?? "Failed to rename entry.",
                loaded.Error?.Exception);
        }

        var mutable = loaded.Value.Manifest.Entries
            .Select(entry =>
            {
                var targetPath = string.Equals(entry.Path, entryPath, StringComparison.OrdinalIgnoreCase) ? newEntryPath : entry.Path;
                return new ArcMutableEntry(
                    targetPath,
                    entry.IsDirectory,
                    entry.ModifiedUtc,
                    entry.IsDirectory ? null : loaded.Value.DataByPath[entry.Path]);
            })
            .ToList();

        return await RewriteArchiveAsync(request.ArchivePath, request.Password, loaded.Value.Header.Profile, mutable, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<OperationResult> DeleteEntryAsync(
        ArchiveOpenRequest request,
        string entryPath,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(request, includeData: true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure || loaded.Value is null)
        {
            return OperationResult.Failure(
                loaded.Error?.Code ?? "archive.delete_failed",
                loaded.Error?.Message ?? "Failed to delete entry.",
                loaded.Error?.Exception);
        }

        var mutable = loaded.Value.Manifest.Entries
            .Where(entry => !string.Equals(entry.Path, entryPath, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new ArcMutableEntry(
                entry.Path,
                entry.IsDirectory,
                entry.ModifiedUtc,
                entry.IsDirectory ? null : loaded.Value.DataByPath[entry.Path]))
            .ToList();

        return await RewriteArchiveAsync(request.ArchivePath, request.Password, loaded.Value.Header.Profile, mutable, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<OperationResult<LoadedArc>> LoadAsync(
        ArchiveOpenRequest request,
        bool includeData,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(request.ArchivePath))
        {
            return OperationResult<LoadedArc>.Failure("archive.not_found", $"Archive not found: {request.ArchivePath}");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return OperationResult<LoadedArc>.Failure("archive.password_required", "This .arc archive requires a password.");
        }

        try
        {
            await using var stream = File.OpenRead(request.ArchivePath);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!magic.SequenceEqual(Magic))
            {
                return OperationResult<LoadedArc>.Failure("archive.invalid_format", "Invalid .arc file format. The file may be incomplete or not created by VaultArc.");
            }

            var version = reader.ReadByte();
            if (version != Version)
            {
                return OperationResult<LoadedArc>.Failure("archive.invalid_format", $"Unsupported .arc version: {version}");
            }

            var profile = (ArcEncryptionProfileKind)reader.ReadByte();
            var saltLength = reader.ReadByte();
            var nonceLength = reader.ReadByte();
            var iterations = reader.ReadInt32();
            var memoryKb = reader.ReadInt32();
            var parallelism = reader.ReadInt32();
            var salt = reader.ReadBytes(saltLength);
            var manifestNonce = reader.ReadBytes(nonceLength);
            var manifestLength = reader.ReadInt64();

            if (!Enum.IsDefined(profile))
            {
                return OperationResult<LoadedArc>.Failure("archive.crypto_profile_unsupported", "Archive uses an unsupported encryption profile.");
            }

            var header = new ArcHeader(profile, salt, iterations, memoryKb, parallelism, manifestNonce);
            var key = ArcCrypto.DeriveKey(request.Password, profile, salt, iterations, memoryKb, parallelism);

            ArcManifest? manifest = null;
            var normalManifestOffset = stream.Length - manifestLength;
            if (normalManifestOffset >= stream.Position)
            {
                try
                {
                    stream.Position = normalManifestOffset;
                    var manifestCipher = reader.ReadBytes((int)manifestLength);
                    var manifestPlain = ArcCrypto.Decrypt(manifestCipher, key, profile, manifestNonce, "manifest"u8.ToArray());
                    manifest = JsonSerializer.Deserialize(manifestPlain, ArcJsonContext.Default.ArcManifest);
                }
                catch (CryptographicException) { throw; }
                catch (ArgumentException) { throw; }
                catch { /* structural damage — attempt recovery below */ }
            }

            manifest ??= TryRecoverManifest(stream, key, profile, manifestNonce);

            if (manifest is null)
            {
                return OperationResult<LoadedArc>.Failure("archive.invalid_format",
                    "Archive manifest is missing or could not be recovered.");
            }

            var dataByPath = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            if (includeData)
            {
                foreach (var entry in manifest.Entries.Where(static e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Position = entry.Offset;
                    var cipher = reader.ReadBytes(entry.CipherLength);
                    var nonce = Convert.FromBase64String(entry.NonceBase64);
                    var plain = ArcCrypto.Decrypt(cipher, key, profile, nonce, Encoding.UTF8.GetBytes(entry.Path));
                    dataByPath[entry.Path] = plain;
                }
            }

            return OperationResult<LoadedArc>.Success(new LoadedArc(header, manifest, dataByPath));
        }
        catch (CryptographicException ex)
        {
            return OperationResult<LoadedArc>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (ArgumentException ex)
        {
            return OperationResult<LoadedArc>.Failure("archive.invalid_password", "Invalid password for encrypted archive.", ex);
        }
        catch (Exception ex)
        {
            return OperationResult<LoadedArc>.Failure("archive.open_failed", "Failed to open .arc archive.", ex);
        }
    }

    private static async Task<OperationResult> RewriteArchiveAsync(
        string archivePath,
        string? password,
        ArcEncryptionProfileKind profile,
        IReadOnlyList<ArcMutableEntry> entries,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return OperationResult.Failure("archive.password_required", "Password is required.");
        }

        var tempPath = archivePath + ".tmp";
        try
        {
            var files = new List<(string FullPath, string RelativePath)>();
            var staging = Path.Combine(Path.GetTempPath(), "VaultArc", "arc-rewrite", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            foreach (var entry in entries.Where(static e => !e.IsDirectory))
            {
                var safeName = entry.Path.Replace('/', '_').Replace('\\', '_');
                var full = Path.Combine(staging, safeName);
                await File.WriteAllBytesAsync(full, entry.Data ?? [], cancellationToken).ConfigureAwait(false);
                files.Add((full, entry.Path));
            }

            var result = await CreateAsync(
                new ArchiveCreateRequest(tempPath, files.Select(f => f.FullPath).ToList(), CompressionPresetKind.Balanced, password, profile),
                files,
                progress: null,
                cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }

            File.Copy(tempPath, archivePath, overwrite: true);
            File.Delete(tempPath);
            Directory.Delete(staging, recursive: true);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            return OperationResult.Failure("archive.rewrite_failed", "Failed to rewrite .arc archive.", ex);
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

    private static byte[] ComputeRecoveryChecksum(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return hash[..4];
    }

    private static ArcManifest? TryRecoverManifest(
        Stream stream, byte[] key, ArcEncryptionProfileKind profile, byte[] manifestNonce)
    {
        try
        {
            var fileLength = stream.Length;
            if (fileLength < RecoveryBlockLength)
                return null;

            const int chunkSize = 1 << 20;
            var scanEnd = fileLength;

            while (scanEnd > 0)
            {
                var scanStart = Math.Max(0, scanEnd - chunkSize);
                var readLen = (int)(scanEnd - scanStart);

                stream.Position = scanStart;
                var chunk = new byte[readLen];
                var actualRead = stream.Read(chunk, 0, readLen);

                for (var i = actualRead - RecoveryBlockLength; i >= 0; i--)
                {
                    if (chunk[i] != 'V' || chunk[i + 1] != 'R' ||
                        chunk[i + 2] != 'C' || chunk[i + 3] != 'V')
                        continue;

                    var payload = chunk.AsSpan(i, 13).ToArray();
                    var storedChecksum = chunk.AsSpan(i + 13, 4).ToArray();
                    if (!storedChecksum.AsSpan().SequenceEqual(ComputeRecoveryChecksum(payload)))
                        continue;

                    var manifestStartOffset = BitConverter.ToInt64(payload, 4);
                    if (manifestStartOffset <= 0 || manifestStartOffset >= fileLength)
                        continue;

                    var manifestCipherLength = (int)(fileLength - manifestStartOffset);
                    var manifestCipher = new byte[manifestCipherLength];
                    stream.Position = manifestStartOffset;
                    stream.ReadExactly(manifestCipher, 0, manifestCipherLength);

                    var manifestPlain = ArcCrypto.Decrypt(
                        manifestCipher, key, profile, manifestNonce, "manifest"u8.ToArray());
                    return JsonSerializer.Deserialize(manifestPlain, ArcJsonContext.Default.ArcManifest);
                }

                scanEnd = scanStart + RecoveryBlockLength - 1;
                if (scanStart == 0) break;
            }
        }
        catch
        {
            // Recovery scan failed — caller handles the null return
        }

        return null;
    }

    private sealed record LoadedArc(ArcHeader Header, ArcManifest Manifest, Dictionary<string, byte[]> DataByPath);
}
