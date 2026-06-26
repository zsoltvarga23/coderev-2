using System.Text;

namespace CodeRev.Core.Markdown;

/// <summary>
/// A small, dependency-free markdown parser covering the constructs that appear
/// in AI code reviews: ATX headings, fenced code blocks, unordered/ordered
/// lists, and inline <c>**bold**</c>, <c>*italic*</c>/<c>_italic_</c> and
/// <c>`code`</c>. It is intentionally lenient — unrecognized syntax falls back to
/// plain text rather than erroring. Output is a flat block list for the renderer.
/// </summary>
public static class MarkdownParser
{
    public static IReadOnlyList<MarkdownBlock> Parse(string? markdown)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(markdown))
            return blocks;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraph = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
                return;
            var text = string.Join(" ", paragraph).Trim();
            paragraph.Clear();
            if (text.Length > 0)
                blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Paragraph, Inlines = ParseInlines(text) });
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Fenced code block: ``` or ~~~ (3+), captured verbatim until the close.
            if (IsFence(trimmed, out var fenceChar))
            {
                FlushParagraph();
                var language = trimmed.TrimStart(fenceChar).Trim();
                var code = new List<string>();
                i++;
                for (; i < lines.Length; i++)
                {
                    if (IsFence(lines[i].TrimStart(), out var closeChar) && closeChar == fenceChar)
                        break;
                    code.Add(lines[i]);
                }
                blocks.Add(new MarkdownBlock
                {
                    Kind = MarkdownBlockKind.CodeBlock,
                    CodeText = string.Join("\n", code),
                    CodeLanguage = language,
                });
                continue;
            }

            // Blank line ends a paragraph.
            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            // ATX heading: 1–6 '#' followed by a space.
            if (TryHeading(trimmed, out var level, out var headingText))
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock
                {
                    Kind = MarkdownBlockKind.Heading,
                    HeadingLevel = level,
                    Inlines = ParseInlines(headingText),
                });
                continue;
            }

            // List item: -, *, + or "N." with leading-space-based indent.
            if (TryListItem(line, out var ordered, out var number, out var indent, out var itemText))
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock
                {
                    Kind = MarkdownBlockKind.ListItem,
                    Ordered = ordered,
                    ListNumber = number,
                    Indent = indent,
                    Inlines = ParseInlines(itemText),
                });
                continue;
            }

            paragraph.Add(trimmed);
        }

        FlushParagraph();
        return blocks;
    }

    private static bool IsFence(string trimmed, out char fenceChar)
    {
        fenceChar = '\0';
        if (trimmed.StartsWith("```", System.StringComparison.Ordinal)) { fenceChar = '`'; return true; }
        if (trimmed.StartsWith("~~~", System.StringComparison.Ordinal)) { fenceChar = '~'; return true; }
        return false;
    }

    private static bool TryHeading(string trimmed, out int level, out string text)
    {
        level = 0;
        text = "";
        var i = 0;
        while (i < trimmed.Length && trimmed[i] == '#') i++;
        if (i is < 1 or > 6 || i >= trimmed.Length || trimmed[i] != ' ')
            return false;
        level = i;
        text = trimmed[i..].Trim();
        return text.Length > 0;
    }

    private static bool TryListItem(string line, out bool ordered, out int number, out int indent, out string text)
    {
        ordered = false;
        number = 0;
        indent = 0;
        text = "";

        var leading = 0;
        while (leading < line.Length && line[leading] == ' ') leading++;
        indent = leading / 2; // 2 spaces ≈ one nesting level

        var rest = line[leading..];
        if (rest.Length >= 2 && (rest[0] is '-' or '*' or '+') && rest[1] == ' ')
        {
            text = rest[2..].Trim();
            return text.Length > 0;
        }

        // Ordered: digits followed by '.' or ')' then a space.
        var d = 0;
        while (d < rest.Length && char.IsDigit(rest[d])) d++;
        if (d > 0 && d + 1 < rest.Length && (rest[d] is '.' or ')') && rest[d + 1] == ' ')
        {
            ordered = true;
            int.TryParse(rest[..d], out number);
            text = rest[(d + 2)..].Trim();
            return text.Length > 0;
        }

        return false;
    }

    /// <summary>Splits a line into styled inline runs. Unterminated markers are
    /// treated as literal text.</summary>
    public static IReadOnlyList<MarkdownInline> ParseInlines(string text)
    {
        var runs = new List<MarkdownInline>();
        var buf = new StringBuilder();

        void FlushPlain()
        {
            if (buf.Length > 0)
            {
                runs.Add(new MarkdownInline(buf.ToString()));
                buf.Clear();
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];

            // Inline code: `code` (require ≥1 char between the backticks)
            if (c == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i + 1)
                {
                    FlushPlain();
                    runs.Add(new MarkdownInline(text[(i + 1)..end], Code: true));
                    i = end + 1;
                    continue;
                }
            }
            // Bold: **text** (require ≥1 char between the markers)
            else if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("**", i + 2, System.StringComparison.Ordinal);
                if (end > i + 2)
                {
                    FlushPlain();
                    runs.Add(new MarkdownInline(text[(i + 2)..end], Bold: true));
                    i = end + 2;
                    continue;
                }
            }
            // Italic: *text* or _text_ (require ≥1 char between the markers)
            else if (c is '*' or '_')
            {
                var end = text.IndexOf(c, i + 1);
                if (end > i + 1)
                {
                    FlushPlain();
                    runs.Add(new MarkdownInline(text[(i + 1)..end], Italic: true));
                    i = end + 1;
                    continue;
                }
            }

            buf.Append(c);
            i++;
        }

        FlushPlain();
        return runs;
    }
}
