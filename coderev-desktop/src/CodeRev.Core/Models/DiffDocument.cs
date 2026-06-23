namespace CodeRev.Core.Models;

public enum DiffLineKind { Hunk, Added, Removed, Context, Meta }

/// <summary>One rendered line of a diff, with old/new line numbers where known.</summary>
public sealed record DiffLine(DiffLineKind Kind, string Text, int? OldNo, int? NewNo);

/// <summary>All changes for a single file in a diff.</summary>
public sealed class DiffFile
{
    public required string Path { get; init; }
    public List<DiffLine> Lines { get; } = new();
    public int Added { get; set; }
    public int Removed { get; set; }

    /// <summary>"path  +N -M" label for the file list.</summary>
    public string Summary => $"{Path}   +{Added} -{Removed}";
}

/// <summary>
/// Parses a unified diff into per-file line lists with line numbers, for a
/// colored, navigable diff view. File-level header noise (<c>diff --git</c>,
/// <c>index</c>, <c>---</c>, <c>+++</c>) is dropped; hunk headers and content
/// lines are kept.
/// </summary>
public static class DiffParser
{
    public static IReadOnlyList<DiffFile> Parse(string? unified)
    {
        var files = new List<DiffFile>();
        if (string.IsNullOrEmpty(unified))
            return files;

        DiffFile? current = null;
        int oldNo = 0, newNo = 0;

        foreach (var raw in unified.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                current = new DiffFile { Path = ExtractPath(raw) };
                files.Add(current);
                continue;
            }

            // File-meta header lines we render via the file header instead.
            if (raw.StartsWith("index ", StringComparison.Ordinal) ||
                raw.StartsWith("--- ", StringComparison.Ordinal) ||
                raw.StartsWith("+++ ", StringComparison.Ordinal) ||
                raw.StartsWith("new file", StringComparison.Ordinal) ||
                raw.StartsWith("deleted file", StringComparison.Ordinal) ||
                raw.StartsWith("similarity ", StringComparison.Ordinal) ||
                raw.StartsWith("rename ", StringComparison.Ordinal) ||
                raw.StartsWith("old mode", StringComparison.Ordinal) ||
                raw.StartsWith("new mode", StringComparison.Ordinal))
            {
                continue;
            }

            if (current is null)
                continue;

            if (raw.StartsWith("@@", StringComparison.Ordinal))
            {
                (oldNo, newNo) = ParseHunkHeader(raw);
                current.Lines.Add(new DiffLine(DiffLineKind.Hunk, raw, null, null));
                continue;
            }

            if (raw.StartsWith("\\", StringComparison.Ordinal)) // "\ No newline at end of file"
            {
                current.Lines.Add(new DiffLine(DiffLineKind.Meta, raw, null, null));
                continue;
            }

            if (raw.Length == 0)
                continue;

            switch (raw[0])
            {
                case '+':
                    current.Lines.Add(new DiffLine(DiffLineKind.Added, raw[1..], null, newNo));
                    current.Added++;
                    newNo++;
                    break;
                case '-':
                    current.Lines.Add(new DiffLine(DiffLineKind.Removed, raw[1..], oldNo, null));
                    current.Removed++;
                    oldNo++;
                    break;
                default: // ' ' context
                    current.Lines.Add(new DiffLine(DiffLineKind.Context, raw[1..], oldNo, newNo));
                    oldNo++;
                    newNo++;
                    break;
            }
        }

        return files;
    }

    /// <summary>Extracts the new-side path from a "diff --git a/x b/y" line.</summary>
    private static string ExtractPath(string line)
    {
        var idx = line.LastIndexOf(" b/", StringComparison.Ordinal);
        if (idx >= 0)
            return line[(idx + 3)..].Trim();
        // Fallback: take the last whitespace-separated token, stripped of a/ or b/.
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var last = parts.Length > 0 ? parts[^1] : line;
        return last.StartsWith("a/") || last.StartsWith("b/") ? last[2..] : last;
    }

    /// <summary>Parses "@@ -oldStart,oldCount +newStart,newCount @@" → (oldStart, newStart).</summary>
    private static (int oldNo, int newNo) ParseHunkHeader(string line)
    {
        // Format: @@ -a[,b] +c[,d] @@ ...
        var minus = line.IndexOf('-');
        var plus = line.IndexOf('+');
        var oldStart = ParseStart(line, minus);
        var newStart = ParseStart(line, plus);
        return (oldStart, newStart);
    }

    private static int ParseStart(string line, int signIndex)
    {
        if (signIndex < 0 || signIndex + 1 >= line.Length)
            return 1;
        var i = signIndex + 1;
        var start = i;
        while (i < line.Length && char.IsDigit(line[i])) i++;
        return start < i && int.TryParse(line.AsSpan(start, i - start), out var n) ? n : 1;
    }
}
