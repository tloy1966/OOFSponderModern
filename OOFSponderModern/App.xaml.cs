using System.Threading;
using System.Windows;

namespace OOFSponderModern;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	private const string SingleInstanceMutexName = "Local\\OOFSponderModern.SingleInstance";
	private Mutex? _singleInstanceMutex;
	private bool _ownsSingleInstanceMutex;

	protected override void OnStartup(StartupEventArgs e)
	{
		_singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsSingleInstanceMutex);
		if (!_ownsSingleInstanceMutex)
		{
			MessageBox.Show(
				"OOFSponderModern is already running.",
				"OOFSponderModern",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
			_singleInstanceMutex.Dispose();
			_singleInstanceMutex = null;
			Shutdown();
			return;
		}

		base.OnStartup(e);
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (_ownsSingleInstanceMutex)
		{
			_singleInstanceMutex?.ReleaseMutex();
		}

		_singleInstanceMutex?.Dispose();
		base.OnExit(e);
	}
}

