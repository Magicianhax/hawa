using Microsoft.Win32;

namespace Hawa.App;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hawa";

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static bool IsSet()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }
}
