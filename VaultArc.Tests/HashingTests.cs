using VaultArc.Hashing;
using VaultArc.Models;
using System.Security.Cryptography;

namespace VaultArc.Tests;

public sealed class HashingTests
{
    [Fact]
    public async Task HashFileAsync_ReturnsKnownSha256()
    {
        var service = new FileHashingService();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "vaultarc");

        string expectedHash;
        await using (var expectedStream = File.OpenRead(tempFile))
        {
            expectedHash = Convert.ToHexString(await SHA256.HashDataAsync(expectedStream));
        }

        var result = await service.HashFileAsync(tempFile, VaultArcHashAlgorithm.Sha256, CancellationToken.None);
        File.Delete(tempFile);
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedHash, result.Value?.HashHex);
    }
}
