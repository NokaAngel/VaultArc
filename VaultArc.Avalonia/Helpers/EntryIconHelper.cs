namespace VaultArc.Avalonia.Helpers;

internal static class EntryIconHelper
{
    public static string GetGlyph(string fullPath, bool isDirectory)
    {
        if (isDirectory) return "📁";

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        return ext switch
        {
            ".exe" or ".com" or ".msi" => "⚙",
            ".bat" or ".cmd" or ".ps1" or ".vbs" or ".js" => "📜",
            ".dll" or ".sys" or ".ocx" => "🔧",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".xz" or ".arc" or ".tgz" or ".txz" => "📦",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp" or ".svg" => "🖼",
            ".mp3" or ".wav" or ".flac" or ".ogg" or ".aac" or ".wma" => "🎵",
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm" => "🎬",
            ".pdf" => "📕",
            ".doc" or ".docx" or ".rtf" or ".odt" => "📝",
            ".xls" or ".xlsx" or ".csv" or ".ods" => "📊",
            ".ppt" or ".pptx" or ".odp" => "📋",
            ".txt" or ".log" or ".md" or ".ini" or ".cfg" or ".conf" => "📝",
            ".xml" or ".json" or ".yaml" or ".yml" or ".toml" => "📜",
            ".cs" or ".cpp" or ".c" or ".h" or ".java" or ".py" or ".rb" or ".go" or ".rs" or ".ts" or ".tsx" or ".jsx" => "📜",
            ".html" or ".htm" or ".css" or ".scss" => "🌐",
            ".iso" or ".img" or ".vhd" or ".vmdk" => "💿",
            ".ttf" or ".otf" or ".woff" or ".woff2" => "🔤",
            ".lnk" or ".url" => "🔗",
            _ => "📄"
        };
    }
}
