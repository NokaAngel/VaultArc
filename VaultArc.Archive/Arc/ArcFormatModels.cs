using System.Text.Json.Serialization;
using VaultArc.Models;

namespace VaultArc.Archive.Arc;

internal sealed record ArcHeader(
    ArcEncryptionProfileKind Profile,
    byte[] Salt,
    int Iterations,
    int MemoryKb,
    int Parallelism,
    byte[] ManifestNonce);

internal sealed record ArcManifest(
    DateTimeOffset CreatedUtc,
    List<ArcManifestEntry> Entries);

internal sealed record ArcManifestEntry(
    string Path,
    bool IsDirectory,
    long Size,
    DateTimeOffset? ModifiedUtc,
    long Offset,
    int CipherLength,
    string NonceBase64,
    string Sha256Hex);

internal sealed record ArcMutableEntry(
    string Path,
    bool IsDirectory,
    DateTimeOffset? ModifiedUtc,
    byte[]? Data);

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(ArcManifest))]
internal partial class ArcJsonContext : JsonSerializerContext;
