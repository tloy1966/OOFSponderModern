using Microsoft.Win32;

namespace OOFSponderModern.Services;

public sealed class WindowsStartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OOFSponderModern";
    private readonly string _startupCommand;

    public WindowsStartupService()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The application executable path is unavailable.");
        _startupCommand = $"\"{executablePath}\"";
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return string.Equals(key?.GetValue(ValueName) as string, _startupCommand, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("The Windows startup settings could not be opened.");

        if (enabled)
        {
            key.SetValue(ValueName, _startupCommand, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}