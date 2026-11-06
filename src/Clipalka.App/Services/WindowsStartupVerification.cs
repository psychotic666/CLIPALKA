using Microsoft.Win32;

namespace Clipalka.App.Services;

internal static class WindowsStartupVerification
{
    public static string Run()
    {
        // Exercise real HKCU operations in an isolated key, never in Windows' Run key.
        var testKey = @"Software\CLIPALKA\Verification\Startup-" + Guid.NewGuid().ToString("N");
        try
        {
            var executable = Path.Combine(AppContext.BaseDirectory, "CLIPALKA.exe");
            var service = new WindowsStartupService(testKey, executable);
            if (service.IsEnabled()) throw new InvalidOperationException("Absent startup entry is enabled.");
            using (var key = Registry.CurrentUser.OpenSubKey(testKey))
                if (key is not null) throw new InvalidOperationException("Reading created a registry key.");
            service.SetEnabled(false);
            using (var key = Registry.CurrentUser.CreateSubKey(testKey))
            {
                key.SetValue("AnotherApp", "keep");
                key.SetValue("CLIPALKA", "\"C:\\Old installation\\CLIPALKA.exe\"");
            }
            service.SetEnabled(true);
            service.SetEnabled(true);
            using (var key = Registry.CurrentUser.OpenSubKey(testKey))
            {
                if (!service.IsEnabled() || key?.GetValue("CLIPALKA") as string != "\"" + executable + "\""
                    || key.GetValueKind("CLIPALKA") != RegistryValueKind.String)
                    throw new InvalidOperationException("Startup executable or quoting is incorrect.");
            }
            service.SetEnabled(false);
            service.SetEnabled(false);
            using (var key = Registry.CurrentUser.OpenSubKey(testKey))
                if (service.IsEnabled() || key?.GetValue("AnotherApp") as string != "keep")
                    throw new InvalidOperationException("Disabling startup affected another application's entry.");
            return "PASS: per-user startup enable/disable, quoted executable path, stale-path replacement, idempotency and preservation of other entries; Windows Run key was not changed.";
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(testKey, throwOnMissingSubKey: false); }
    }
}
