using Microsoft.Win32;

namespace VaultArc.App.Services;

/// <summary>
/// Registers / unregisters Windows Explorer context menu entries for VaultArc.
/// 
/// Two layers:
/// 1. Classic cascading submenu (shows under "Show more options" on Win11, and directly on Win10)
/// 2. Modern Windows 11 IExplorerCommand COM shell extension (shows in top-level context menu)
/// </summary>
internal static class ShellContextMenuService
{
    private const string RootKey = @"Software\Classes";
    private const string ShellSubKey = "VaultArc";
    private const string ShellExtClsid = "{7B2E8F4A-3D5C-4E1A-9F6B-A2C8D7E5F043}";

    private static readonly string[] ArchiveExtensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".xz", ".tgz", ".txz", ".arc"];

    private static string ExePath =>
        Path.Combine(AppContext.BaseDirectory, "VaultArc.App.exe");

    public static void Register()
    {
        var exe = ExePath;
        if (!File.Exists(exe)) return;

        RegisterCascadingMenu(exe);
        RegisterModernContextMenu();
    }

    public static void Unregister()
    {
        UnregisterCascadingMenu();
        UnregisterModernContextMenu();
    }

    #region Classic cascading submenu (Win10 + Win11 "Show more options")

    private static void RegisterCascadingMenu(string exe)
    {
        // Register the root cascading menu for all files
        RegisterCascadeRoot($@"{RootKey}\*\shell\{ShellSubKey}", exe);
        RegisterCascadeSubItems($@"{RootKey}\*\shell\{ShellSubKey}", exe,
            ("Open", "Open with VaultArc", "--open"),
            ("ExtractHere", "Extract here", "--extract-here"),
            ("ExtractTo", "Extract to folder...", "--extract"),
            ("AddToArc", "Add to archive...", "--add"));

        // Register for directory backgrounds
        RegisterCascadeRoot($@"{RootKey}\Directory\Background\shell\{ShellSubKey}", exe);
        RegisterCascadeSubItems($@"{RootKey}\Directory\Background\shell\{ShellSubKey}", exe,
            ("AddToArc", "Compress with VaultArc", "--add"));

        // Register for directories
        RegisterCascadeRoot($@"{RootKey}\Directory\shell\{ShellSubKey}", exe);
        RegisterCascadeSubItems($@"{RootKey}\Directory\shell\{ShellSubKey}", exe,
            ("AddToArc", "Add to archive...", "--add"));

        // Register per archive extension for priority
        foreach (var ext in ArchiveExtensions)
        {
            var baseKey = $@"{RootKey}\SystemFileAssociations\{ext}\shell\{ShellSubKey}";
            RegisterCascadeRoot(baseKey, exe);
            RegisterCascadeSubItems(baseKey, exe,
                ("Open", "Open with VaultArc", "--open"),
                ("ExtractHere", "Extract here", "--extract-here"),
                ("ExtractTo", "Extract to folder...", "--extract"));
        }
    }

    private static void RegisterCascadeRoot(string keyPath, string exe)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue("MUIVerb", "VaultArc");
        key.SetValue("Icon", exe);
        key.SetValue("SubCommands", "");
    }

    private static void RegisterCascadeSubItems(string parentKeyPath, string exe,
        params (string Id, string Label, string Verb)[] items)
    {
        var shellPath = $@"{parentKeyPath}\shell";
        for (var i = 0; i < items.Length; i++)
        {
            var (id, label, verb) = items[i];
            var itemKey = $@"{shellPath}\{id}";
            using var key = Registry.CurrentUser.CreateSubKey(itemKey);
            key.SetValue("MUIVerb", label);
            key.SetValue("Icon", exe);
            // Position ordering (alphabetical fallback, but prefix for sort)
            key.SetValue("Position", i == 0 ? "Top" : "");

            using var cmd = Registry.CurrentUser.CreateSubKey($@"{itemKey}\command");
            var placeholder = verb == "--add" && parentKeyPath.Contains("Background")
                ? "%V" : "%1";
            cmd.SetValue("", $"\"{exe}\" {verb} \"{placeholder}\"");
        }
    }

    private static void UnregisterCascadingMenu()
    {
        TryDeleteKey($@"{RootKey}\*\shell\{ShellSubKey}");
        TryDeleteKey($@"{RootKey}\Directory\Background\shell\{ShellSubKey}");
        TryDeleteKey($@"{RootKey}\Directory\shell\{ShellSubKey}");

        foreach (var ext in ArchiveExtensions)
            TryDeleteKey($@"{RootKey}\SystemFileAssociations\{ext}\shell\{ShellSubKey}");
    }

    #endregion

    #region Modern Windows 11 IExplorerCommand shell extension

    private static void RegisterModernContextMenu()
    {
        var comHostDll = FindComHostDll();
        if (comHostDll is null) return;

        // Register COM server
        using var clsidKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\CLSID\{ShellExtClsid}");
        clsidKey.SetValue("", "VaultArc Shell Extension");
        using var inproc = clsidKey.CreateSubKey("InprocServer32");
        inproc.SetValue("", comHostDll);
        inproc.SetValue("ThreadingModel", "Apartment");

        // Hook into all files
        using var allFiles = Registry.CurrentUser.CreateSubKey($@"Software\Classes\*\shell\{ShellSubKey}Modern");
        allFiles.SetValue("ExplorerCommandHandler", ShellExtClsid);

        // Hook into directories
        using var dirs = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Directory\shell\{ShellSubKey}Modern");
        dirs.SetValue("ExplorerCommandHandler", ShellExtClsid);

        // Hook per archive extension
        foreach (var ext in ArchiveExtensions)
        {
            using var extKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\SystemFileAssociations\{ext}\shell\{ShellSubKey}Modern");
            extKey.SetValue("ExplorerCommandHandler", ShellExtClsid);
        }
    }

    private static void UnregisterModernContextMenu()
    {
        TryDeleteKey($@"Software\Classes\CLSID\{ShellExtClsid}");
        TryDeleteKey($@"Software\Classes\*\shell\{ShellSubKey}Modern");
        TryDeleteKey($@"Software\Classes\Directory\shell\{ShellSubKey}Modern");

        foreach (var ext in ArchiveExtensions)
            TryDeleteKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\{ShellSubKey}Modern");
    }

    private static string? FindComHostDll()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "VaultArc.ShellExtension.comhost.dll");
        if (File.Exists(candidate)) return candidate;

        var shellExtDir = Path.Combine(Path.GetDirectoryName(baseDir.TrimEnd('\\', '/'))!, "VaultArc.ShellExtension");
        candidate = Path.Combine(shellExtDir, "VaultArc.ShellExtension.comhost.dll");
        if (File.Exists(candidate)) return candidate;

        return null;
    }

    #endregion

    private static void TryDeleteKey(string subKey)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false); }
        catch { }
    }
}
