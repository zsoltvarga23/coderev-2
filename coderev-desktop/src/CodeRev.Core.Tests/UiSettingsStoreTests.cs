using CodeRev.Core.Settings;

namespace CodeRev.Core.Tests;

public class UiSettingsStoreTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private UiSettingsStore NewStore(out string file)
    {
        var dir = Path.Combine(Path.GetTempPath(), "coderev-ui-" + Guid.NewGuid().ToString("N"));
        _tempDirs.Add(dir);
        file = Path.Combine(dir, "ui-settings.json");
        return new UiSettingsStore(file);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void RoundTripsTheme()
    {
        var store = NewStore(out _);
        store.Save(new UiSettings { Theme = "retro" });
        Assert.Equal("retro", store.Load().Theme);
    }

    [Fact]
    public void MissingFileYieldsDefaults()
    {
        var store = NewStore(out _);
        Assert.Equal("", store.Load().Theme);
    }

    [Fact]
    public void CorruptFileYieldsDefaults()
    {
        var store = NewStore(out var file);
        File.WriteAllText(file, "{ not json !!");
        Assert.Equal("", store.Load().Theme);
    }
}
