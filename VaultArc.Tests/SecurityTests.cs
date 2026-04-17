using VaultArc.Security;

namespace VaultArc.Tests;

public sealed class SecurityTests
{
    private readonly ExtractionSafetyService _service = new();

    [Fact]
    public void ValidateExtractionTarget_AllowsSafePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "vaultarc-tests-root");
        var result = _service.ValidateExtractionTarget(root, "docs/readme.txt");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateExtractionTarget_BlocksPathTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "vaultarc-tests-root");
        var result = _service.ValidateExtractionTarget(root, "..\\..\\Windows\\System32\\malware.dll");
        Assert.True(result.IsFailure);
        Assert.Equal("security.path_traversal", result.Error?.Code);
    }
}
