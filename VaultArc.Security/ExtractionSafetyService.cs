using System.Text.RegularExpressions;
using VaultArc.Core;

namespace VaultArc.Security;

public sealed class ExtractionSafetyService : IExtractionSafetyService, IArchiveSecurityService
{
    private static readonly string[] SensitiveRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
    ];

    public OperationResult<string> ValidateExtractionTarget(string extractionRoot, string entryPath)
    {
        if (string.IsNullOrWhiteSpace(extractionRoot))
        {
            return OperationResult<string>.Failure("security.invalid_root", "Extraction root is required.");
        }

        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return OperationResult<string>.Failure("security.invalid_entry", "Archive entry path is required.");
        }

        if (Regex.IsMatch(entryPath, @"^[A-Za-z]:\\"))
        {
            return OperationResult<string>.Failure("security.absolute_path", $"Entry '{entryPath}' uses an absolute path.");
        }

        var normalizedEntryPath = entryPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(extractionRoot);
        var destinationPath = Path.GetFullPath(Path.Combine(fullRoot, normalizedEntryPath));

        if (!destinationPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<string>.Failure(
                "security.path_traversal",
                $"Entry '{entryPath}' resolves outside the extraction directory.");
        }

        return OperationResult<string>.Success(destinationPath);
    }

    public bool IsSensitiveLocation(string path)
    {
        var fullPath = Path.GetFullPath(path);

        return SensitiveRoots
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    public OperationResult<string> ValidateEntryTargetPath(string rootDirectory, string entryPath) =>
        ValidateExtractionTarget(rootDirectory, entryPath);
}
