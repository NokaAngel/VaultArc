using System.Runtime.InteropServices;

namespace VaultArc.ShellExtension;

/// <summary>
/// Root context menu entry that shows "VaultArc" with a cascade of sub-commands.
/// This GUID must match the CLSID registered in the Windows registry.
/// </summary>
[ComVisible(true)]
[Guid("7B2E8F4A-3D5C-4E1A-9F6B-A2C8D7E5F043")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class VaultArcRootCommand : IExplorerCommand
{
    // ECF_HASSUBCOMMANDS
    private const uint ECF_HASSUBCOMMANDS = 0x00000008;

    private static readonly Guid CanonicalId = new("7B2E8F4A-3D5C-4E1A-9F6B-A2C8D7E5F043");

    public void GetTitle(IShellItemArray? psiItemArray, out string ppszName) => ppszName = "VaultArc";

    public void GetIcon(IShellItemArray? psiItemArray, out string ppszIcon)
    {
        var exe = GetExePath();
        ppszIcon = string.IsNullOrEmpty(exe) ? string.Empty : $"{exe},0";
    }

    public void GetToolTip(IShellItemArray? psiItemArray, out string ppszInfotip)
        => ppszInfotip = "VaultArc archive operations";

    public void GetCanonicalName(out Guid pguidCommandName) => pguidCommandName = CanonicalId;

    public void GetState(IShellItemArray? psiItemArray, bool fOkToBeSlow, out uint pCmdState)
        => pCmdState = 0; // ECS_ENABLED

    public void Invoke(IShellItemArray? psiItemArray, IntPtr pbc) { }

    public void GetFlags(out uint pFlags) => pFlags = ECF_HASSUBCOMMANDS;

    public void EnumSubCommands(out IEnumExplorerCommand? ppEnum)
    {
        var exe = GetExePath();
        var icon = string.IsNullOrEmpty(exe) ? string.Empty : $"{exe},0";

        var commands = new IExplorerCommand[]
        {
            new SubCommand("Open with VaultArc", icon, "--open",
                new Guid("7B2E8F4B-3D5C-4E1A-9F6B-A2C8D7E5F043")),

            new SubCommand("Extract here", icon, "--extract-here",
                new Guid("7B2E8F4C-3D5C-4E1A-9F6B-A2C8D7E5F043")),

            new SubCommand("Extract to folder...", icon, "--extract",
                new Guid("7B2E8F4D-3D5C-4E1A-9F6B-A2C8D7E5F043")),

            new SubCommand("Add to archive...", icon, "--add",
                new Guid("7B2E8F4E-3D5C-4E1A-9F6B-A2C8D7E5F043")),
        };

        ppEnum = new SubCommandEnumerator(commands);
    }

    internal static string GetExePath()
    {
        var dll = typeof(VaultArcRootCommand).Assembly.Location;
        if (string.IsNullOrEmpty(dll)) return string.Empty;

        var dir = Path.GetDirectoryName(dll);
        if (dir is null) return string.Empty;

        var candidate = Path.Combine(dir, "VaultArc.App.exe");
        if (File.Exists(candidate)) return candidate;

        var parent = Path.GetDirectoryName(dir);
        if (parent is not null)
        {
            candidate = Path.Combine(parent, "VaultArc.App.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return string.Empty;
    }
}
