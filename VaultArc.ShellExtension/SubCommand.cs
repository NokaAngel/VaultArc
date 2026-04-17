using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VaultArc.ShellExtension;

internal sealed class SubCommand : IExplorerCommand
{
    private readonly string _title;
    private readonly string _iconPath;
    private readonly string _verb;
    private readonly Guid _canonicalId;

    public SubCommand(string title, string iconPath, string verb, Guid canonicalId)
    {
        _title = title;
        _iconPath = iconPath;
        _verb = verb;
        _canonicalId = canonicalId;
    }

    public void GetTitle(IShellItemArray? psiItemArray, out string ppszName) => ppszName = _title;
    public void GetIcon(IShellItemArray? psiItemArray, out string ppszIcon) => ppszIcon = _iconPath;
    public void GetToolTip(IShellItemArray? psiItemArray, out string ppszInfotip) => ppszInfotip = string.Empty;
    public void GetCanonicalName(out Guid pguidCommandName) => pguidCommandName = _canonicalId;
    public void GetState(IShellItemArray? psiItemArray, bool fOkToBeSlow, out uint pCmdState) => pCmdState = 0; // ECS_ENABLED
    public void GetFlags(out uint pFlags) => pFlags = 0;
    public void EnumSubCommands(out IEnumExplorerCommand? ppEnum) => ppEnum = null;

    public void Invoke(IShellItemArray? psiItemArray, IntPtr pbc)
    {
        if (psiItemArray is null) return;

        psiItemArray.GetCount(out var count);
        if (count == 0) return;

        psiItemArray.GetItemAt(0, out var item);
        item.GetDisplayName(0x80058000 /* SIGDN_FILESYSPATH */, out var path);

        var exe = VaultArcRootCommand.GetExePath();
        if (string.IsNullOrEmpty(exe)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"{_verb} \"{path}\"",
                UseShellExecute = false
            });
        }
        catch { }
    }
}
