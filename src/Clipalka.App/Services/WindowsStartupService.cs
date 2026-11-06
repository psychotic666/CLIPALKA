using Microsoft.Win32;

namespace Clipalka.App.Services;

internal sealed class WindowsStartupService(
    string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
    string? executablePath = null)
{
    private const string ValueName = "CLIPALKA";
    private string ExecutablePath => executablePath ?? Path.Combine(AppContext.BaseDirectory, "CLIPALKA.exe");

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(registryPath);
        return key?.GetValue(ValueName) is string command && !string.IsNullOrWhiteSpace(command);
    }

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var path = Path.GetFullPath(ExecutablePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Не удалось включить автозапуск: файл CLIPALKA.exe не найден.", path);
        using var runKey = Registry.CurrentUser.CreateSubKey(registryPath, writable: true);
        runKey.SetValue(ValueName, "\"" + path + "\"", RegistryValueKind.String);
    }
}
