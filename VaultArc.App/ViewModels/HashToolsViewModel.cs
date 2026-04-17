using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.App.ViewModels;

public partial class HashToolsViewModel(VaultArcFacade facade) : ViewModelBase
{
    [ObservableProperty]
    private string _leftFilePath = string.Empty;

    [ObservableProperty]
    private string _rightFilePath = string.Empty;

    [ObservableProperty]
    private VaultArcHashAlgorithm _algorithm = VaultArcHashAlgorithm.Sha256;

    [ObservableProperty]
    private string _comparisonResult = string.Empty;

    public ObservableCollection<HashReportItem> Hashes { get; } = [];

    public Array Algorithms => Enum.GetValues(typeof(VaultArcHashAlgorithm));

    public async Task HashSingleAsync(string filePath)
    {
        var result = await facade.HashFileAsync(filePath, Algorithm, CancellationToken.None);
        if (result.IsFailure || result.Value is null)
        {
            StatusMessage = result.Error?.Message ?? "Hash failed.";
            return;
        }

        Hashes.Add(result.Value);
        StatusMessage = "File hashed.";
    }

    public async Task CompareAsync()
    {
        var result = await facade.CompareHashesAsync(LeftFilePath, RightFilePath, Algorithm, CancellationToken.None);
        if (result.IsFailure || result.Value is null)
        {
            ComparisonResult = result.Error?.Message ?? "Hash compare failed.";
            return;
        }

        ComparisonResult = result.Value.Matches
            ? "Hashes match."
            : "Hashes do not match.";
    }

    public async Task ExportAsync(string outputPath)
    {
        var export = await facade.ExportHashReportAsync(Hashes, outputPath, CancellationToken.None);
        StatusMessage = export.IsSuccess ? $"Exported to {export.Value}" : export.Error?.Message ?? "Export failed.";
    }
}
