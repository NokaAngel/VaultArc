using System.Security.Cryptography;
using System.Text;
using VaultArc.Core;
using VaultArc.Models;

namespace VaultArc.Hashing;

public sealed class FileHashingService : IHashingService
{
    public async Task<OperationResult<HashReportItem>> HashFileAsync(
        string filePath,
        VaultArcHashAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return OperationResult<HashReportItem>.Failure("hash.file_missing", $"File not found: {filePath}");
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await ComputeHashAsync(stream, algorithm, cancellationToken).ConfigureAwait(false);
            var item = new HashReportItem(filePath, algorithm, Convert.ToHexString(hash), stream.Length);
            return OperationResult<HashReportItem>.Success(item);
        }
        catch (Exception ex)
        {
            return OperationResult<HashReportItem>.Failure("hash.failed", $"Failed to hash file: {filePath}", ex);
        }
    }

    public async Task<OperationResult<HashComparisonResult>> CompareFilesAsync(
        string leftPath,
        string rightPath,
        VaultArcHashAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        var leftResult = await HashFileAsync(leftPath, algorithm, cancellationToken).ConfigureAwait(false);
        if (leftResult.IsFailure || leftResult.Value is null)
        {
            return OperationResult<HashComparisonResult>.Failure(
                leftResult.Error?.Code ?? "hash.compare_left_failed",
                leftResult.Error?.Message ?? "Unable to hash first file.",
                leftResult.Error?.Exception);
        }

        var rightResult = await HashFileAsync(rightPath, algorithm, cancellationToken).ConfigureAwait(false);
        if (rightResult.IsFailure || rightResult.Value is null)
        {
            return OperationResult<HashComparisonResult>.Failure(
                rightResult.Error?.Code ?? "hash.compare_right_failed",
                rightResult.Error?.Message ?? "Unable to hash second file.",
                rightResult.Error?.Exception);
        }

        var compareResult = new HashComparisonResult(
            leftResult.Value.HashHex.Equals(rightResult.Value.HashHex, StringComparison.OrdinalIgnoreCase),
            leftResult.Value.HashHex,
            rightResult.Value.HashHex,
            algorithm);

        return OperationResult<HashComparisonResult>.Success(compareResult);
    }

    public async Task<OperationResult<string>> ExportReportAsync(
        IEnumerable<HashReportItem> items,
        string outputPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lines = new List<string>
            {
                "VaultArc Hash Report",
                $"GeneratedUtc: {DateTimeOffset.UtcNow:O}",
                string.Empty
            };

            lines.AddRange(items.Select(item => $"{item.Algorithm},{item.HashHex},{item.FileSizeBytes},\"{item.FilePath}\""));
            await File.WriteAllLinesAsync(outputPath, lines, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            return OperationResult<string>.Success(outputPath);
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Failure("hash.export_failed", "Failed to export hash report.", ex);
        }
    }

    private static async Task<byte[]> ComputeHashAsync(Stream stream, VaultArcHashAlgorithm algorithm, CancellationToken cancellationToken)
    {
        using HashAlgorithm hasher = algorithm switch
        {
            VaultArcHashAlgorithm.Md5 => MD5.Create(),
            VaultArcHashAlgorithm.Sha256 => SHA256.Create(),
            VaultArcHashAlgorithm.Sha512 => SHA512.Create(),
            _ => SHA256.Create()
        };

        return await hasher.ComputeHashAsync(stream, cancellationToken);
    }
}
