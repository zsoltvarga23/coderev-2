namespace CodeRev.Core.Models;

/// <summary>Severity/category of a review section, used for color coding.</summary>
public enum ReviewSeverity { Summary, Major, Minor, Tests, Suggestions, Other }

public sealed record ReviewSection(string Title, string Body, ReviewSeverity Severity);

/// <summary>
/// Splits a markdown review into top-level (<c>#</c>/<c>##</c>) sections and
/// classifies each by severity using Hungarian and English keyword matching, so
/// the GUI can render color-coded, collapsible cards.
/// </summary>
public static class ReviewParser
{
    public static IReadOnlyList<ReviewSection> Parse(string? markdown)
    {
        var sections = new List<ReviewSection>();
        if (string.IsNullOrWhiteSpace(markdown))
            return sections;

        string? title = null;
        var body = new List<string>();

        void Flush()
        {
            if (title is null && body.Count == 0) return;
            var text = string.Join("\n", body).Trim();
            // Skip the document's H1 task title with no content of its own.
            sections.Add(new ReviewSection(title ?? "", text, Classify(title)));
            body.Clear();
        }

        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (IsHeading(line, out var headingText))
            {
                Flush();
                title = headingText;
            }
            else
            {
                body.Add(line);
            }
        }
        Flush();

        // Drop a leading empty/title-only "Other" section (the H1), keeping real content.
        return sections
            .Where(s => !(s.Severity == ReviewSeverity.Other && string.IsNullOrWhiteSpace(s.Body) && sections.Count > 1))
            .ToList();
    }

    private static bool IsHeading(string line, out string text)
    {
        text = "";
        var t = line.TrimStart();
        if (!t.StartsWith('#')) return false;
        var i = 0;
        while (i < t.Length && t[i] == '#') i++;
        if (i == 0 || i > 6) return false;
        text = t[i..].Trim();
        return text.Length > 0;
    }

    private static ReviewSeverity Classify(string? title)
    {
        if (string.IsNullOrEmpty(title)) return ReviewSeverity.Other;
        var t = title.ToLowerInvariant();
        if (Contains(t, "összegz", "summary", "áttekint", "overview")) return ReviewSeverity.Summary;
        if (Contains(t, "fő problém", "fontos", "major", "critical", "blocker")) return ReviewSeverity.Major;
        if (Contains(t, "apró", "kisebb", "minor", "nit")) return ReviewSeverity.Minor;
        if (Contains(t, "teszt", "test")) return ReviewSeverity.Tests;
        if (Contains(t, "javasl", "suggest", "recommend")) return ReviewSeverity.Suggestions;
        return ReviewSeverity.Other;
    }

    private static bool Contains(string haystack, params string[] needles)
    {
        foreach (var n in needles)
            if (haystack.Contains(n, StringComparison.Ordinal))
                return true;
        return false;
    }
}
