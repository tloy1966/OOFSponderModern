using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            new GitHubReleaseUpdateService(),
            new WindowsStartupService());
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
        if (Keyboard.FocusedElement is TextBox focusedTextBox)
        {
            focusedTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SaveWindowPlacement(this);
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
