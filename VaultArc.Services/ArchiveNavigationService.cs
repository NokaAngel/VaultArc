using VaultArc.Core;

namespace VaultArc.Services;

public sealed class ArchiveNavigationService : IArchiveNavigationService
{
    public string CurrentFolderPath { get; private set; } = string.Empty;

    public bool CanGoUp => !string.IsNullOrWhiteSpace(CurrentFolderPath);

    public void NavigateTo(string folderPath)
    {
        CurrentFolderPath = NormalizeFolder(folderPath);
    }

    public bool NavigateUp()
    {
        if (!CanGoUp)
        {
            return false;
        }

        var current = CurrentFolderPath;
        var slash = current.LastIndexOf('/');
        CurrentFolderPath = slash < 0 ? string.Empty : current[..slash];
        return true;
    }

    public void Reset()
    {
        CurrentFolderPath = string.Empty;
    }

    private static string NormalizeFolder(string path) => (path ?? string.Empty).Replace('\\', '/').Trim('/');
}
