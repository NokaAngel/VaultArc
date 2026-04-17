using Microsoft.Win32;

namespace VaultArc.ShellExtension;

/// <summary>
/// Registers / unregisters the COM shell extension for Windows 11 modern context menu.
/// Call from VaultArc.App when the user toggles shell integration.
/// </summary>
public static class ShellExtensionRegistrar
{
    private const string Clsid = "{7B2E8F4A-3D5C-4E1A-9F6B-A2C8D7E5F043}";

    private static readonly string[] ArchiveExtensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".xz", ".tgz", ".txz", ".arc"];

    /// <summary>
    /// Registers the VaultArc shell extension COM server and hooks it into context menus
    /// for archive file types, all files (*), and directories.
    /// </summary>
    public static void Register(string comHostDllPath)
    {
        if (!File.Exists(comHostDllPath)) return;

        RegisterComServer(comHostDllPath);
        RegisterForAllFiles();
        RegisterForDirectories();
        RegisterForArchiveExtensions();
    }

    /// <summary>
    /// Removes all VaultArc shell extension registry entries.
    /// </summary>
    public static void Unregister()
    {
        TryDelete($@"Software\Classes\CLSID\{Clsid}");
        TryDelete($@"Software\Classes\*\shell\VaultArc");
        TryDelete($@"Software\Classes\Directory\shell\VaultArc");

        foreach (var ext in ArchiveExtensions)
        {
            TryDelete($@"Software\Classes\SystemFileAssociations\{ext}\shell\VaultArc");
        }
    }

    private static void RegisterComServer(string dllPath)
    {
        using var clsidKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\CLSID\{Clsid}");
        clsidKey.SetValue("", "VaultArc Shell Extension");

        using var inproc = clsidKey.CreateSubKey("InprocServer32");
        inproc.SetValue("", dllPath);
        inproc.SetValue("ThreadingModel", "Apartment");
    }

    private static void RegisterForAllFiles()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\VaultArc");
        key.SetValue("ExplorerCommandHandler", Clsid);
    }

    private static void RegisterForDirectories()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell\VaultArc");
        key.SetValue("ExplorerCommandHandler", Clsid);
    }

    private static void RegisterForArchiveExtensions()
    {
        foreach (var ext in ArchiveExtensions)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\VaultArc");
            key.SetValue("ExplorerCommandHandler", Clsid);
        }
    }

    private static void TryDelete(string subKey)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(subKey, false); } catch { }
    }
}
