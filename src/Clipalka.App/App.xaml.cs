using System.Windows;

namespace Clipalka.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length == 2 && e.Args[0] == "--render-ui")
        {
            StartupUri = null;
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
        base.OnStartup(e);
    }
}
