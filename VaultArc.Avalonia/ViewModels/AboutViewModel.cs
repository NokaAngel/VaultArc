namespace VaultArc.Avalonia.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    public string AppName => "VaultArc";
    public string Version => "0.1.0-mvp";
    public string Description =>
        "VaultArc is a cross-platform archive manager built to modernize how users compress, extract, inspect, and verify archives."
        + "\n\n"
        + "Built with C#, .NET 8, and Avalonia UI."
        + "\n\n"
        + "Supports ZIP, 7Z, TAR, GZ, XZ, and encrypted .arc archive creation and extraction.";
}
