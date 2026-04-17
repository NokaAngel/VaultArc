namespace VaultArc.App.Helpers;

/// <summary>
/// Returns a Segoe Fluent Icons glyph for a given archive entry based on extension / folder state.
/// </summary>
internal static class EntryIconHelper
{
    public static string GetGlyph(string fullPath, bool isDirectory)
    {
        if (isDirectory)
            return "\uE8B7"; // FolderOpen

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        return ext switch
        {
            ".exe" or ".com" or ".msi" => "\uE756", // Play (runnable)
            ".bat" or ".cmd" or ".ps1" or ".vbs" or ".js" => "\uE943", // Code
            ".dll" or ".sys" or ".ocx" => "\uE74C", // Settings gear
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".xz" or ".arc" or ".tgz" or ".txz" => "\uE8B7", // Package
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp" or ".svg" => "\uE8B9", // Picture
            ".mp3" or ".wav" or ".flac" or ".ogg" or ".aac" or ".wma" => "\uE8D6", // Music
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm" => "\uE714", // Video
            ".pdf" => "\uE736", // PDF
            ".doc" or ".docx" or ".rtf" or ".odt" => "\uE8A5", // Edit
            ".xls" or ".xlsx" or ".csv" or ".ods" => "\uE80A", // BulletedList
            ".ppt" or ".pptx" or ".odp" => "\uE8A1", // Slideshow
            ".txt" or ".log" or ".md" or ".ini" or ".cfg" or ".conf" => "\uE8A5", // Edit
            ".xml" or ".json" or ".yaml" or ".yml" or ".toml" => "\uE943", // Code
            ".cs" or ".cpp" or ".c" or ".h" or ".java" or ".py" or ".rb" or ".go" or ".rs" or ".ts" or ".tsx" or ".jsx" => "\uE943", // Code
            ".html" or ".htm" or ".css" or ".scss" => "\uE774", // Globe
            ".iso" or ".img" or ".vhd" or ".vmdk" => "\uE958", // HardDrive
            ".ttf" or ".otf" or ".woff" or ".woff2" => "\uE8D2", // Font
            ".lnk" or ".url" => "\uE71B", // Link
            _ => "\uE7C3" // Page (generic file)
        };
    }
}
