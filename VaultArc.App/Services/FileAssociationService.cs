using Microsoft.Win32;

namespace VaultArc.App.Services;

/// <summary>
/// Registers VaultArc as a handler for archive file types via HKCU.
/// No elevation required.
/// </summary>
internal static class FileAssociationService
{
    private const string ProgId = "VaultArc.Archive";
    private static readonly string[] Extensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".xz", ".tgz", ".txz", ".arc"];

    private static string ExePath =>
        Path.Combine(AppContext.BaseDirectory, "VaultArc.App.exe");

    public static void Register()
    {
        var exe = ExePath;
        if (!File.Exists(exe)) return;

        using var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        progKey.SetValue("", "VaultArc Archive");

        using var icon = progKey.CreateSubKey("DefaultIcon");
        icon.SetValue("", $"\"{exe}\",0");

        using var cmd = progKey.CreateSubKey(@"shell\open\command");
        cmd.SetValue("", $"\"{exe}\" --open \"%1\"");

        foreach (var ext in Extensions)
        {
            using var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithProgids");
            extKey.SetValue(ProgId, new byte[0], RegistryValueKind.None);
        }
    }

    public static void Unregister()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false); } catch { }

        foreach (var ext in Extensions)
        {
            try
            {
                using var extKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}\OpenWithProgids", true);
                extKey?.DeleteValue(ProgId, false);
            }
            catch { }
        }
    }
}
