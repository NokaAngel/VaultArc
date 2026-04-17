using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        NavList.SelectionChanged += NavList_SelectionChanged;
        ContentArea.Content = CreateHomePage();
        _ = CheckForUpdateAsync();
    }

    private HomePage CreateHomePage()
    {
        var homePage = new HomePage();
        if (homePage.DataContext is HomeViewModel homeVm)
        {
            homeVm.OpenArchiveRequested += path =>
            {
                NavigateToPage("open");
                if (ContentArea.Content is OpenArchivePage openPage &&
                    openPage.DataContext is OpenArchiveViewModel openVm)
                {
                    openVm.ArchivePath = path;
                    _ = openVm.OpenAsync(path);
                }
            };
            homeVm.NavigateRequested += tag => NavigateToPage(tag);
        }
        return homePage;
    }

    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item) return;
        var tag = item.Tag?.ToString();
        ContentArea.Content = tag switch
        {
            "home" => CreateHomePage(),
            "open" => new OpenArchivePage(),
            "create" => new CreateArchivePage(),
            "jobs" => new JobQueuePage(),
            "hash" => new HashToolsPage(),
            "settings" => new SettingsPage(),
            "about" => new AboutPage(),
            _ => CreateHomePage()
        };
    }

    public void NavigateToPage(string tag)
    {
        for (var i = 0; i < NavList.Items.Count; i++)
        {
            if (NavList.Items[i] is ListBoxItem item && item.Tag?.ToString() == tag)
            {
                NavList.SelectedIndex = i;
                return;
            }
        }
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var facade = App.Services.GetRequiredService<VaultArc.Services.VaultArcFacade>();
            var settings = await facade.LoadSettingsAsync(CancellationToken.None);
            if (!settings.CheckForUpdates) return;

            var result = await VaultArc.Avalonia.Services.UpdateCheckService.CheckAsync();
            if (result is { Available: true })
            {
                var banner = this.FindControl<Border>("UpdateBanner");
                var text = this.FindControl<TextBlock>("UpdateText");
                var link = this.FindControl<Button>("UpdateLink");
                var dismiss = this.FindControl<Button>("DismissUpdate");

                if (banner != null)
                {
                    if (text != null) text.Text = $"VaultArc v{result.Value.Version} is available!";
                    if (link != null) link.Click += (_, _) =>
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.Value.Url) { UseShellExecute = true }); } catch { }
                    };
                    if (dismiss != null) dismiss.Click += (_, _) => banner.IsVisible = false;
                    banner.IsVisible = true;
                }
            }
        }
        catch { }
    }
}
