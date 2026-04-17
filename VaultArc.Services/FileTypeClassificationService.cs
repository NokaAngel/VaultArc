using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Services;

public sealed class FileTypeClassificationService : IFileTypeClassificationService
{
    private static readonly HashSet<string> RunnableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".msi", ".ps1", ".jar", ".lnk"
    };

    private static readonly HashSet<string> DangerousScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".cmd", ".ps1", ".vbs", ".js"
    };

    private static readonly HashSet<string> OpenableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".yml", ".yaml", ".csv",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".cs", ".ts", ".js", ".html", ".css", ".log"
    };

    public ArchiveEntryClassification Classify(string entryPath, bool isDirectory)
    {
        if (isDirectory)
        {
            return new ArchiveEntryClassification(entryPath, ArchiveEntryKind.Folder, false, false, false);
        }

        var extension = Path.GetExtension(entryPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return new ArchiveEntryClassification(entryPath, ArchiveEntryKind.Unknown, false, false, false);
        }

        if (DangerousScriptExtensions.Contains(extension))
        {
            return new ArchiveEntryClassification(entryPath, ArchiveEntryKind.DangerousScript, true, true, true);
        }

        if (RunnableExtensions.Contains(extension))
        {
            // .lnk is intentionally treated as warning-level runnable.
            var needsWarning = extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
            return new ArchiveEntryClassification(entryPath, ArchiveEntryKind.Runnable, needsWarning, true, true);
        }

        if (OpenableExtensions.Contains(extension))
        {
            return new ArchiveEntryClassification(entryPath, ArchiveEntryKind.Openable, false, false, true);
        }

        return new ArchiveEntryClassification(entryPath, ArchiveEntryKind.Unknown, false, false, false);
    }

    public bool IsRunnableExtension(string entryPath)
    {
        var extension = Path.GetExtension(entryPath);
        return !string.IsNullOrWhiteSpace(extension) && RunnableExtensions.Contains(extension);
    }
}
