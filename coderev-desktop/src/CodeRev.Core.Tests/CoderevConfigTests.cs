using CodeRev.Core.Config;

namespace CodeRev.Core.Tests;

public class CoderevConfigTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var cfg = CoderevConfig.Load(dir);
        Assert.Equal("origin/main", cfg.BaseRef);
        Assert.Equal("codex", cfg.Agent);
        Assert.Equal(20, cfg.ContextLines);
        Assert.Equal("en", cfg.Lang);
        Assert.Empty(cfg.ObeyDoc);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var cfg = new CoderevConfig
        {
            BaseRef = "origin/develop",
            Agent = "claude",
            Lang = "en",
            ContextLines = 30,
            IncludeFullFiles = true,
            ObeyDoc = new() { "STYLE.md", "API.md" },
            Out = "review.md",
        };
        var path = cfg.Save(dir);
        Assert.True(File.Exists(path));

        var loaded = CoderevConfig.Load(dir);
        Assert.Equal("origin/develop", loaded.BaseRef);
        Assert.Equal("claude", loaded.Agent);
        Assert.Equal("en", loaded.Lang);
        Assert.Equal(30, loaded.ContextLines);
        Assert.True(loaded.IncludeFullFiles);
        Assert.Equal(new[] { "STYLE.md", "API.md" }, loaded.ObeyDoc);
        Assert.Equal("review.md", loaded.Out);
    }

    [Fact]
    public void UsesKebabCaseKeys()
    {
        var json = new CoderevConfig { ContextLines = 7 }.ToJson();
        Assert.Contains("\"base-ref\"", json);
        Assert.Contains("\"include-full-files\"", json);
        Assert.Contains("\"context-lines\": 7", json);
        Assert.Contains("\"snippet-max-chars\"", json);
    }

    [Fact]
    public void ReadsGoStyleFile()
    {
        // A file as written by `coderev init` (kebab-case, obey-doc array).
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(CoderevConfig.PathFor(dir),
            """
            {
              "obey-doc": ["CONTRIBUTING.md"],
              "base-ref": "origin/develop",
              "agent": "copilot",
              "context-lines": 15,
              "lang": "en"
            }
            """);
        var cfg = CoderevConfig.Load(dir);
        Assert.Equal(new[] { "CONTRIBUTING.md" }, cfg.ObeyDoc);
        Assert.Equal("origin/develop", cfg.BaseRef);
        Assert.Equal("copilot", cfg.Agent);
        Assert.Equal(15, cfg.ContextLines);
    }

    [Fact]
    public void AgentConfig_ObjectIsReadAsRawJson_AndOmittedWhenNull()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(CoderevConfig.PathFor(dir),
            """
            { "agent-config": {"name":"mycli","cmd":["mycli","--in","{prompt_file}"],"mode":"file"} }
            """);
        var cfg = CoderevConfig.Load(dir);
        Assert.NotNull(cfg.AgentConfig);
        Assert.Contains("mycli", cfg.AgentConfig!);
        Assert.Contains("\"mode\"", cfg.AgentConfig!);

        // Round-trips back out as a real JSON object, and is omitted when null.
        var withAgent = cfg.ToJson();
        Assert.Contains("\"agent-config\"", withAgent);
        Assert.Contains("\"name\": \"mycli\"", withAgent);

        cfg.AgentConfig = null;
        Assert.DoesNotContain("agent-config", cfg.ToJson());
    }
}
