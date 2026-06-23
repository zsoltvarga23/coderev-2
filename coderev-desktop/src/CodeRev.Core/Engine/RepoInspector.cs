using System.Diagnostics;

namespace CodeRev.Core.Engine;

/// <summary>
/// Read-only git inspection used by the GUI for convenience features: checking
/// whether a folder is a git repository and listing its local branches (for
/// branch autocomplete). It shells out to <c>git</c>; any failure yields a safe
/// empty/false result rather than throwing.
/// </summary>
public static class RepoInspector
{
    /// <summary>True if <paramref name="path"/> is inside a git work tree.</summary>
    public static async Task<bool> IsGitRepositoryAsync(string? path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;
        var (ok, output) = await RunGitAsync(path!, ct, "rev-parse", "--is-inside-work-tree");
        return ok && output.Trim() == "true";
    }

    /// <summary>Local branch names (refs/heads), or empty if not a repo.</summary>
    public static async Task<IReadOnlyList<string>> ListLocalBranchesAsync(string? path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return Array.Empty<string>();

        var (ok, output) = await RunGitAsync(path!, ct,
            "for-each-ref", "--format=%(refname:short)", "refs/heads");
        if (!ok)
            return Array.Empty<string>();

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static async Task<(bool ok, string output)> RunGitAsync(
        string workingDir, CancellationToken ct, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
                return (false, "");

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            _ = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return (process.ExitCode == 0, stdout);
        }
        catch (Exception)
        {
            return (false, ""); // git missing, cancelled, etc.
        }
    }
}
