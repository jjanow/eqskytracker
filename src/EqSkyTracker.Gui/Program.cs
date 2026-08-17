using Avalonia;
using System;
using Velopack;

namespace EqSkyTracker.Gui;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run before anything else: handles Velopack's install/update/uninstall
        // hooks, some of which call Environment.Exit and never return.
        VelopackApp.Build().Run();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--dir")
            {
                App.InitialDir = args[i + 1];
                break;
            }
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
