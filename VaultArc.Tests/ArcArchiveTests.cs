using VaultArc.Archive;
using VaultArc.Models;
using VaultArc.Security;

namespace VaultArc.Tests;

public sealed class ArcArchiveTests
{
    [Theory]
    [InlineData(ArcEncryptionProfileKind.XChaCha20Argon2id)]
    [InlineData(ArcEncryptionProfileKind.AesGcmPbkdf2)]
    public async Task CreateArc_AllProfiles_OpenSuccessfully(ArcEncryptionProfileKind profile)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "vaultarc-arc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var sourceFile = Path.Combine(tempRoot, "profile.txt");
            await File.WriteAllTextAsync(sourceFile, "profile-content");
            var arcPath = Path.Combine(tempRoot, "profile.arc");
            var service = new SharpCompressArchiveService(new ExtractionSafetyService());

            var create = await service.CreateArchiveAsync(
                new ArchiveCreateRequest(arcPath, [sourceFile], CompressionPresetKind.Balanced, "pass123!", profile),
                progress: null,
                CancellationToken.None);
            Assert.True(create.IsSuccess, create.Error?.Message);

            var open = await service.OpenAsync(new ArchiveOpenRequest(arcPath, "pass123!"), CancellationToken.None);
            Assert.True(open.IsSuccess, open.Error?.Message);
            Assert.Single(open.Value!.Items, static i => !i.IsDirectory);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateArc_OpenExtract_RoundTrips()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "vaultarc-arc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var sourceRoot = Path.Combine(tempRoot, "input");
            Directory.CreateDirectory(sourceRoot);
            var inputFile = Path.Combine(sourceRoot, "hello.txt");
            await File.WriteAllTextAsync(inputFile, "vaultarc arc content");
            var arcPath = Path.Combine(tempRoot, "sample.arc");

            var service = new SharpCompressArchiveService(new ExtractionSafetyService());
            var create = await service.CreateArchiveAsync(
                new ArchiveCreateRequest(
                    arcPath,
                    [sourceRoot],
                    CompressionPresetKind.Balanced,
                    "pass123!",
                    ArcEncryptionProfileKind.XChaCha20Argon2id),
                progress: null,
                CancellationToken.None);

            Assert.True(create.IsSuccess, create.Error?.Message);

            var open = await service.OpenAsync(new ArchiveOpenRequest(arcPath, "pass123!"), CancellationToken.None);
            Assert.True(open.IsSuccess, open.Error?.Message);
            Assert.Contains(open.Value!.Items, static item => item.FullPath.EndsWith("hello.txt", StringComparison.OrdinalIgnoreCase));

            var extractRoot = Path.Combine(tempRoot, "output");
            var extract = await service.ExtractAsync(
                new ArchiveExtractRequest(arcPath, extractRoot, OverwriteExisting: true, "pass123!"),
                progress: null,
                CancellationToken.None);
            Assert.True(extract.IsSuccess, extract.Error?.Message);

            var extracted = Directory.EnumerateFiles(extractRoot, "hello.txt", SearchOption.AllDirectories).Single();
            var text = await File.ReadAllTextAsync(extracted);
            Assert.Equal("vaultarc arc content", text);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Arc_WithWrongPassword_ReturnsInvalidPassword()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "vaultarc-arc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var sourceFile = Path.Combine(tempRoot, "note.txt");
            await File.WriteAllTextAsync(sourceFile, "secret");
            var arcPath = Path.Combine(tempRoot, "wrongpass.arc");
            var service = new SharpCompressArchiveService(new ExtractionSafetyService());

            var create = await service.CreateArchiveAsync(
                new ArchiveCreateRequest(arcPath, [sourceFile], CompressionPresetKind.Balanced, "right-pass"),
                progress: null,
                CancellationToken.None);
            Assert.True(create.IsSuccess, create.Error?.Message);

            var open = await service.OpenAsync(new ArchiveOpenRequest(arcPath, "bad-pass"), CancellationToken.None);
            Assert.True(open.IsFailure);
            Assert.Equal("archive.invalid_password", open.Error?.Code);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Arc_RenameAndDelete_UpdatesEntries()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "vaultarc-arc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var sourceFile = Path.Combine(tempRoot, "entry.txt");
            await File.WriteAllTextAsync(sourceFile, "value");
            var arcPath = Path.Combine(tempRoot, "mutate.arc");
            var service = new SharpCompressArchiveService(new ExtractionSafetyService());

            var create = await service.CreateArchiveAsync(
                new ArchiveCreateRequest(arcPath, [sourceFile], CompressionPresetKind.Balanced, "pass123!"),
                progress: null,
                CancellationToken.None);
            Assert.True(create.IsSuccess, create.Error?.Message);

            var open = await service.OpenAsync(new ArchiveOpenRequest(arcPath, "pass123!"), CancellationToken.None);
            var original = open.Value!.Items.Single(i => !i.IsDirectory);

            var rename = await service.RenameEntryAsync(
                new ArchiveOpenRequest(arcPath, "pass123!"),
                original.FullPath,
                "renamed.txt",
                CancellationToken.None);
            Assert.True(rename.IsSuccess, rename.Error?.Message);

            var openedAfterRename = await service.OpenAsync(new ArchiveOpenRequest(arcPath, "pass123!"), CancellationToken.None);
            Assert.Contains(openedAfterRename.Value!.Items, i => i.FullPath.Equals("renamed.txt", StringComparison.OrdinalIgnoreCase));

            var delete = await service.DeleteEntryAsync(
                new ArchiveOpenRequest(arcPath, "pass123!"),
                "renamed.txt",
                CancellationToken.None);
            Assert.True(delete.IsSuccess, delete.Error?.Message);

            var openedAfterDelete = await service.OpenAsync(new ArchiveOpenRequest(arcPath, "pass123!"), CancellationToken.None);
            Assert.DoesNotContain(openedAfterDelete.Value!.Items, i => i.FullPath.Equals("renamed.txt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
