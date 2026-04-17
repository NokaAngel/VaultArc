using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using VaultArc.Avalonia.ViewModels;

namespace VaultArc.Avalonia.Views;

public partial class ClassicArchiveExplorerWindow : Window
{
    public static ClassicArchiveExplorerWindow? Instance { get; private set; }
    private OpenArchiveViewModel ViewModel { get; }

    public ClassicArchiveExplorerWindow()
    {
        InitializeComponent();
        Instance = this;
        Closed += (_, _) => Instance = null;

        ViewModel = App.Services.GetRequiredService<OpenArchiveViewModel>();
        DataContext = ViewModel;

        MenuOpen.Click += BrowseClicked;
        MenuExtract.Click += BrowseExtractClicked;
        MenuClose.Click += (_, _) => Close();
        MenuOpenSel.Click += OpenSelClicked;
        MenuRunSel.Click += RunSelClicked;
        MenuPreview.Click += PreviewClicked;
        MenuIntegrity.Click += IntegrityClicked;
        MenuRefresh.Click += (_, _) => ViewModel.RefreshVisibleItems();

        TbOpen.Click += BrowseClicked;
        TbExtract.Click += BrowseExtractClicked;
        TbOpenSel.Click += OpenSelClicked;
        TbRunSel.Click += RunSelClicked;
        TbIntegrity.Click += IntegrityClicked;
        TbRefresh.Click += (_, _) => ViewModel.RefreshVisibleItems();

        NavRoot.Click += (_, _) => ViewModel.NavigateToRoot();
        NavUp.Click += (_, _) => ViewModel.NavigateUp();
        AddrBrowse.Click += BrowseClicked;
        AddrOpen.Click += OpenArchiveClicked;

        ClassicPreview.Click += PreviewClicked;
        ClassicBrowseExtract.Click += BrowseExtractClicked;
        ClassicExtractAll.Click += ExtractAllClicked;

        ClassicEntriesList.DoubleTapped += EntriesDoubleTapped;
    }

    public static void EnsureVisible()
    {
        if (Instance is null)
        {
            var window = new ClassicArchiveExplorerWindow();
            window.Show();
        }
        else Instance.Activate();
    }

    private async void BrowseClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open Archive", AllowMultiple = false });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) ViewModel.ArchivePath = path;
    }

    private async void OpenArchiveClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.OpenAsync(ViewModel.ArchivePath);

    private async void OpenSelClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.OpenSelectedAsync();

    private async void RunSelClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.RunSelectedAsync();

    private async void PreviewClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.PreviewSelectedAsync();

    private async void IntegrityClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.QueueIntegrityTestAsync();

    private async void BrowseExtractClicked(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Extract Destination", AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path) ViewModel.ExtractDestination = path;
    }

    private async void ExtractAllClicked(object? sender, RoutedEventArgs e) =>
        await ViewModel.QueueExtractAllAsync();

    private async void EntriesDoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (ViewModel.SelectedItem?.IsDirectory is true) ViewModel.NavigateIntoSelectedFolder();
        else await ViewModel.OpenSelectedAsync();
    }
}
