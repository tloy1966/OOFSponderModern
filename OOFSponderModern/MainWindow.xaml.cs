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
            new InMemorySettingsService(),
            new SchedulerService(),
            new GraphMailboxSettingsClient(),
            new LocalOofTemplateGenerator());
    }
}
