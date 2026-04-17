using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VaultArc.App.ViewModels;

namespace VaultArc.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = App.Services.GetRequiredService<SettingsViewModel>();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPageLoaded;
    }

    private async void SettingsPageLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        App.ApplyTheme(ViewModel.BuildSettings());
    }

    private async void SaveClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.SaveAsync();
        App.ApplyTheme(ViewModel.BuildSettings());
    }

    private void ThemeOptionChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        App.ApplyTheme(ViewModel.BuildSettings());

    public bool InvertBool(bool value) => !value;
}
