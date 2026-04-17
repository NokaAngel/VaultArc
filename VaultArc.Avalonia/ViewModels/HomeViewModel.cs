using System.Collections.ObjectModel;
using VaultArc.Services;

namespace VaultArc.Avalonia.ViewModels;

public partial class HomeViewModel(VaultArcFacade facade) : ViewModelBase
{
    public ObservableCollection<string> RecentArchives { get; } = [];

    public async Task RefreshAsync()
    {
        try
        {
            var recent = await facade.GetRecentArchivesAsync(CancellationToken.None);
            RecentArchives.Clear();
            foreach (var path in recent.Take(8))
                RecentArchives.Add(path);
        }
        catch { }
    }
}
