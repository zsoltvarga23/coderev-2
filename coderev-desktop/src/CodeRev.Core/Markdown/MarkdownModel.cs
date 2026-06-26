namespace CodeRev.Core.Markdown;

/// <summary>Kind of a parsed markdown block.</summary>
public enum MarkdownBlockKind
{
    Paragraph,
    Heading,
    CodeBlock,
    ListItem,
}

/// <summary>
/// A styled run of text within a block. At most one emphasis applies (we do not
/// model nested bold+italic), which is sufficient for AI review output.
/// </summary>
public sealed record MarkdownInline(string Text, bool Bold = false, bool Italic = false, bool Code = false);

/// <summary>
/// A single parsed markdown block. Only the fields relevant to <see cref="Kind"/>
/// are populated. This is a UI-agnostic model so it can be unit-tested in Core;
/// the desktop app turns it into Avalonia controls.
/// </summary>
public sealed record MarkdownBlock
{
    public MarkdownBlockKind Kind { get; init; }

    /// <summary>Inline runs (Paragraph, Heading, ListItem).</summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; init; } = System.Array.Empty<MarkdownInline>();

    /// <summary>Heading level 1–6 (Heading only).</summary>
    public int HeadingLevel { get; init; }

    /// <summary>Raw, verbatim code (CodeBlock only).</summary>
    public string CodeText { get; init; } = "";

    /// <summary>Fence info string / language, may be empty (CodeBlock only).</summary>
    public string CodeLanguage { get; init; } = "";

    /// <summary>True for an ordered (numbered) list item (ListItem only).</summary>
    public bool Ordered { get; init; }

    /// <summary>The displayed number for an ordered item (ListItem only).</summary>
    public int ListNumber { get; init; }

    /// <summary>Nesting depth, 0 = top level (ListItem only).</summary>
    public int Indent { get; init; }
}
