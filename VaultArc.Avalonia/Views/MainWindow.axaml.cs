using Avalonia.Controls;

namespace VaultArc.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        NavList.SelectionChanged += NavList_SelectionChanged;
        ContentArea.Content = new HomePage();
    }

    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item) return;
        var tag = item.Tag?.ToString();
        ContentArea.Content = tag switch
        {
            "home" => new HomePage(),
            "open" => new OpenArchivePage(),
            "create" => new CreateArchivePage(),
            "jobs" => new JobQueuePage(),
            "hash" => new HashToolsPage(),
            "settings" => new SettingsPage(),
            "about" => new AboutPage(),
            _ => new HomePage()
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
}
