using CodeRev.Core.Models;

namespace CodeRev.Core.Tests;

public class DiffParserTests
{
    private const string Sample =
        "diff --git a/auth.js b/auth.js\n" +
        "index 111..222 100644\n" +
        "--- a/auth.js\n" +
        "+++ b/auth.js\n" +
        "@@ -1,4 +1,5 @@\n" +
        " function login(user, pass) {\n" +
        "-  if (pass == \"admin\") return true;\n" +
        "-  return checkPassword(user, pass);\n" +
        "+  if (!user || !pass) return false;\n" +
        "+  return bcrypt.compareSync(pass, getHash(user));\n" +
        "+  // done\n" +
        " }\n" +
        "diff --git a/hash.js b/hash.js\n" +
        "new file mode 100644\n" +
        "index 000..333\n" +
        "--- /dev/null\n" +
        "+++ b/hash.js\n" +
        "@@ -0,0 +1,2 @@\n" +
        "+export function getHash(user) {\n" +
        "+  return db.users.find(user).passwordHash;\n";

    [Fact]
    public void SplitsByFile()
    {
        var files = DiffParser.Parse(Sample);
        Assert.Equal(2, files.Count);
        Assert.Equal("auth.js", files[0].Path);
        Assert.Equal("hash.js", files[1].Path);
    }

    [Fact]
    public void CountsAddedAndRemoved()
    {
        var files = DiffParser.Parse(Sample);
        Assert.Equal(3, files[0].Added);
        Assert.Equal(2, files[0].Removed);
        Assert.Equal(2, files[1].Added);
        Assert.Equal(0, files[1].Removed);
    }

    [Fact]
    public void AssignsLineNumbers()
    {
        var auth = DiffParser.Parse(Sample)[0];
        // First content line is context "function login" at old/new line 1.
        var ctx = auth.Lines.First(l => l.Kind == DiffLineKind.Context);
        Assert.Equal(1, ctx.OldNo);
        Assert.Equal(1, ctx.NewNo);

        var firstAdded = auth.Lines.First(l => l.Kind == DiffLineKind.Added);
        Assert.Null(firstAdded.OldNo);
        Assert.Equal(2, firstAdded.NewNo); // after the single context line

        var firstRemoved = auth.Lines.First(l => l.Kind == DiffLineKind.Removed);
        Assert.Equal(2, firstRemoved.OldNo);
        Assert.Null(firstRemoved.NewNo);
    }

    [Fact]
    public void StripsLeadingMarkerFromText()
    {
        var auth = DiffParser.Parse(Sample)[0];
        var added = auth.Lines.First(l => l.Kind == DiffLineKind.Added);
        Assert.StartsWith("  if (!user", added.Text); // '+' marker removed
    }

    [Fact]
    public void DropsFileMetaHeaders()
    {
        var auth = DiffParser.Parse(Sample)[0];
        Assert.DoesNotContain(auth.Lines, l => l.Text.StartsWith("index "));
        Assert.DoesNotContain(auth.Lines, l => l.Text.StartsWith("--- "));
        Assert.Contains(auth.Lines, l => l.Kind == DiffLineKind.Hunk);
    }

    [Fact]
    public void EmptyInputYieldsNoFiles()
    {
        Assert.Empty(DiffParser.Parse(""));
        Assert.Empty(DiffParser.Parse(null));
    }
}
