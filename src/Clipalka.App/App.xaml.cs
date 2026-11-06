using System.Windows;

namespace Clipalka.App;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length == 2 && e.Args[0] == "--verify-microphone-meter")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            System.IO.Directory.CreateDirectory(e.Args[1]);
            try
            {
                var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CLIPALKA", "settings.json");
                var settings = await new Clipalka.Core.Services.JsonSettingsStore(path).LoadAsync();
                var result = await Services.MicrophoneLevelVerification.RunAsync(settings);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "result.txt"), result);
                Shutdown(0);
            }
            catch (Exception error)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--verify-startup")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            System.IO.Directory.CreateDirectory(e.Args[1]);
            try
            {
                var result = Services.WindowsStartupVerification.Run();
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "result.txt"), result);
                Shutdown(0);
            }
            catch (Exception error)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--probe-controller")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            try
            {
                await Services.ControllerHotkeyVerification.ProbePhysicalInputAsync(e.Args[1]);
                Shutdown(0);
            }
            catch (Exception error)
            {
                System.IO.Directory.CreateDirectory(e.Args[1]);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--verify-native-audio")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            System.IO.Directory.CreateDirectory(e.Args[1]);
            try
            {
                var result = Services.NativeAudioVerification.Run();
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "result.txt"), result);
                Shutdown(0);
            }
            catch (Exception error)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--verify-input")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            System.IO.Directory.CreateDirectory(e.Args[1]);
            try
            {
                var result = Services.NativeControllerInputs.VerifyDesktopInterop() + Environment.NewLine +
                    Services.ControllerHotkeyVerification.Run() + Environment.NewLine +
                    Services.ControllerHotkeyVerification.DescribeConnectedDevices();
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "result.txt"), result);
                Shutdown(0);
            }
            catch (Exception error)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--verify-export")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            try
            {
                await Services.ReplayExportVerification.RunAsync(e.Args[1]);
                Shutdown(0);
            }
            catch (Exception error)
            {
                System.IO.Directory.CreateDirectory(e.Args[1]);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--render-ui")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            try
            {
                var window = new MainWindow();
                window.RenderUiSamples(e.Args[1]);
                Shutdown(0);
            }
            catch (Exception exception)
            {
                System.IO.Directory.CreateDirectory(e.Args[1]);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), exception.ToString());
                Shutdown(1);
            }
            return;
        }
        StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
        base.OnStartup(e);
    }
}
