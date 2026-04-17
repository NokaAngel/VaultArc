using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<AboutViewModel>();
    }
}
