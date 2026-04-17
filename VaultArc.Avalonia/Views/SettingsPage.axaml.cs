using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class SettingsPage : UserControl
{
    private SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
        SaveBtn.Click += SaveClicked;
    }

    private async void SaveClicked(object? sender, RoutedEventArgs e)
    {
        await ViewModel.SaveAsync();
        App.ApplyTheme(ViewModel.BuildSettings());
    }
}
