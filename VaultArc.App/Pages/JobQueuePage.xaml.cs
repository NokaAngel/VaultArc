using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VaultArc.App.ViewModels;

namespace VaultArc.App.Pages;

public sealed partial class JobQueuePage : Page
{
    public JobQueueViewModel ViewModel { get; } = App.Services.GetRequiredService<JobQueueViewModel>();

    public JobQueuePage()
    {
        InitializeComponent();
    }

    private void RefreshClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Refresh();
    }

    private void CancelJobClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid jobId })
        {
            return;
        }

        _ = ViewModel.Cancel(jobId);
    }
}
