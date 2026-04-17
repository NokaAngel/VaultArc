using System.Text.Json;
using Microsoft.Extensions.Logging;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Infrastructure;

public sealed class JsonRecentArchivesStore : IRecentArchivesStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonRecentArchivesStore()
    {
        _filePath = Path.Combine(GetStateDirectory(), "recent-archives.json");
    }

    public async Task<IReadOnlyList<string>> GetRecentAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return list;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddRecentAsync(string archivePath, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = (await GetRecentInternalAsync(cancellationToken).ConfigureAwait(false))
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            existing.RemoveAll(path => path.Equals(archivePath, StringComparison.OrdinalIgnoreCase));
            existing.Insert(0, archivePath);

            if (existing.Count > 15)
            {
                existing = existing.Take(15).ToList();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<string>> GetRecentInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    private static string GetStateDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VaultArc");
}

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private readonly string _filePath;

    public JsonAppSettingsStore()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VaultArc",
            "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings(true, true, true, 2);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings(true, true, true, 2);
        }
        catch
        {
            return new AppSettings(true, true, true, 2);
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var folder = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(folder);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class VaultArcLogger<T> : ILogger<T>
{
    private readonly string _logPath;

    public VaultArcLogger()
    {
        _logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VaultArc",
            "vaultarc.log");
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var line = $"[{DateTimeOffset.UtcNow:O}] [{logLevel}] {formatter(state, exception)}{Environment.NewLine}";
        var folder = Path.GetDirectoryName(_logPath)!;
        Directory.CreateDirectory(folder);
        File.AppendAllText(_logPath, line);
    }
}
