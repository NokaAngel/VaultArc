using System.Runtime.InteropServices;

namespace VaultArc.ShellExtension;

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
public interface IExplorerCommand
{
    void GetTitle(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetIcon(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszIcon);
    void GetToolTip(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszInfotip);
    void GetCanonicalName(out Guid pguidCommandName);
    void GetState(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out uint pCmdState);
    void Invoke(IShellItemArray? psiItemArray, IntPtr pbc);
    void GetFlags(out uint pFlags);
    void EnumSubCommands(out IEnumExplorerCommand? ppEnum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
public interface IEnumExplorerCommand
{
    [PreserveSig]
    int Next(uint celt, [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] IExplorerCommand[] pUICommand, out uint pceltFetched);
    void Skip(uint celt);
    void Reset();
    void Clone(out IEnumExplorerCommand ppenum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
public interface IShellItemArray
{
    void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppvOut);
    void GetPropertyStore(int flags, [In] ref Guid riid, out IntPtr ppv);
    void GetPropertyDescriptionList([In] IntPtr keyType, [In] ref Guid riid, out IntPtr ppv);
    void GetAttributes(int AttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
    void GetCount(out uint pdwNumItems);
    void GetItemAt(uint dwIndex, out IShellItem ppsi);
    void EnumItems(out IntPtr ppenumShellItems);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
public interface IShellItem
{
    void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, uint hint, out int piOrder);
}
