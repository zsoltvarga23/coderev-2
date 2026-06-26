using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using CodeRev.Core.Markdown;

namespace CodeRev.App.Controls;

/// <summary>
/// Renders a markdown string as native Avalonia controls — no third-party
/// packages. Parsing lives in <see cref="MarkdownParser"/> (Core); this turns the
/// resulting blocks into headings, formatted paragraphs, list items and
/// monospace code blocks. Bind via <c>&lt;ctrl:MarkdownView Markdown="{Binding …}"/&gt;</c>.
/// </summary>
public class MarkdownView : ContentControl
{
    private static readonly FontFamily Mono = new("Cascadia Code,Consolas,Menlo,monospace");

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    static MarkdownView()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Rebuild());
    }

    private void Rebuild()
    {
        var panel = new StackPanel { Spacing = 6 };
        foreach (var block in MarkdownParser.Parse(Markdown ?? ""))
            panel.Children.Add(RenderBlock(block));
        Content = panel;
    }

    // Instance (not static) so they can resolve theme-aware brushes from this
    // control's resource scope via GetResourceObservable — the code colors then
    // track light/dark like the diff colors do.
    private Control RenderBlock(MarkdownBlock block) => block.Kind switch
    {
        MarkdownBlockKind.CodeBlock => RenderCodeBlock(block),
        MarkdownBlockKind.Heading => RenderHeading(block),
        MarkdownBlockKind.ListItem => RenderListItem(block),
        _ => BuildTextBlock(block.Inlines),
    };

    private Control RenderHeading(MarkdownBlock block)
    {
        var tb = BuildTextBlock(block.Inlines);
        tb.FontWeight = FontWeight.Bold;
        tb.FontSize = block.HeadingLevel <= 1 ? 18 : block.HeadingLevel == 2 ? 16 : 14;
        tb.Margin = new Thickness(0, 4, 0, 0);
        return tb;
    }

    private Control RenderListItem(MarkdownBlock block)
    {
        var prefix = block.Ordered ? $"{block.ListNumber}. " : "•  ";
        var tb = BuildTextBlock(block.Inlines, prefix);
        tb.Margin = new Thickness(8 + block.Indent * 16, 0, 0, 0);
        return tb;
    }

    private Control RenderCodeBlock(MarkdownBlock block)
    {
        var code = new SelectableTextBlock
        {
            Text = block.CodeText,
            FontFamily = Mono,
            FontSize = 12.5,
            TextWrapping = TextWrapping.NoWrap,
        };
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = code,
            },
        };
        border.Bind(Border.BackgroundProperty, this.GetResourceObservable("CodeBlockBg"));
        border.Bind(Border.BorderBrushProperty, this.GetResourceObservable("CodeBlockBorder"));
        return border;
    }

    private SelectableTextBlock BuildTextBlock(IReadOnlyList<MarkdownInline> inlines, string? prefix = null)
    {
        var tb = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        if (!string.IsNullOrEmpty(prefix))
            tb.Inlines!.Add(new Run(prefix));

        foreach (var inline in inlines)
        {
            if (inline.Code)
            {
                var codeRun = new Run(inline.Text) { FontFamily = Mono };
                codeRun.Bind(TextElement.BackgroundProperty, this.GetResourceObservable("CodeInlineBg"));
                tb.Inlines!.Add(codeRun);
            }
            else
            {
                var run = new Run(inline.Text);
                if (inline.Bold) run.FontWeight = FontWeight.Bold;
                if (inline.Italic) run.FontStyle = FontStyle.Italic;
                tb.Inlines!.Add(run);
            }
        }
        return tb;
    }
}
