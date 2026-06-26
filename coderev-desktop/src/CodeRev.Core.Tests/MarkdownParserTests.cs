using CodeRev.Core.Markdown;

namespace CodeRev.Core.Tests;

public class MarkdownParserTests
{
    [Fact]
    public void ParsesFencedCodeBlockVerbatimWithLanguage()
    {
        var md = "Intro\n\n```yaml\nkey: value\nlist:\n  - a\n```\n\nOutro";
        var blocks = MarkdownParser.Parse(md);

        var code = blocks.Single(b => b.Kind == MarkdownBlockKind.CodeBlock);
        Assert.Equal("yaml", code.CodeLanguage);
        Assert.Equal("key: value\nlist:\n  - a", code.CodeText);
        // The fence markers themselves never leak into text blocks.
        Assert.DoesNotContain(blocks, b =>
            b.Inlines.Any(i => i.Text.Contains("```")));
    }

    [Fact]
    public void ParsesHeadingLevels()
    {
        var blocks = MarkdownParser.Parse("# One\n## Two\n### Three\n");
        Assert.Collection(blocks.Where(b => b.Kind == MarkdownBlockKind.Heading),
            b => Assert.Equal(1, b.HeadingLevel),
            b => Assert.Equal(2, b.HeadingLevel),
            b => Assert.Equal(3, b.HeadingLevel));
    }

    [Fact]
    public void ParsesUnorderedAndOrderedListItems()
    {
        var blocks = MarkdownParser.Parse("- first\n- second\n1. one\n2. two\n");
        var items = blocks.Where(b => b.Kind == MarkdownBlockKind.ListItem).ToList();
        Assert.Equal(4, items.Count);
        Assert.False(items[0].Ordered);
        Assert.True(items[2].Ordered);
        Assert.Equal(1, items[2].ListNumber);
        Assert.Equal(2, items[3].ListNumber);
    }

    [Fact]
    public void NestedListItemGetsIndent()
    {
        var blocks = MarkdownParser.Parse("- top\n  - nested\n");
        var items = blocks.Where(b => b.Kind == MarkdownBlockKind.ListItem).ToList();
        Assert.Equal(0, items[0].Indent);
        Assert.Equal(1, items[1].Indent);
    }

    [Fact]
    public void ParsesInlineStyles()
    {
        var runs = MarkdownParser.ParseInlines("plain **bold** and *italic* and `code` end");
        Assert.Contains(runs, r => r.Bold && r.Text == "bold");
        Assert.Contains(runs, r => r.Italic && r.Text == "italic");
        Assert.Contains(runs, r => r.Code && r.Text == "code");
        Assert.Contains(runs, r => !r.Bold && !r.Italic && !r.Code && r.Text.Contains("plain"));
    }

    [Fact]
    public void UnterminatedMarkerIsLiteral()
    {
        var runs = MarkdownParser.ParseInlines("a * b without close");
        Assert.Single(runs);
        Assert.Equal("a * b without close", runs[0].Text);
        Assert.False(runs[0].Italic);
    }

    [Fact]
    public void ParagraphsJoinWrappedLinesAndSplitOnBlank()
    {
        var blocks = MarkdownParser.Parse("line one\nline two\n\nsecond para");
        var paras = blocks.Where(b => b.Kind == MarkdownBlockKind.Paragraph).ToList();
        Assert.Equal(2, paras.Count);
        Assert.Equal("line one line two", string.Concat(paras[0].Inlines.Select(i => i.Text)));
    }

    [Fact]
    public void EmptyInputYieldsNoBlocks()
    {
        Assert.Empty(MarkdownParser.Parse(""));
        Assert.Empty(MarkdownParser.Parse(null));
    }
}
