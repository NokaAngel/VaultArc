using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.Tests;

public sealed class FileTypeClassificationTests
{
    private readonly FileTypeClassificationService _service = new();

    [Theory]
    [InlineData("bin/tool.exe", ArchiveEntryKind.Runnable, true, true, false)]
    [InlineData("scripts/deploy.ps1", ArchiveEntryKind.DangerousScript, true, true, false)]
    [InlineData("docs/readme.txt", ArchiveEntryKind.Openable, false, true, false)]
    [InlineData("folder", ArchiveEntryKind.Folder, false, false, true)]
    public void Classify_ReturnsExpectedKind(
        string path,
        ArchiveEntryKind expectedKind,
        bool expectedRunnable,
        bool expectedOpenable,
        bool isDirectory = false)
    {
        var classification = _service.Classify(path, isDirectory);

        Assert.Equal(expectedKind, classification.Kind);
        Assert.Equal(expectedRunnable, classification.IsRunnable);
        Assert.Equal(expectedOpenable, classification.IsOpenable);
    }
}
