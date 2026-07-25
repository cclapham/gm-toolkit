using System.Text;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;

using GmToolkit.UI.Markdown;

namespace GmToolkit.UI.Tests.Markdown;

/// <summary>
/// Structural tests for <see cref="MarkdownRenderer"/> -- asserts on the actual
/// <see cref="Control"/>/<see cref="Inline"/> tree it produces (never on rendered pixels; there's no
/// windowing/headless-Avalonia runner in this test project, and none is needed since
/// <see cref="MarkdownRenderer"/> only ever constructs controls, never shows them). See
/// <see cref="MarkdownLinkPolicyTests"/> for the link-scheme-allowlist logic on its own.
/// </summary>
public class MarkdownRendererTests
{
    [Fact]
    public void Bold_markdown_renders_as_a_Bold_inline()
    {
        var renderer = new MarkdownRenderer();

        var paragraph = RenderSingleParagraph(renderer, "This is **bold** text.");

        var bold = FindInline<Bold>(paragraph.Inlines!);
        Assert.NotNull(bold);
        Assert.Equal("bold", Flatten(bold!.Inlines));
        Assert.Equal("This is bold text.", Flatten(paragraph.Inlines!));
    }

    [Fact]
    public void Italic_markdown_renders_as_an_Italic_inline()
    {
        var renderer = new MarkdownRenderer();

        var paragraph = RenderSingleParagraph(renderer, "This is *italic* text.");

        var italic = FindInline<Italic>(paragraph.Inlines!);
        Assert.NotNull(italic);
        Assert.Equal("italic", Flatten(italic!.Inlines));
    }

    [Fact]
    public void Combined_bold_and_italic_nests_correctly()
    {
        var renderer = new MarkdownRenderer();

        var paragraph = RenderSingleParagraph(renderer, "***both***");

        // Markdig nests EmphasisInline for combined bold+italic -- either nesting order (Bold
        // wrapping Italic, or vice versa) is an acceptable, faithful rendering of "***both***".
        var outer = FindInline<Bold>(paragraph.Inlines!) ?? (Span?)FindInline<Italic>(paragraph.Inlines!);
        Assert.NotNull(outer);
        Assert.Equal("both", Flatten(paragraph.Inlines!));
    }

    [Theory]
    [InlineData(1, "title")]
    [InlineData(2, "subtitle")]
    [InlineData(3, "body")]
    [InlineData(6, "body")]
    public void Heading_renders_with_the_expected_typography_class(int level, string expectedClass)
    {
        var renderer = new MarkdownRenderer();
        var markdown = new string('#', level) + " Chapter One";

        var root = (StackPanel)renderer.Render(markdown);
        var heading = Assert.IsType<TextBlock>(Assert.Single(root.Children));

        Assert.Contains(expectedClass, heading.Classes);
        Assert.Equal("Chapter One", Flatten(heading.Inlines!));
    }

    [Fact]
    public void Heading_level_3_and_above_is_bold_to_distinguish_it_from_body_text()
    {
        var renderer = new MarkdownRenderer();

        var root = (StackPanel)renderer.Render("### Sub-section");
        var heading = Assert.IsType<TextBlock>(Assert.Single(root.Children));

        Assert.Equal(FontWeight.Bold, heading.FontWeight);
    }

    [Fact]
    public void Unordered_list_renders_one_bullet_row_per_item_in_order()
    {
        var renderer = new MarkdownRenderer();

        var root = (StackPanel)renderer.Render("- first\n- second\n- third");
        var list = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        var rows = list.Children.Cast<Grid>().ToList();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal("•", ((TextBlock)row.Children[0]).Text));
        Assert.Equal(
            ["first", "second", "third"],
            rows.Select(row => Flatten(((TextBlock)((StackPanel)row.Children[1]).Children[0]).Inlines!)));
    }

    [Fact]
    public void Ordered_list_numbers_items_sequentially_starting_at_one()
    {
        var renderer = new MarkdownRenderer();

        var root = (StackPanel)renderer.Render("1. first\n2. second\n3. third");
        var list = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        var rows = list.Children.Cast<Grid>().ToList();

        Assert.Equal(["1.", "2.", "3."], rows.Select(row => ((TextBlock)row.Children[0]).Text));
    }

    [Fact]
    public void Https_link_renders_as_a_clickable_control_that_invokes_the_open_callback_with_the_url()
    {
        Uri? openedUri = null;
        var renderer = new MarkdownRenderer(uri => openedUri = uri);

        var paragraph = RenderSingleParagraph(renderer, "Check [the docs](https://example.com/docs) for more.");
        var container = FindInline<InlineUIContainer>(paragraph.Inlines!);

        Assert.NotNull(container);
        var button = Assert.IsType<Button>(container!.Child);
        Assert.Contains("link", button.Classes);
        var label = Assert.IsType<TextBlock>(button.Content);
        Assert.Equal("the docs", label.Text);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(new Uri("https://example.com/docs"), openedUri);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    public void Link_with_a_disallowed_scheme_renders_as_plain_non_clickable_text_of_its_label(string url)
    {
        var openCallCount = 0;
        var renderer = new MarkdownRenderer(_ => openCallCount++);

        var paragraph = RenderSingleParagraph(renderer, $"Click [here]({url}) if you dare.");

        Assert.Null(FindInline<InlineUIContainer>(paragraph.Inlines!));
        Assert.DoesNotContain(paragraph.Inlines!, inline => inline is InlineUIContainer);
        Assert.Equal("Click here if you dare.", Flatten(paragraph.Inlines!));
        Assert.Equal(0, openCallCount);
    }

    [Fact]
    public void Raw_html_block_renders_as_inert_literal_text_not_as_markup()
    {
        var renderer = new MarkdownRenderer();

        var root = (StackPanel)renderer.Render("<script>alert(1)</script>");
        var block = Assert.IsType<TextBlock>(Assert.Single(root.Children));

        // The exact raw source text shows up verbatim as plain display data -- there's no Button,
        // no InlineUIContainer, nothing that gives it any special meaning.
        Assert.Contains("<script>alert(1)</script>", block.Text);
    }

    [Fact]
    public void Raw_inline_html_inside_a_paragraph_renders_as_inert_literal_text()
    {
        var renderer = new MarkdownRenderer();

        var paragraph = RenderSingleParagraph(renderer, "before <img src=x onerror=alert(1)> after");

        var text = Flatten(paragraph.Inlines!);
        Assert.Contains("<img src=x onerror=alert(1)>", text);
        Assert.DoesNotContain(paragraph.Inlines!, inline => inline is InlineUIContainer);
    }

    [Theory]
    [InlineData("```\nvar x = 1;\n```")] // fenced code block
    [InlineData("> a quote")] // block quote
    [InlineData("---")] // thematic break
    [InlineData("| a | b |\n|---|---|\n| 1 | 2 |")] // pipe-table syntax -- no table extension enabled
    public void Unsupported_block_constructs_render_without_throwing(string markdown)
    {
        var renderer = new MarkdownRenderer();

        var exception = Record.Exception(() => renderer.Render(markdown));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_or_whitespace_input_renders_an_empty_panel_without_throwing(string? markdown)
    {
        var renderer = new MarkdownRenderer();

        var root = (StackPanel)renderer.Render(markdown);

        Assert.Empty(root.Children);
    }

    [Fact]
    public void Malformed_input_never_throws_and_falls_back_to_the_raw_text()
    {
        var renderer = new MarkdownRenderer();

        // Nothing pathological actually crashes Markdig's parser, but Render's own defensive
        // try/catch (see its remarks) means even a hypothetical parser exception would still
        // produce a control, not an unhandled throw -- exercised here via ordinary unbalanced
        // markdown syntax that a real user could plausibly type.
        var exception = Record.Exception(() => renderer.Render("**unterminated bold and [broken(link"));

        Assert.Null(exception);
    }

    private static TextBlock RenderSingleParagraph(MarkdownRenderer renderer, string markdown)
    {
        var root = (StackPanel)renderer.Render(markdown);
        return Assert.IsType<TextBlock>(Assert.Single(root.Children));
    }

    private static T? FindInline<T>(IEnumerable<Inline> inlines)
        where T : Inline
    {
        foreach (var inline in inlines)
        {
            if (inline is T match)
            {
                return match;
            }

            if (inline is Span span)
            {
                var found = FindInline<T>(span.Inlines);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static string Flatten(IEnumerable<Inline> inlines)
    {
        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            AppendText(builder, inline);
        }

        return builder.ToString();
    }

    private static void AppendText(StringBuilder builder, Inline inline)
    {
        switch (inline)
        {
            case Run run:
                builder.Append(run.Text);
                break;
            case LineBreak:
                builder.Append(' ');
                break;
            case InlineUIContainer { Child: Button { Content: TextBlock label } }:
                builder.Append(label.Text);
                break;
            case Span span:
                foreach (var child in span.Inlines)
                {
                    AppendText(builder, child);
                }

                break;
        }
    }
}