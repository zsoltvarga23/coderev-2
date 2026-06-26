using CodeRev.Core.History;

namespace CodeRev.Core.Tests;

public class RecentRepositoriesStoreTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    private RecentRepositoriesStore NewStore(out string file)
    {
        var dir = Path.Combine(Path.GetTempPath(), "coderev-recent-" + Guid.NewGuid().ToString("N"));
        _tempDirs.Add(dir);
        file = Path.Combine(dir, "recent.json");
        return new RecentRepositoriesStore(file);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* test cleanup is best-effort */ }
        }
    }

    [Fact]
    public void AddPutsNewestFirstAndDerivesName()
    {
        var store = NewStore(out _);
        var a = Path.Combine(Path.GetTempPath(), "repoA");
        var b = Path.Combine(Path.GetTempPath(), "repoB");
        store.Add(a);
        store.Add(b);

        var list = store.List();
        Assert.Equal(2, list.Count);
        Assert.Equal("repoB", list[0].Name); // most recent first
        Assert.Equal("repoA", list[1].Name);
    }

    [Fact]
    public void AddDeduplicatesAndMovesToFront()
    {
        var store = NewStore(out _);
        var a = Path.Combine(Path.GetTempPath(), "repoA");
        var b = Path.Combine(Path.GetTempPath(), "repoB");
        store.Add(a);
        store.Add(b);
        store.Add(a); // re-open A

        var list = store.List();
        Assert.Equal(2, list.Count);
        Assert.Equal("repoA", list[0].Name);
    }

    [Fact]
    public void TrailingSeparatorAndCaseDoNotCreateDuplicates()
    {
        // De-duplication is intentionally case-insensitive (OrdinalIgnoreCase) on
        // ALL platforms — even though Linux filesystems are case-sensitive, repo
        // paths differing only by case are treated as the same entry by design.
        var store = NewStore(out _);
        var p = Path.Combine(Path.GetTempPath(), "RepoX");
        store.Add(p);
        store.Add(p + Path.DirectorySeparatorChar);   // trailing slash
        store.Add(p.ToUpperInvariant());              // different case

        Assert.Single(store.List());
    }

    [Fact]
    public void CapsAtTwentyEntries()
    {
        var store = NewStore(out _);
        for (var i = 0; i < 25; i++)
            store.Add(Path.Combine(Path.GetTempPath(), "repo" + i));

        Assert.Equal(20, store.List().Count);
        Assert.Equal("repo24", store.List()[0].Name); // newest kept
    }

    [Fact]
    public void RemoveDropsEntry()
    {
        var store = NewStore(out _);
        var a = Path.Combine(Path.GetTempPath(), "repoA");
        store.Add(a);
        store.Remove(a);
        Assert.Empty(store.List());
    }

    [Fact]
    public void EmptyOrWhitespacePathIsIgnored()
    {
        var store = NewStore(out _);
        store.Add("");
        store.Add("   ");
        Assert.Empty(store.List());
    }
}
