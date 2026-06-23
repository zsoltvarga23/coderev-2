using System.Diagnostics;
using CodeRev.Core.Engine;

namespace CodeRev.Core.Tests;

public class RepoInspectorTests
{
    private static bool GitAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "git", Arguments = "--version",
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            });
            p!.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static void Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo { FileName = "git", WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["GIT_AUTHOR_NAME"] = "t"; psi.Environment["GIT_AUTHOR_EMAIL"] = "t@t";
        psi.Environment["GIT_COMMITTER_NAME"] = "t"; psi.Environment["GIT_COMMITTER_EMAIL"] = "t@t";
        using var p = Process.Start(psi)!;
        p.WaitForExit();
    }

    [Fact]
    public async Task NonRepoDirectory_IsNotRepo_AndHasNoBranches()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        Assert.False(await RepoInspector.IsGitRepositoryAsync(dir));
        Assert.Empty(await RepoInspector.ListLocalBranchesAsync(dir));
    }

    [Fact]
    public async Task MissingDirectory_IsHandled()
    {
        Assert.False(await RepoInspector.IsGitRepositoryAsync("Z:\\nope\\nope"));
        Assert.Empty(await RepoInspector.ListLocalBranchesAsync(null));
    }

    [Fact]
    public async Task RealRepo_ListsLocalBranches()
    {
        if (!GitAvailable())
            return; // environment without git; nothing to assert

        var dir = Directory.CreateTempSubdirectory().FullName;
        Git(dir, "init", "-b", "main");
        File.WriteAllText(Path.Combine(dir, "a.txt"), "x");
        Git(dir, "add", ".");
        Git(dir, "commit", "-m", "init");
        Git(dir, "branch", "feature/login");
        Git(dir, "branch", "bugfix/auth");

        Assert.True(await RepoInspector.IsGitRepositoryAsync(dir));

        var branches = await RepoInspector.ListLocalBranchesAsync(dir);
        Assert.Contains("main", branches);
        Assert.Contains("feature/login", branches);
        Assert.Contains("bugfix/auth", branches);
    }
}
