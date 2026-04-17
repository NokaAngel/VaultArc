using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;
using VaultArc.App.Pages;
using VaultArc.Models;
using VaultArc.Services;

namespace VaultArc.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        NavFrame.Navigate(typeof(HomePage));
    }

    public void ApplyWindowChromeTheme(bool isDark)
    {
        var foreground = isDark ? Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
        var foregroundDisabled = isDark ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x99, 0x00, 0x00, 0x00);
        var buttonHover = isDark ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x20, 0x00, 0x00, 0x00);
        var buttonPressed = isDark ? Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x35, 0x00, 0x00, 0x00);

        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
        titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = foregroundDisabled;
        titleBar.ButtonHoverBackgroundColor = buttonHover;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = buttonPressed;
        titleBar.ButtonPressedForegroundColor = foreground;
    }

    public void NavigateMainFrame(Type pageType) => NavFrame.Navigate(pageType);

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "home":
                    NavFrame.Navigate(typeof(HomePage));
                    break;
                case "open":
                    await NavigateOpenArchiveAsync();
                    break;
                case "create":
                    NavFrame.Navigate(typeof(CreateArchivePage));
                    break;
                case "jobs":
                    NavFrame.Navigate(typeof(JobQueuePage));
                    break;
                case "hash":
                    NavFrame.Navigate(typeof(HashToolsPage));
                    break;
                case "about":
                    NavFrame.Navigate(typeof(AboutPage));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown navigation item tag: {item.Tag}");
            }
        }
    }

    private async Task NavigateOpenArchiveAsync()
    {
        var facade = App.Services.GetRequiredService<VaultArcFacade>();
        var settings = await facade.LoadSettingsAsync(CancellationToken.None);
        if (settings.UseClassicLayout)
        {
            ClassicArchiveExplorerWindow.EnsureVisible();
            NavFrame.Navigate(typeof(HomePage));
            SelectNavItemByTag("home");
        }
        else
        {
            NavFrame.Navigate(typeof(OpenArchivePage));
        }
    }

    private void SelectNavItemByTag(string tag)
    {
        foreach (var raw in NavView.MenuItems)
        {
            if (raw is NavigationViewItem navItem && navItem.Tag as string == tag)
            {
                NavView.SelectedItem = navItem;
                return;
            }
        }
    }
}
