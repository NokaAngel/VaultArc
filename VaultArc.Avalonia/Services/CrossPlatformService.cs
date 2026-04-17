using System.Diagnostics;
using System.Runtime.InteropServices;
using VaultArc.Core;

namespace VaultArc.Avalonia.Services;

internal sealed class CrossPlatformService : IPlatformService
{
    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) return;

        if (IsWindows)
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false });
        else if (IsMacOS)
            Process.Start(new ProcessStartInfo("open", $"\"{path}\"") { UseShellExecute = false });
        else
            Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = false });
    }

    public void OpenFile(string path)
    {
        if (!File.Exists(path)) return;

        if (IsWindows)
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else if (IsMacOS)
            Process.Start(new ProcessStartInfo("open", $"\"{path}\"") { UseShellExecute = false });
        else
            Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = false });
    }

    public void ShowNotification(string title, string message)
    {
        if (IsLinux)
        {
            try { Process.Start(new ProcessStartInfo("notify-send", $"\"{title}\" \"{message}\"") { UseShellExecute = false }); }
            catch { }
        }
        else if (IsMacOS)
        {
            try { Process.Start(new ProcessStartInfo("osascript", $"-e 'display notification \"{message}\" with title \"{title}\"'") { UseShellExecute = false }); }
            catch { }
        }
    }
}
