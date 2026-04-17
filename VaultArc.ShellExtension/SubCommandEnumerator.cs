using System.Runtime.InteropServices;

namespace VaultArc.ShellExtension;

internal sealed class SubCommandEnumerator : IEnumExplorerCommand
{
    private readonly IExplorerCommand[] _commands;
    private int _index;

    public SubCommandEnumerator(IExplorerCommand[] commands)
    {
        _commands = commands;
    }

    public int Next(uint celt, IExplorerCommand[] pUICommand, out uint pceltFetched)
    {
        pceltFetched = 0;
        for (uint i = 0; i < celt && _index < _commands.Length; i++)
        {
            pUICommand[i] = _commands[_index++];
            pceltFetched++;
        }
        return pceltFetched == celt ? 0 : 1; // S_OK or S_FALSE
    }

    public void Skip(uint celt) => _index += (int)celt;
    public void Reset() => _index = 0;
    public void Clone(out IEnumExplorerCommand ppenum) => ppenum = new SubCommandEnumerator(_commands) { _index = _index };
}
