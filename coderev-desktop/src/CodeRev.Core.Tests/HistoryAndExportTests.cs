using CodeRev.Core.Export;
using CodeRev.Core.History;

namespace CodeRev.Core.Tests;

public class HistoryStoreTests
{
    private static HistoryStore TempStore() =>
        new(Directory.CreateTempSubdirectory().FullName);

    [Fact]
    public void SaveListLoadRoundTrips()
    {
        var store = TempStore();
        var e = ReviewHistoryEntry.Create("feature/x", "main", "claude", "hu", 3, "## Összegzés\nok", "diff...");
        store.Save(e);

        var list = store.List();
        Assert.Single(list);
        var loaded = store.Load(e.Id);
        Assert.NotNull(loaded);
        Assert.Equal("feature/x", loaded!.Branch);
        Assert.Equal(3, loaded.ChangedFiles);
        Assert.Contains("Összegzés", loaded.ReviewMarkdown);
    }

    [Fact]
    public void ListIsNewestFirst()
    {
        var store = TempStore();
        store.Save(new ReviewHistoryEntry { Id = "a", Timestamp = DateTimeOffset.Now.AddMinutes(-10), Branch = "old" });
        store.Save(new ReviewHistoryEntry { Id = "b", Timestamp = DateTimeOffset.Now, Branch = "new" });
        var list = store.List();
        Assert.Equal("new", list[0].Branch);
        Assert.Equal("old", list[1].Branch);
    }

    [Fact]
    public void DeleteRemovesEntry()
    {
        var store = TempStore();
        var e = ReviewHistoryEntry.Create("b", "main", "codex", "hu", 1, "x", "");
        store.Save(e);
        store.Delete(e.Id);
        Assert.Empty(store.List());
        Assert.Null(store.Load(e.Id));
    }

    [Fact]
    public void CorruptEntryIsSkipped()
    {
        var store = TempStore();
        File.WriteAllText(Path.Combine(store.Directory, "bad.json"), "{ not json");
        store.Save(ReviewHistoryEntry.Create("ok", "main", "codex", "hu", 0, "x", ""));
        Assert.Single(store.List()); // the corrupt file is ignored
    }
}

public class ReviewExporterTests
{
    private static ReviewHistoryEntry Entry(string md) =>
        ReviewHistoryEntry.Create("feature/x", "main", "claude", "hu", 2, md, "diff");

    [Fact]
    public void MarkdownIncludesMetaHeaderAndBody()
    {
        var md = ReviewExporter.ToMarkdown(Entry("## Összegzés\nrendben"));
        Assert.Contains("coderev review", md);
        Assert.Contains("feature/x", md);
        Assert.Contains("## Összegzés", md);
    }

    [Fact]
    public void HtmlConvertsHeadingsAndLists()
    {
        var html = ReviewExporter.ToHtml(Entry("## Fő problémák\n- első\n- második"));
        Assert.Contains("<h2>Fő problémák</h2>", html);
        Assert.Contains("<li>első</li>", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<!doctype html>", html);
    }

    [Fact]
    public void HtmlEscapesAngleBrackets()
    {
        var html = ReviewExporter.MarkdownToHtml("Use <script> carefully & well");
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&amp;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void HtmlHandlesFencedCode()
    {
        var html = ReviewExporter.MarkdownToHtml("```\nvar x = 1;\n```");
        Assert.Contains("<pre><code>", html);
        Assert.Contains("var x = 1;", html);
        Assert.Contains("</code></pre>", html);
    }
}
