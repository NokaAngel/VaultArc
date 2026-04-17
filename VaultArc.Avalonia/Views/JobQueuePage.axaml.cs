using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class JobQueuePage : UserControl
{
    private JobQueueViewModel ViewModel { get; }

    public JobQueuePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<JobQueueViewModel>();
        DataContext = ViewModel;
        RefreshBtn.Click += (_, _) => ViewModel.Refresh();
    }

    private void CancelBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid jobId && DataContext is JobQueueViewModel vm)
            vm.Cancel(jobId);
    }
}
