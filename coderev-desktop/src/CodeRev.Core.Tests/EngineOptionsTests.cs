using CodeRev.Core.Engine;

namespace CodeRev.Core.Tests;

public class EngineOptionsTests
{
    [Fact]
    public void ToArguments_AlwaysJsonFormat_AndIncludesSetFlags()
    {
        var opts = new RunOptions
        {
            Branch = "feature/x",
            RepositoryPath = "/repo",
            BaseRef = "origin/develop",
            Agent = "copilot",
            Lang = "en",
            DryRun = true,
            IncludeFullFiles = true,
        };
        var args = opts.ToArguments();

        Assert.Equal("feature/x", args[0]);
        Assert.Contains("--format", args);
        Assert.Equal("json", args[args.ToList().IndexOf("--format") + 1]);
        Assert.Contains("--base-ref", args);
        Assert.Contains("origin/develop", args);
        Assert.Contains("--agent", args);
        Assert.Contains("copilot", args);
        Assert.Contains("--dry-run", args);
        Assert.Contains("--include-full-files", args);
    }

    [Fact]
    public void ToArguments_OmitsUnsetOptionalFlags()
    {
        var opts = new RunOptions { Branch = "b", RepositoryPath = "/r" };
        var args = opts.ToArguments();
        Assert.DoesNotContain("--base-ref", args);
        Assert.DoesNotContain("--out", args);
        Assert.DoesNotContain("--dry-run", args);
    }

    [Fact]
    public void BinaryLocator_PrefersEnvOverride()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(tmp, "");
        try
        {
            var env = new Dictionary<string, string?> { [BinaryLocator.EnvVar] = tmp };
            Assert.Equal(tmp, BinaryLocator.Resolve(env: env));
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void BinaryLocator_FallsBackToExecutableName()
    {
        var env = new Dictionary<string, string?>();
        var emptyDir = Directory.CreateTempSubdirectory().FullName;
        var resolved = BinaryLocator.Resolve(appDirectory: emptyDir, env: env);
        Assert.Equal(BinaryLocator.ExecutableName, resolved);
    }
}
