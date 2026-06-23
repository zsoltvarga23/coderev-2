using System.Runtime.InteropServices;

namespace CodeRev.Core.Engine;

/// <summary>
/// Locates the coderev engine binary. Resolution order:
/// <list type="number">
///   <item>the <c>CODEREV_BIN</c> environment variable (explicit override),</item>
///   <item>a binary bundled next to the application (version-pinned),</item>
///   <item>the executable name on <c>PATH</c>.</item>
/// </list>
/// Bundling a pinned engine avoids version drift while still letting power users
/// point at their own build via the environment variable.
/// </summary>
public static class BinaryLocator
{
    public const string EnvVar = "CODEREV_BIN";

    public static string ExecutableName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "coderev.exe" : "coderev";

    /// <summary>
    /// Returns the path to use for the engine. If a bundled or override path
    /// exists it is returned; otherwise the bare executable name is returned so
    /// the OS resolves it from PATH.
    /// </summary>
    public static string Resolve(string? appDirectory = null, IDictionary<string, string?>? env = null)
    {
        var getEnv = env is null
            ? Environment.GetEnvironmentVariable
            : (Func<string, string?>)(k => env.TryGetValue(k, out var v) ? v : null);

        var overridePath = getEnv(EnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath!;

        appDirectory ??= AppContext.BaseDirectory;
        var bundled = Path.Combine(appDirectory, ExecutableName);
        if (File.Exists(bundled))
            return bundled;

        return ExecutableName; // fall back to PATH lookup
    }
}
