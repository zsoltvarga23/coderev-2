using Velopack;
using Velopack.Sources;

namespace CodeRev.App.Services;

/// <summary>
/// Thin wrapper over Velopack's <see cref="UpdateManager"/> for in-app updates.
/// Updates are pulled from the project's GitHub Releases (the same releases the
/// installer was built from). Override the source with the
/// <c>CODEREV_UPDATE_URL</c> environment variable (e.g. a fork or a mirror).
///
/// In a non-installed build (plain <c>dotnet run</c> / portable extract),
/// <see cref="IsInstalled"/> is false and update calls are no-ops, so the UI can
/// safely show a disabled / "dev build" state instead of crashing.
/// </summary>
public sealed class UpdateService
{
    /// <summary>Default update feed: this project's GitHub repository.</summary>
    public const string DefaultRepoUrl = "https://github.com/zsoltvarga23/coderev-2";

    private readonly UpdateManager _mgr;

    public UpdateService(string? repoUrl = null)
    {
        var url = Environment.GetEnvironmentVariable("CODEREV_UPDATE_URL");
        if (string.IsNullOrWhiteSpace(url))
            url = repoUrl ?? DefaultRepoUrl;

        // prerelease: false -> only stable (non-draft, non-prerelease) releases.
        _mgr = new UpdateManager(new GithubSource(url, null, prerelease: false));
    }

    /// <summary>True when running from a real Velopack install (updates possible).</summary>
    public bool IsInstalled => _mgr.IsInstalled;

    /// <summary>The installed version, or null for an un-packaged dev build.</summary>
    public string? CurrentVersion => _mgr.CurrentVersion?.ToString();

    /// <summary>
    /// Checks the feed for a newer stable release. Returns null when up to date
    /// or when not running as an installed app.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!_mgr.IsInstalled)
            return null;
        return await _mgr.CheckForUpdatesAsync();
    }

    /// <summary>Downloads the given update; <paramref name="progress"/> reports 0–100.</summary>
    public Task DownloadAsync(UpdateInfo update, Action<int>? progress = null, CancellationToken ct = default)
        => _mgr.DownloadUpdatesAsync(update, progress, ct);

    /// <summary>
    /// Applies the downloaded update and relaunches the app. The current process
    /// exits immediately, so call this last (after the user confirms).
    /// </summary>
    public void ApplyAndRestart(UpdateInfo update)
        => _mgr.ApplyUpdatesAndRestart(update.TargetFullRelease);
}
