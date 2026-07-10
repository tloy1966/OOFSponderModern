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
            new LocalOofTemplateGenerator());
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RestoreWindowPlacement(this);
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
