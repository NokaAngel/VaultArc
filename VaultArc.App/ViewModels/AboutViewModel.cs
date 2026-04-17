namespace VaultArc.App.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    public string AppName => "VaultArc";
    public string Version => "0.1.0-mvp";
    public string Description =>
        "VaultArc is a native Windows archive manager built to modernize how users compress, extract, inspect, and verify archives."
        + "\n\n"
        + "Built with C#, .NET 8, and WinUI 3."
        + "\n\n"
        + "The current MVP supports ZIP archive creation, along with reading and extraction for ZIP, 7Z, TAR, GZ, and XZ formats.";
}
