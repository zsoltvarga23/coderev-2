using Avalonia;
using Velopack;

namespace CodeRev.App;

internal static class Program
{
    // Avalonia configuration; do not use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: Velopack handles install/update/uninstall hooks (e.g.
        // first-run setup, --squirrel-* events) and exits early when invoked by
        // the updater. Safe to call in dev builds too (it just no-ops).
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
