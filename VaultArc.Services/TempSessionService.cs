using System.Diagnostics;
using System.Text.Json;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Services;

public sealed class TempSessionService : ITempSessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _sessionsRoot;
    private readonly SemaphoreSlim _sync = new(1, 1);

    public TempSessionService()
    {
        _sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VaultArc",
            "Temp",
            "Sessions");
    }

    public async Task<OperationResult<TempSessionInfo>> CreateSessionAsync(
        string archivePath,
        string sessionReason,
        bool isPinned,
        SessionCleanupPolicy cleanupPolicy,
        CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_sessionsRoot);
            var sessionId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            var rootPath = Path.Combine(_sessionsRoot, sessionId);
            Directory.CreateDirectory(rootPath);

            var now = DateTimeOffset.UtcNow;
            var metadata = new SessionMetadata
            {
                SessionId = sessionId,
                ArchivePath = archivePath,
                RootPath = rootPath,
                CreatedUtc = now,
                LastAccessUtc = now,
                IsPinned = isPinned,
                CleanupPolicy = cleanupPolicy,
                SessionReason = sessionReason,
                ProcessIds = []
            };

            await WriteMetadataAsync(metadata, cancellationToken).ConfigureAwait(false);
            return OperationResult<TempSessionInfo>.Success(metadata.ToInfo());
        }
        catch (Exception ex)
        {
            return OperationResult<TempSessionInfo>.Failure("session.create_failed", "Failed to create temp session.", ex);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<OperationResult> RegisterProcessAsync(string sessionId, int processId, CancellationToken cancellationToken)
    {
        return await UpdateSessionAsync(sessionId, metadata =>
        {
            if (!metadata.ProcessIds.Contains(processId))
            {
                metadata.ProcessIds.Add(processId);
            }

            metadata.LastAccessUtc = DateTimeOffset.UtcNow;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<OperationResult> TouchSessionAsync(string sessionId, CancellationToken cancellationToken) =>
        UpdateSessionAsync(sessionId, metadata => metadata.LastAccessUtc = DateTimeOffset.UtcNow, cancellationToken);

    public Task<OperationResult> PinSessionAsync(string sessionId, bool isPinned, CancellationToken cancellationToken) =>
        UpdateSessionAsync(sessionId, metadata => metadata.IsPinned = isPinned, cancellationToken);

    public async Task<OperationResult> OpenSessionFolderAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return OperationResult.Failure("session.not_found", $"Session not found: {sessionId}");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{session.RootPath}\"",
                UseShellExecute = true
            });

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("session.open_folder_failed", "Failed to open session folder.", ex);
        }
    }

    public async Task<OperationResult<IReadOnlyList<TempSessionInfo>>> GetSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sessions = new List<TempSessionInfo>();
            if (!Directory.Exists(_sessionsRoot))
            {
                return OperationResult<IReadOnlyList<TempSessionInfo>>.Success(sessions);
            }

            foreach (var directory in Directory.EnumerateDirectories(_sessionsRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metadata = await ReadMetadataByFolderAsync(directory, cancellationToken).ConfigureAwait(false);
                if (metadata is not null)
                {
                    sessions.Add(metadata.ToInfo());
                }
            }

            return OperationResult<IReadOnlyList<TempSessionInfo>>.Success(
                sessions.OrderByDescending(static s => s.CreatedUtc).ToList());
        }
        catch (Exception ex)
        {
            return OperationResult<IReadOnlyList<TempSessionInfo>>.Failure("session.list_failed", "Failed to list temp sessions.", ex);
        }
    }

    public async Task<OperationResult> CleanupOldSessionsAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(_sessionsRoot))
            {
                return OperationResult.Success();
            }

            var cutoff = DateTimeOffset.UtcNow - maxAge;
            foreach (var folder in Directory.EnumerateDirectories(_sessionsRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metadata = await ReadMetadataByFolderAsync(folder, cancellationToken).ConfigureAwait(false);
                if (metadata is null)
                {
                    continue;
                }

                if (metadata.IsPinned || metadata.CleanupPolicy == SessionCleanupPolicy.KeepPinned)
                {
                    continue;
                }

                if (metadata.LastAccessUtc > cutoff)
                {
                    continue;
                }

                if (HasRunningProcesses(metadata.ProcessIds))
                {
                    continue;
                }

                Directory.Delete(folder, true);
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("session.cleanup_failed", "Failed to cleanup old temp sessions.", ex);
        }
    }

    public async Task<OperationResult> CleanupSessionAsync(string sessionId, bool force, CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return OperationResult.Failure("session.not_found", $"Session not found: {sessionId}");
        }

        try
        {
            if (!force && HasRunningProcesses(session.ProcessIds))
            {
                return OperationResult.Failure("session.active_process", "Cannot cleanup session while launched processes are still running.");
            }

            if (Directory.Exists(session.RootPath))
            {
                Directory.Delete(session.RootPath, true);
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("session.cleanup_failed", "Failed to cleanup temp session.", ex);
        }
    }

    private async Task<OperationResult> UpdateSessionAsync(
        string sessionId,
        Action<SessionMetadata> mutate,
        CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var metadata = await LoadSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (metadata is null)
            {
                return OperationResult.Failure("session.not_found", $"Session not found: {sessionId}");
            }

            mutate(metadata);
            await WriteMetadataAsync(metadata, cancellationToken).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("session.update_failed", "Failed to update temp session.", ex);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task<SessionMetadata?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(_sessionsRoot, sessionId);
        return await ReadMetadataByFolderAsync(folder, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SessionMetadata?> ReadMetadataByFolderAsync(string folder, CancellationToken cancellationToken)
    {
        var metadataPath = GetMetadataPath(folder);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SessionMetadata>(json, JsonOptions);
    }

    private async Task WriteMetadataAsync(SessionMetadata metadata, CancellationToken cancellationToken)
    {
        var metadataPath = GetMetadataPath(metadata.RootPath);
        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken).ConfigureAwait(false);
    }

    private static string GetMetadataPath(string folder) => Path.Combine(folder, "session.json");

    private static bool HasRunningProcesses(IEnumerable<int> processIds)
    {
        foreach (var pid in processIds)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.HasExited)
                {
                    return true;
                }
            }
            catch
            {
                // Process not found is treated as not running.
            }
        }

        return false;
    }

    private sealed class SessionMetadata
    {
        public string SessionId { get; set; } = string.Empty;
        public string ArchivePath { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; }
        public DateTimeOffset LastAccessUtc { get; set; }
        public bool IsPinned { get; set; }
        public SessionCleanupPolicy CleanupPolicy { get; set; }
        public string SessionReason { get; set; } = string.Empty;
        public List<int> ProcessIds { get; set; } = [];

        public TempSessionInfo ToInfo() =>
            new(SessionId, ArchivePath, RootPath, CreatedUtc, LastAccessUtc, IsPinned, CleanupPolicy, ProcessIds.ToList());
    }
}
