using System.Text.RegularExpressions;

namespace VaultArc.Avalonia.Helpers;

internal static class SyntaxHighlightHelper
{
    public record ColoredSegment(string Text, string ColorHex);

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".cpp", ".c", ".h",
        ".go", ".rs", ".rb", ".php", ".swift", ".kt", ".scala", ".lua", ".r",
        ".json", ".xml", ".html", ".css", ".scss", ".yaml", ".yml", ".toml",
        ".md", ".sql", ".sh", ".bash", ".ps1", ".bat", ".cmd", ".dockerfile",
        ".gitignore", ".env", ".ini", ".cfg", ".conf", ".txt", ".log", ".csv"
    };

    public static bool IsCodeFile(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return CodeExtensions.Contains(ext);
    }

    public static List<ColoredSegment> Highlight(string text, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".json" => HighlightJson(text),
            ".xml" or ".html" or ".axaml" or ".xaml" or ".svg" => HighlightXml(text),
            ".css" or ".scss" => HighlightCss(text),
            ".md" => HighlightMarkdown(text),
            ".yaml" or ".yml" or ".toml" or ".ini" or ".cfg" or ".conf" => HighlightConfig(text),
            _ => HighlightGenericCode(text)
        };
    }

    private static List<ColoredSegment> HighlightGenericCode(string text)
    {
        var segments = new List<ColoredSegment>();
        var pattern = new Regex(
            @"(//[^\n]*|/\*[\s\S]*?\*/|#[^\n]*)" +          // comments
            @"|(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')" +   // strings
            @"|(\b\d+\.?\d*\b)" +                            // numbers
            @"|(\b(?:if|else|for|while|do|switch|case|break|continue|return|try|catch|finally|throw|new|class|struct|enum|interface|public|private|protected|internal|static|void|int|string|bool|var|let|const|function|def|import|from|using|namespace|async|await|yield|true|false|null|None|self|this|super)\b)", // keywords
            RegexOptions.Compiled);

        int lastEnd = 0;
        foreach (Match m in pattern.Matches(text))
        {
            if (m.Index > lastEnd)
                segments.Add(new ColoredSegment(text[lastEnd..m.Index], "#D4D4D4"));

            if (m.Groups[1].Success)
                segments.Add(new ColoredSegment(m.Value, "#6A9955"));
            else if (m.Groups[2].Success)
                segments.Add(new ColoredSegment(m.Value, "#CE9178"));
            else if (m.Groups[3].Success)
                segments.Add(new ColoredSegment(m.Value, "#B5CEA8"));
            else if (m.Groups[4].Success)
                segments.Add(new ColoredSegment(m.Value, "#569CD6"));

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < text.Length)
            segments.Add(new ColoredSegment(text[lastEnd..], "#D4D4D4"));

        return segments.Count == 0 ? [new ColoredSegment(text, "#D4D4D4")] : segments;
    }

    private static List<ColoredSegment> HighlightJson(string text)
    {
        var segments = new List<ColoredSegment>();
        var pattern = new Regex(
            @"(""(?:[^""\\]|\\.)*"")\s*:" +     // keys
            @"|(""(?:[^""\\]|\\.)*"")" +          // string values
            @"|(\b\d+\.?\d*\b)" +                 // numbers
            @"|(\btrue\b|\bfalse\b|\bnull\b)",    // literals
            RegexOptions.Compiled);

        int lastEnd = 0;
        foreach (Match m in pattern.Matches(text))
        {
            if (m.Index > lastEnd)
                segments.Add(new ColoredSegment(text[lastEnd..m.Index], "#D4D4D4"));

            if (m.Groups[1].Success)
                segments.Add(new ColoredSegment(m.Value, "#9CDCFE"));
            else if (m.Groups[2].Success)
                segments.Add(new ColoredSegment(m.Value, "#CE9178"));
            else if (m.Groups[3].Success)
                segments.Add(new ColoredSegment(m.Value, "#B5CEA8"));
            else if (m.Groups[4].Success)
                segments.Add(new ColoredSegment(m.Value, "#569CD6"));

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < text.Length)
            segments.Add(new ColoredSegment(text[lastEnd..], "#D4D4D4"));

        return segments.Count == 0 ? [new ColoredSegment(text, "#D4D4D4")] : segments;
    }

    private static List<ColoredSegment> HighlightXml(string text)
    {
        var segments = new List<ColoredSegment>();
        var pattern = new Regex(
            @"(<!--[\s\S]*?-->)" +                    // comments
            @"|(</?[\w:.-]+)" +                       // tag names
            @"|([\w:.-]+)(\s*=\s*)(""[^""]*"")" +    // attributes
            @"|(/>|>|</)",                             // brackets
            RegexOptions.Compiled);

        int lastEnd = 0;
        foreach (Match m in pattern.Matches(text))
        {
            if (m.Index > lastEnd)
                segments.Add(new ColoredSegment(text[lastEnd..m.Index], "#D4D4D4"));

            if (m.Groups[1].Success)
                segments.Add(new ColoredSegment(m.Value, "#6A9955"));
            else if (m.Groups[2].Success)
                segments.Add(new ColoredSegment(m.Value, "#569CD6"));
            else if (m.Groups[3].Success)
            {
                segments.Add(new ColoredSegment(m.Groups[3].Value, "#9CDCFE"));
                segments.Add(new ColoredSegment(m.Groups[4].Value, "#D4D4D4"));
                segments.Add(new ColoredSegment(m.Groups[5].Value, "#CE9178"));
            }
            else if (m.Groups[6].Success)
                segments.Add(new ColoredSegment(m.Value, "#808080"));

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < text.Length)
            segments.Add(new ColoredSegment(text[lastEnd..], "#D4D4D4"));

        return segments.Count == 0 ? [new ColoredSegment(text, "#D4D4D4")] : segments;
    }

    private static List<ColoredSegment> HighlightCss(string text)
    {
        var segments = new List<ColoredSegment>();
        var pattern = new Regex(
            @"(/\*[\s\S]*?\*/)" +            // comments
            @"|(#[0-9a-fA-F]{3,8}\b)" +      // hex colors
            @"|(\b\d+\.?\d*(?:px|em|rem|%|vh|vw|pt|deg|s|ms)?\b)" + // numbers with units
            @"|([\w-]+)\s*:" +               // property names
            @"|(""[^""]*""|'[^']*')",        // strings
            RegexOptions.Compiled);

        int lastEnd = 0;
        foreach (Match m in pattern.Matches(text))
        {
            if (m.Index > lastEnd)
                segments.Add(new ColoredSegment(text[lastEnd..m.Index], "#D4D4D4"));

            if (m.Groups[1].Success)
                segments.Add(new ColoredSegment(m.Value, "#6A9955"));
            else if (m.Groups[2].Success)
                segments.Add(new ColoredSegment(m.Value, "#CE9178"));
            else if (m.Groups[3].Success)
                segments.Add(new ColoredSegment(m.Value, "#B5CEA8"));
            else if (m.Groups[4].Success)
                segments.Add(new ColoredSegment(m.Value, "#9CDCFE"));
            else if (m.Groups[5].Success)
                segments.Add(new ColoredSegment(m.Value, "#CE9178"));

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < text.Length)
            segments.Add(new ColoredSegment(text[lastEnd..], "#D4D4D4"));

        return segments.Count == 0 ? [new ColoredSegment(text, "#D4D4D4")] : segments;
    }

    private static List<ColoredSegment> HighlightMarkdown(string text)
    {
        var segments = new List<ColoredSegment>();
        var pattern = new Regex(
            @"^(#{1,6}\s+.*)$" +                 // headings
            @"|(`[^`]+`)" +                       // inline code
            @"|(\*\*[^*]+\*\*|__[^_]+__)" +      // bold
            @"|(\*[^*]+\*|_[^_]+_)" +             // italic
            @"|(\[.*?\]\(.*?\))" +                // links
            @"|(^>\s+.*)$",                       // blockquotes
            RegexOptions.Compiled | RegexOptions.Multiline);

        int lastEnd = 0;
        foreach (Match m in pattern.Matches(text))
        {
            if (m.Index > lastEnd)
                segments.Add(new ColoredSegment(text[lastEnd..m.Index], "#D4D4D4"));

            if (m.Groups[1].Success)
                segments.Add(new ColoredSegment(m.Value, "#569CD6"));
            else if (m.Groups[2].Success)
                segments.Add(new ColoredSegment(m.Value, "#CE9178"));
            else if (m.Groups[3].Success)
                segments.Add(new ColoredSegment(m.Value, "#DCDCAA"));
            else if (m.Groups[4].Success)
                segments.Add(new ColoredSegment(m.Value, "#C586C0"));
            else if (m.Groups[5].Success)
                segments.Add(new ColoredSegment(m.Value, "#4EC9B0"));
            else if (m.Groups[6].Success)
                segments.Add(new ColoredSegment(m.Value, "#6A9955"));

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < text.Length)
            segments.Add(new ColoredSegment(text[lastEnd..], "#D4D4D4"));

        return segments.Count == 0 ? [new ColoredSegment(text, "#D4D4D4")] : segments;
    }

    private static List<ColoredSegment> HighlightConfig(string text)
    {
        var segments = new List<ColoredSegment>();
        var pattern = new Regex(
            @"(#[^\n]*|;[^\n]*)" +               // comments
            @"|(\[[^\]]+\])" +                    // section headers
            @"|([\w.-]+)\s*[:=]" +                // keys
            @"|(""[^""]*""|'[^']*')" +            // strings
            @"|(\b\d+\.?\d*\b)" +                 // numbers
            @"|(\btrue\b|\bfalse\b|\byes\b|\bno\b|\bon\b|\boff\b)", // booleans
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        int lastEnd = 0;
        foreach (Match m in pattern.Matches(text))
        {
            if (m.Index > lastEnd)
                segments.Add(new ColoredSegment(text[lastEnd..m.Index], "#D4D4D4"));

            if (m.Groups[1].Success)
                segments.Add(new ColoredSegment(m.Value, "#6A9955"));
            else if (m.Groups[2].Success)
                segments.Add(new ColoredSegment(m.Value, "#569CD6"));
            else if (m.Groups[3].Success)
                segments.Add(new ColoredSegment(m.Value, "#9CDCFE"));
            else if (m.Groups[4].Success)
                segments.Add(new ColoredSegment(m.Value, "#CE9178"));
            else if (m.Groups[5].Success)
                segments.Add(new ColoredSegment(m.Value, "#B5CEA8"));
            else if (m.Groups[6].Success)
                segments.Add(new ColoredSegment(m.Value, "#569CD6"));

            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < text.Length)
            segments.Add(new ColoredSegment(text[lastEnd..], "#D4D4D4"));

        return segments.Count == 0 ? [new ColoredSegment(text, "#D4D4D4")] : segments;
    }
}
