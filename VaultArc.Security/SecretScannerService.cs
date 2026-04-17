using System.Diagnostics;
using System.Text.RegularExpressions;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Security;

public sealed class SecretScannerService(IArchiveService archiveService) : ISecretScannerService
{
    private static readonly (string Name, Regex Pattern)[] Patterns =
    [
        ("AWS Access Key", new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)),
        ("AWS Secret Key", new Regex(@"(?i)aws_secret_access_key\s*[=:]\s*\S{20,}", RegexOptions.Compiled)),
        ("GitHub Token", new Regex(@"gh[pousr]_[A-Za-z0-9_]{36,}", RegexOptions.Compiled)),
        ("Generic API Key", new Regex(@"(?i)(api[_-]?key|apikey)\s*[=:""]\s*[A-Za-z0-9_\-]{16,}", RegexOptions.Compiled)),
        ("Generic Secret", new Regex(@"(?i)(secret|password|passwd|token)\s*[=:""]\s*[^\s""']{8,}", RegexOptions.Compiled)),
        ("Private Key", new Regex(@"-----BEGIN\s+(RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----", RegexOptions.Compiled)),
        ("JWT Token", new Regex(@"eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]+", RegexOptions.Compiled)),
        ("Connection String", new Regex(@"(?i)(server|data source|host)\s*=\s*[^;]+;\s*(user|uid|password|pwd)\s*=", RegexOptions.Compiled)),
        (".env Secret", new Regex(@"^[A-Z_]{2,}=\S{8,}$", RegexOptions.Compiled | RegexOptions.Multiline)),
    ];

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".py", ".java", ".go", ".rs", ".rb", ".php", ".swift",
        ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf",
        ".env", ".txt", ".md", ".sh", ".bash", ".ps1", ".bat", ".cmd",
        ".html", ".css", ".sql", ".dockerfile", ".tf", ".properties",
        ".gitignore", ".npmrc", ".config", ".settings"
    };

    public async Task<OperationResult<SecretScanReport>> ScanArchiveAsync(
        ArchiveOpenRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var openResult = await archiveService.OpenAsync(request, cancellationToken).ConfigureAwait(false);
        if (openResult.IsFailure)
            return OperationResult<SecretScanReport>.Failure(openResult.Error!.Code, openResult.Error.Message);

        var findings = new List<SecretFinding>();
        int filesScanned = 0;

        foreach (var item in openResult.Value!.Items.Where(i => !i.IsDirectory))
        {
            var ext = Path.GetExtension(item.Name);
            if (!TextExtensions.Contains(ext) && !item.Name.Contains(".env", StringComparison.OrdinalIgnoreCase))
                continue;

            cancellationToken.ThrowIfCancellationRequested();

            var previewResult = await archiveService.PreviewEntryAsync(request, item.FullPath, cancellationToken)
                .ConfigureAwait(false);
            if (previewResult.IsFailure || previewResult.Value?.Data == null)
                continue;

            filesScanned++;
            var text = System.Text.Encoding.UTF8.GetString(previewResult.Value.Data);
            var lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                foreach (var (name, pattern) in Patterns)
                {
                    var match = pattern.Match(line);
                    if (match.Success)
                    {
                        var snippet = line.Length > 120 ? line[..120] + "..." : line;
                        findings.Add(new SecretFinding(item.FullPath, i + 1, name, snippet.Trim()));
                    }
                }
            }
        }

        sw.Stop();
        var report = new SecretScanReport(findings, filesScanned, sw.Elapsed);
        return OperationResult<SecretScanReport>.Success(report);
    }
}
