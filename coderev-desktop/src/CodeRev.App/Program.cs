using Avalonia;

namespace CodeRev.App;

internal static class Program
{
    // Avalonia configuration; do not use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
