using System;
using Avalonia;

namespace RpaExplorer
{
    static class Program
    {
        // Command-line args are captured so the archive path passed on launch can be opened,
        // mirroring the original behaviour.
        public static string[] StartupArgs = Array.Empty<string>();

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            StartupArgs = args;
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
