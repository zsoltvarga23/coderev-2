using CodeRev.Core.Models;

namespace CodeRev.Core.Tests;

public class ReviewParserTests
{
    private const string Hungarian =
        "# Feladat: PR review\n\n" +
        "## Összegzés\n" +
        "A bejelentkezés refaktorálása rendben.\n\n" +
        "## Fő problémák\n" +
        "- A getHash null esetét nem kezeli.\n\n" +
        "## Apró problémák\n" +
        "- Elgépelés a kommentben.\n\n" +
        "## Tesztek\n" +
        "Nincs új teszt.\n\n" +
        "## Javaslatok\n" +
        "- Adj hozzá unit tesztet.\n";

    [Fact]
    public void ParsesSectionsAndSeverities()
    {
        var sections = ReviewParser.Parse(Hungarian);

        Assert.Equal(ReviewSeverity.Summary, Find(sections, "Összegzés").Severity);
        Assert.Equal(ReviewSeverity.Major, Find(sections, "Fő problémák").Severity);
        Assert.Equal(ReviewSeverity.Minor, Find(sections, "Apró problémák").Severity);
        Assert.Equal(ReviewSeverity.Tests, Find(sections, "Tesztek").Severity);
        Assert.Equal(ReviewSeverity.Suggestions, Find(sections, "Javaslatok").Severity);
    }

    [Fact]
    public void CapturesBody()
    {
        var sections = ReviewParser.Parse(Hungarian);
        Assert.Contains("null esetét", Find(sections, "Fő problémák").Body);
    }

    [Fact]
    public void DropsEmptyH1TitleSection()
    {
        var sections = ReviewParser.Parse(Hungarian);
        // The "# Feladat: PR review" H1 has no body and should be dropped.
        Assert.DoesNotContain(sections, s => s.Title.StartsWith("Feladat"));
    }

    [Fact]
    public void EnglishHeadingsClassified()
    {
        var md = "## Summary\nok\n## Major Issues\nbug\n## Suggestions\ndo x\n";
        var sections = ReviewParser.Parse(md);
        Assert.Equal(ReviewSeverity.Summary, Find(sections, "Summary").Severity);
        Assert.Equal(ReviewSeverity.Major, Find(sections, "Major Issues").Severity);
        Assert.Equal(ReviewSeverity.Suggestions, Find(sections, "Suggestions").Severity);
    }

    [Fact]
    public void UnknownHeadingIsOther()
    {
        var sections = ReviewParser.Parse("## Random Notes\nstuff\n");
        Assert.Equal(ReviewSeverity.Other, sections[0].Severity);
    }

    [Fact]
    public void EmptyInputYieldsNoSections()
    {
        Assert.Empty(ReviewParser.Parse(""));
        Assert.Empty(ReviewParser.Parse(null));
    }

    private static ReviewSection Find(IReadOnlyList<ReviewSection> s, string titleContains) =>
        s.First(x => x.Title.Contains(titleContains, StringComparison.Ordinal));
}
