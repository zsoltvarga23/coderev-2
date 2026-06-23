using System.Text;
using CodeRev.Core.History;

namespace CodeRev.Core.Export;

/// <summary>
/// Exports a review to Markdown or a self-contained HTML document. The HTML
/// converter handles the constructs the review prompt asks for (headings,
/// bullet lists, fenced code) and HTML-escapes everything else, so output from
/// any agent renders safely.
/// </summary>
public static class ReviewExporter
{
    /// <summary>Returns the review as Markdown, with a small metadata header.</summary>
    public static string ToMarkdown(ReviewHistoryEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append("<!-- coderev review · ").Append(entry.Branch)
          .Append(" vs ").Append(entry.Base)
          .Append(" · ").Append(entry.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm"))
          .Append(" -->\n\n");
        sb.Append(entry.ReviewMarkdown.TrimEnd());
        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>Wraps the converted review in a styled, standalone HTML page.</summary>
    public static string ToHtml(ReviewHistoryEntry entry)
    {
        var title = $"coderev review — {entry.Branch}";
        var body = MarkdownToHtml(entry.ReviewMarkdown);
        return $$"""
            <!doctype html>
            <html lang="{{entry.Lang}}">
            <head>
            <meta charset="utf-8">
            <title>{{Escape(title)}}</title>
            <style>
              body{font-family:system-ui,Segoe UI,sans-serif;max-width:820px;margin:2rem auto;padding:0 1rem;line-height:1.5}
              h1,h2,h3{border-bottom:1px solid #eee;padding-bottom:.2em}
              code,pre{font-family:Cascadia Code,Consolas,monospace;background:#f6f8fa}
              pre{padding:.8em;overflow:auto;border-radius:6px}
              .meta{color:#666;font-size:.9em}
            </style>
            </head>
            <body>
            <p class="meta">{{Escape($"{entry.Branch} vs {entry.Base} · {entry.Timestamp.LocalDateTime:yyyy-MM-dd HH:mm} · agent: {entry.Agent}")}}</p>
            {{body}}
            </body>
            </html>
            """;
    }

    /// <summary>Minimal, dependency-free Markdown to HTML conversion.</summary>
    public static string MarkdownToHtml(string markdown)
    {
        var sb = new StringBuilder();
        var inList = false;
        var inCode = false;

        void CloseList()
        {
            if (inList) { sb.Append("</ul>\n"); inList = false; }
        }

        foreach (var raw in (markdown ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw;

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode) { sb.Append("</code></pre>\n"); inCode = false; }
                else { CloseList(); sb.Append("<pre><code>"); inCode = true; }
                continue;
            }
            if (inCode)
            {
                sb.Append(Escape(line)).Append('\n');
                continue;
            }

            var trimmed = line.TrimStart();

            // Headings: #..###### followed by a space.
            var hashes = 0;
            while (hashes < trimmed.Length && trimmed[hashes] == '#') hashes++;
            if (hashes is >= 1 and <= 6 && hashes < trimmed.Length && trimmed[hashes] == ' ')
            {
                CloseList();
                var text = Escape(trimmed[(hashes + 1)..].Trim());
                sb.Append("<h").Append(hashes).Append('>').Append(text)
                  .Append("</h").Append(hashes).Append(">\n");
                continue;
            }

            // Unordered list items.
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                if (!inList) { sb.Append("<ul>\n"); inList = true; }
                sb.Append("<li>").Append(Escape(trimmed[2..].Trim())).Append("</li>\n");
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                CloseList();
                continue;
            }

            CloseList();
            sb.Append("<p>").Append(Escape(line.Trim())).Append("</p>\n");
        }

        if (inCode) sb.Append("</code></pre>\n");
        CloseList();
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
