using CommunityToolkit.Mvvm.ComponentModel;

namespace VaultArc.Avalonia.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;
}
