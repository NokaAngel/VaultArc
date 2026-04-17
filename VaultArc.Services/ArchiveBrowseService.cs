using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Services;

public sealed class ArchiveBrowseService : IArchiveBrowseService
{
    public IReadOnlyList<ArchiveItem> GetChildren(IReadOnlyList<ArchiveItem> allItems, string currentFolderPath)
    {
        var normalizedCurrent = NormalizeFolder(currentFolderPath);
        var folderLookup = new Dictionary<string, ArchiveItem>(StringComparer.OrdinalIgnoreCase);
        var directFiles = new List<ArchiveItem>();

        foreach (var item in allItems)
        {
            var normalizedItemPath = NormalizePath(item.FullPath);
            if (string.IsNullOrWhiteSpace(normalizedItemPath))
            {
                continue;
            }

            var parent = GetParentFolder(normalizedItemPath) ?? string.Empty;
            if (!parent.Equals(normalizedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                var isWithinCurrent = normalizedCurrent.Length == 0 ||
                                      normalizedItemPath.StartsWith($"{normalizedCurrent}/", StringComparison.OrdinalIgnoreCase);
                if (isWithinCurrent)
                {
                    var remaining = normalizedCurrent.Length == 0
                        ? normalizedItemPath
                        : normalizedItemPath[(normalizedCurrent.Length + 1)..];
                    var slash = remaining.IndexOf('/', StringComparison.Ordinal);
                    if (slash > 0)
                    {
                        var firstSegment = remaining[..slash];
                        var folderPath = normalizedCurrent.Length == 0
                            ? firstSegment
                            : $"{normalizedCurrent}/{firstSegment}";
                        if (!folderLookup.ContainsKey(folderPath))
                        {
                            folderLookup[folderPath] = new ArchiveItem(
                                folderPath,
                                firstSegment,
                                0,
                                true,
                                null,
                                null,
                                "Folder");
                        }
                    }
                }

                continue;
            }

            if (item.IsDirectory)
            {
                folderLookup[normalizedItemPath] = item with { FullPath = normalizedItemPath };
            }
            else
            {
                directFiles.Add(item with { FullPath = normalizedItemPath });
            }
        }

        return folderLookup.Values
            .OrderBy(static i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Concat(directFiles.OrderBy(static i => i.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public string? GetParentFolder(string currentFolderPath)
    {
        var normalized = NormalizeFolder(currentFolderPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalized[..lastSlash];
    }

    private static string NormalizeFolder(string path) => NormalizePath(path).TrimEnd('/');

    private static string NormalizePath(string path) => (path ?? string.Empty).Replace('\\', '/').Trim('/');
}
