using System.Security.Cryptography;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Services;

public sealed class DuplicateDetectionService : IDuplicateDetectionService
{
    public async Task<OperationResult<DuplicateReport>> ScanForDuplicatesAsync(
        IReadOnlyList<string> inputPaths, CancellationToken cancellationToken)
    {
        var fileHashes = new Dictionary<string, List<(string Path, long Size)>>();
        int totalFiles = 0;

        foreach (var inputPath in inputPaths)
        {
            IEnumerable<string> files;
            if (Directory.Exists(inputPath))
                files = Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories);
            else if (File.Exists(inputPath))
                files = [inputPath];
            else
                continue;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalFiles++;
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length == 0) continue;

                    using var stream = File.OpenRead(file);
                    var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                    var hash = Convert.ToHexString(hashBytes);

                    if (!fileHashes.TryGetValue(hash, out var list))
                    {
                        list = [];
                        fileHashes[hash] = list;
                    }
                    list.Add((file, info.Length));
                }
                catch { }
            }
        }

        var groups = fileHashes
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => new DuplicateGroup(
                kv.Key,
                kv.Value[0].Size,
                kv.Value.Select(v => v.Path).ToList()))
            .ToList();

        var wastedBytes = groups.Sum(g => g.FileSize * (g.Paths.Count - 1));
        return OperationResult<DuplicateReport>.Success(new DuplicateReport(groups, wastedBytes, totalFiles));
    }
}
