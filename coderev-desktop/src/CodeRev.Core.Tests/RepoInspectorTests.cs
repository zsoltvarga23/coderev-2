using System.Diagnostics;
using System.Linq;
using CodeRev.Core.Engine;

namespace CodeRev.Core.Tests;

public class RepoInspectorTests : IDisposable
{
    private readonly List<string> _temps = new();

    private string Temp()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        _temps.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _temps)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* test cleanup is best-effort (git may keep handles briefly) */ }
        }
    }

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
        var dir = Temp();
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

        var dir = Temp();
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

    [Fact]
    public async Task BaseRefCandidates_IncludeLocalBranches_WhenNoRemote()
    {
        if (!GitAvailable())
            return;

        var dir = Temp();
        Git(dir, "init", "-b", "main");
        File.WriteAllText(Path.Combine(dir, "a.txt"), "x");
        Git(dir, "add", ".");
        Git(dir, "commit", "-m", "init");
        Git(dir, "branch", "develop");

        var candidates = await RepoInspector.ListBaseRefCandidatesAsync(dir);
        Assert.Contains("main", candidates);
        Assert.Contains("develop", candidates);
    }

    [Fact]
    public async Task BaseRefCandidates_RemoteBranchesComeBeforeLocal()
    {
        if (!GitAvailable())
            return;

        var remote = Temp();
        Git(remote, "init", "--bare", "-b", "main");

        var work = Temp();
        Git(work, "init", "-b", "main");
        File.WriteAllText(Path.Combine(work, "a.txt"), "x");
        Git(work, "add", ".");
        Git(work, "commit", "-m", "init");
        Git(work, "remote", "add", "origin", remote);
        Git(work, "push", "origin", "main");

        var candidates = (await RepoInspector.ListBaseRefCandidatesAsync(work)).ToList();
        Assert.Contains("origin/main", candidates);
        Assert.Contains("main", candidates);
        Assert.True(candidates.IndexOf("origin/main") < candidates.IndexOf("main"));
    }

    [Fact]
    public async Task BaseRefCandidates_RemoteDefaultComesFirst()
    {
        if (!GitAvailable())
            return;

        var remote = Temp();
        Git(remote, "init", "--bare", "-b", "main");

        var work = Temp();
        Git(work, "init", "-b", "main");
        File.WriteAllText(Path.Combine(work, "a.txt"), "x");
        Git(work, "add", ".");
        Git(work, "commit", "-m", "init");
        Git(work, "branch", "develop");
        Git(work, "remote", "add", "origin", remote);
        Git(work, "push", "origin", "main");
        Git(work, "push", "origin", "develop");
        Git(work, "remote", "set-head", "origin", "main"); // sets origin/HEAD -> origin/main

        var candidates = (await RepoInspector.ListBaseRefCandidatesAsync(work)).ToList();
        Assert.Equal("origin/main", candidates[0]); // remote default is first
        Assert.Contains("origin/develop", candidates);
    }

    [Fact]
    public async Task BaseRefCandidates_NullOrMissingPath_IsEmpty()
    {
        Assert.Empty(await RepoInspector.ListBaseRefCandidatesAsync(null));
        Assert.Empty(await RepoInspector.ListBaseRefCandidatesAsync("Z:\\nope\\nope"));
    }
}
