using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.Tests;

public sealed class ArchiveBrowseServiceTests
{
    private readonly ArchiveBrowseService _service = new();

    [Fact]
    public void GetChildren_ReturnsTopLevelFoldersAndFiles()
    {
        var items = new List<ArchiveItem>
        {
            new("app.exe", "app.exe", 10, false, DateTimeOffset.UtcNow),
            new("bin/tool.exe", "tool.exe", 20, false, DateTimeOffset.UtcNow),
            new("docs/readme.txt", "readme.txt", 5, false, DateTimeOffset.UtcNow)
        };

        var children = _service.GetChildren(items, string.Empty);

        Assert.Contains(children, entry => entry.IsDirectory && entry.Name == "bin");
        Assert.Contains(children, entry => entry.IsDirectory && entry.Name == "docs");
        Assert.Contains(children, entry => !entry.IsDirectory && entry.Name == "app.exe");
    }

    [Fact]
    public void GetParentFolder_ReturnsExpectedParent()
    {
        var parent = _service.GetParentFolder("bin/tools");
        Assert.Equal("bin", parent);
    }
}
