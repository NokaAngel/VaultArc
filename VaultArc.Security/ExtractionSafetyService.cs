using System.Text.RegularExpressions;
using VaultArc.Core;
using VaultArc.Models;

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

    public OperationResult ValidateAgainstPolicy(string entryPath, long entrySize, ExtractionPolicy policy)
    {
        if (policy.Kind == ExtractionPolicyKind.Permissive)
            return OperationResult.Success();

        if (policy.BlockExecutables)
        {
            var ext = Path.GetExtension(entryPath).ToLowerInvariant();
            if (ext is ".exe" or ".bat" or ".cmd" or ".ps1" or ".sh" or ".msi" or ".dll" or ".com" or ".scr" or ".vbs" or ".js" or ".wsf")
                return OperationResult.Failure("policy.executable_blocked", $"Executable file blocked by policy: {entryPath}");
        }

        if (policy.MaxFileSizeMB > 0 && entrySize > policy.MaxFileSizeMB * 1024L * 1024L)
            return OperationResult.Failure("policy.file_too_large", $"File exceeds policy size limit ({policy.MaxFileSizeMB} MB): {entryPath}");

        if (policy.BlockHiddenFiles)
        {
            var name = Path.GetFileName(entryPath);
            if (name.StartsWith('.') || name.StartsWith('_'))
                return OperationResult.Failure("policy.hidden_blocked", $"Hidden file blocked by policy: {entryPath}");
        }

        if (!string.IsNullOrWhiteSpace(policy.AllowedExtensions))
        {
            var allowed = policy.AllowedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ext = Path.GetExtension(entryPath).ToLowerInvariant();
            if (allowed.Length > 0 && !allowed.Any(a => a.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                return OperationResult.Failure("policy.extension_blocked", $"File extension not allowed by policy: {entryPath}");
        }

        return OperationResult.Success();
    }
}
