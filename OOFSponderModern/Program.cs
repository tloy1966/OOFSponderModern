using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace OOFSponderModern;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\OOFSponderModern.SingleInstance";
    private const int ShowWindowRestore = 9;

    [STAThread]
    public static void Main()
    {
        using var singleInstanceMutex = AcquireSingleInstanceMutex();
        if (singleInstanceMutex is null)
        {
            return;
        }

        var app = new App
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };
        app.InitializeComponent();

        var mainWindow = new MainWindow();
        app.Run(mainWindow);
    }

    private static Mutex? AcquireSingleInstanceMutex()
    {
        var mutex = new Mutex(true, SingleInstanceMutexName, out var ownsMutex);
        if (ownsMutex)
        {
            return mutex;
        }

        if (TryActivateExistingWindow())
        {
            mutex.Dispose();
            return null;
        }

        TerminateStaleInstances();
        mutex.Dispose();

        mutex = new Mutex(true, SingleInstanceMutexName, out ownsMutex);
        if (ownsMutex)
        {
            return mutex;
        }

        MessageBox.Show(
            "OOFSponderModern is already running, but no visible window could be activated. Please close it from Task Manager and try again.",
            "OOFSponderModern",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        mutex.Dispose();
        return null;
    }

    private static bool TryActivateExistingWindow()
    {
        foreach (var process in GetPeerProcesses())
        {
            if (process.MainWindowHandle == IntPtr.Zero)
            {
                continue;
            }

            ShowWindow(process.MainWindowHandle, ShowWindowRestore);
            SetForegroundWindow(process.MainWindowHandle);
            return true;
        }

        return false;
    }

    private static void TerminateStaleInstances()
    {
        foreach (var process in GetPeerProcesses().Where(process => process.MainWindowHandle == IntPtr.Zero))
        {
            try
            {
                process.Kill();
                process.WaitForExit(2000);
            }
            catch
            {
            }
        }
    }

    private static IEnumerable<Process> GetPeerProcesses()
    {
        var current = Process.GetCurrentProcess();
        var currentPath = GetProcessPath(current);
        return Process.GetProcessesByName(current.ProcessName)
            .Where(process => process.Id != current.Id)
            .Where(process => string.Equals(GetProcessPath(process), currentPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}