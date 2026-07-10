using System.Windows;
using OOFSponderModern.Services;
using OOFSponderModern.ViewModels;

namespace OOFSponderModern;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(
            new FileSettingsService(),
            new SchedulerService(),
            new GraphMailboxSettingsClient(),
            new LocalOofTemplateGenerator(),
            new MessageTemplateRenderer(),
            new GitHubReleaseUpdateService());
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RestoreWindowPlacement(this);
        }

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SaveWindowPlacement(this);
        }

        base.OnClosing(e);
    }
}
