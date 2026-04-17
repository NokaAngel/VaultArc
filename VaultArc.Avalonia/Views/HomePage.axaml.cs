using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<HomeViewModel>();
        Loaded += async (_, _) =>
        {
            if (DataContext is HomeViewModel vm)
                await vm.RefreshAsync();
        };
    }
}
