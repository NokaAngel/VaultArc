using VaultArc.Services;

namespace VaultArc.Tests;

public sealed class TempSessionServiceTests
{
    [Fact]
    public async Task CreateSession_ThenCleanupSession_Works()
    {
        var service = new TempSessionService();
        var create = await service.CreateSessionAsync(
            archivePath: "C:\\temp\\sample.zip",
            sessionReason: "test",
            isPinned: false,
            cleanupPolicy: VaultArc.Models.SessionCleanupPolicy.Auto,
            CancellationToken.None);

        Assert.True(create.IsSuccess);
        Assert.NotNull(create.Value);
        Assert.True(Directory.Exists(create.Value!.RootPath));

        var cleanup = await service.CleanupSessionAsync(create.Value.SessionId, force: true, CancellationToken.None);
        Assert.True(cleanup.IsSuccess);
        Assert.False(Directory.Exists(create.Value.RootPath));
    }
}
