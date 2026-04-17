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

    private void RecentArchiveBtn_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && DataContext is HomeViewModel vm)
            vm.RequestOpenArchive(path);
    }

    private void FeatureCard_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && DataContext is HomeViewModel vm)
            vm.RequestNavigate(tag);
    }
}
