using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Services;

public sealed class ArchiveDiffService : IArchiveDiffService
{
    public ArchiveDiffResult ComputeDiff(ArchiveSummary left, ArchiveSummary right)
    {
        var leftMap = left.Items.Where(i => !i.IsDirectory).ToDictionary(i => i.FullPath, StringComparer.OrdinalIgnoreCase);
        var rightMap = right.Items.Where(i => !i.IsDirectory).ToDictionary(i => i.FullPath, StringComparer.OrdinalIgnoreCase);

        var entries = new List<DiffEntry>();
        int added = 0, removed = 0, modified = 0, unchanged = 0;

        foreach (var (path, leftItem) in leftMap)
        {
            if (rightMap.TryGetValue(path, out var rightItem))
            {
                if (leftItem.Size != rightItem.Size || leftItem.ModifiedUtc != rightItem.ModifiedUtc)
                {
                    entries.Add(new DiffEntry(path, DiffEntryKind.Modified, leftItem.Size, rightItem.Size));
                    modified++;
                }
                else
                {
                    entries.Add(new DiffEntry(path, DiffEntryKind.Unchanged, leftItem.Size, rightItem.Size));
                    unchanged++;
                }
            }
            else
            {
                entries.Add(new DiffEntry(path, DiffEntryKind.Removed, leftItem.Size, null));
                removed++;
            }
        }

        foreach (var (path, rightItem) in rightMap)
        {
            if (!leftMap.ContainsKey(path))
            {
                entries.Add(new DiffEntry(path, DiffEntryKind.Added, null, rightItem.Size));
                added++;
            }
        }

        entries.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
        return new ArchiveDiffResult(entries, added, removed, modified, unchanged);
    }
}
