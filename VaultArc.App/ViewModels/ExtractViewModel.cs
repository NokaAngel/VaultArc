using CommunityToolkit.Mvvm.ComponentModel;
using VaultArc.Core;
using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.App.ViewModels;

public partial class ExtractViewModel(VaultArcFacade facade) : ViewModelBase
{
    [ObservableProperty]
    private string _archivePath = string.Empty;

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    private bool _overwriteExisting;

    [ObservableProperty]
    private string _password = string.Empty;

    public async Task<OperationResult> QueueExtractAsync()
    {
        var openResult = await facade.OpenArchiveAsync(
            new ArchiveOpenRequest(ArchivePath, string.IsNullOrWhiteSpace(Password) ? null : Password),
            CancellationToken.None);

        if (openResult.IsFailure)
        {
            StatusMessage = openResult.Error?.Message ?? "Unable to access archive.";
            return OperationResult.Failure(
                openResult.Error?.Code ?? "archive.open_failed",
                openResult.Error?.Message ?? "Unable to access archive.",
                openResult.Error?.Exception);
        }

        var request = new ArchiveExtractRequest(
            ArchivePath,
            DestinationPath,
            OverwriteExisting,
            string.IsNullOrWhiteSpace(Password) ? null : Password);

        var result = await facade.QueueExtractionAsync(request, CancellationToken.None);
        StatusMessage = result.IsSuccess ? "Extraction job queued." : result.Error?.Message ?? "Failed to queue extraction job.";
        return result;
    }
}
