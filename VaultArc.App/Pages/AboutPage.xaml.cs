using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VaultArc.App.ViewModels;

namespace VaultArc.App.Pages;

public sealed partial class AboutPage : Page
{
    public AboutViewModel ViewModel { get; } = App.Services.GetRequiredService<AboutViewModel>();

    public AboutPage()
    {
        InitializeComponent();
    }
}
