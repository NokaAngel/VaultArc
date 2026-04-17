using CommunityToolkit.Mvvm.ComponentModel;
using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.App.ViewModels;

public partial class CreateArchiveViewModel(VaultArcFacade facade) : ViewModelBase
{
    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    private string _inputPaths = string.Empty;

    [ObservableProperty]
    private CompressionPresetKind _preset = CompressionPresetKind.Balanced;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private ArcEncryptionProfileKind _encryptionProfile = ArcEncryptionProfileKind.XChaCha20Argon2id;

    public Array Presets => Enum.GetValues(typeof(CompressionPresetKind));
    public Array EncryptionProfiles => Enum.GetValues(typeof(ArcEncryptionProfileKind));

    public void AddInputPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var existing = InputPaths
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (existing.Add(path))
        {
            InputPaths = string.Join(Environment.NewLine, existing.Order(StringComparer.OrdinalIgnoreCase));
        }
    }

    public async Task QueueCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath))
        {
            StatusMessage = "Select a destination archive path first.";
            return;
        }

        var inputs = InputPaths
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (inputs.Count == 0)
        {
            StatusMessage = "Add at least one input file or folder before queueing.";
            DeleteDestinationPlaceholderIfEmpty();
            return;
        }

        var existingInputs = inputs.Where(path => File.Exists(path) || Directory.Exists(path)).ToList();
        if (existingInputs.Count == 0)
        {
            StatusMessage = "None of the input paths were found on disk.";
            DeleteDestinationPlaceholderIfEmpty();
            return;
        }

        var extension = Path.GetExtension(DestinationPath);
        if (extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "7z creation is not supported by the current archive writer. Use .zip, .tar, .tar.gz, .tar.xz, or .arc.";
            DeleteDestinationPlaceholderIfEmpty();
            return;
        }

        if (extension.Equals(".rar", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "RAR creation is not supported. Use .zip, .tar, .tar.gz, .tar.xz, or .arc.";
            DeleteDestinationPlaceholderIfEmpty();
            return;
        }

        if (extension.Equals(".arc", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Encrypted .arc creation requires a password.";
            DeleteDestinationPlaceholderIfEmpty();
            return;
        }

        var request = new ArchiveCreateRequest(
            DestinationPath,
            existingInputs,
            Preset,
            string.IsNullOrWhiteSpace(Password) ? null : Password,
            EncryptionProfile);

        var result = await facade.QueueArchiveCreationAsync(request, CancellationToken.None);
        StatusMessage = result.IsSuccess ? "Compression job queued." : result.Error?.Message ?? "Failed to queue compression job.";
        if (result.IsFailure)
        {
            DeleteDestinationPlaceholderIfEmpty();
        }
    }

    private void DeleteDestinationPlaceholderIfEmpty()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath))
        {
            return;
        }

        try
        {
            if (File.Exists(DestinationPath))
            {
                var info = new FileInfo(DestinationPath);
                if (info.Length == 0)
                {
                    File.Delete(DestinationPath);
                }
            }
        }
        catch
        {
            // Best effort only.
        }
    }
}
