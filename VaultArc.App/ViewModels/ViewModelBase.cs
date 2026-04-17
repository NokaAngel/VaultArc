using CommunityToolkit.Mvvm.ComponentModel;

namespace VaultArc.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;
}
