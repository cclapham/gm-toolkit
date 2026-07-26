using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace GmToolkit.UI.Markdown;

/// <summary>
/// Parses a markdown string with Markdig and walks its AST to build a tree of native Avalonia
/// controls (issue #22), rather than going through an HTML intermediate step or a third-party
/// Avalonia-specific markdown UI package.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a hand-written AST-to-control renderer, not a community Avalonia markdown package:</b>
/// (a) link-click handling is the one genuinely security-sensitive part of this feature (see
/// <see cref="MarkdownLinkPolicy"/>), and writing it ourselves keeps that behavior fully in our own
/// control rather than trusting a third party's choices; (b) a third-party Avalonia-specific
/// markdown *UI* package would itself be an additional, unverified AOT/trimming risk stacked on top
/// of Markdig's own -- exactly the kind of risk this issue asks to be careful about; (c) this renders
/// only the 5 constructs the issue actually asks for (bold, italic, lists, headings, links) at
/// proportionate effort, rather than chasing full CommonMark-to-native-control fidelity Avalonia
/// (not being a WebView) can't cheaply offer anyway.
/// </para>
/// <para>
/// <b>Fidelity is intentionally partial.</b> Only <see cref="HeadingBlock"/>, <see cref="ParagraphBlock"/>
/// and <see cref="ListBlock"/> get dedicated rendering; everything else Markdig can parse from the
/// default CommonMark pipeline (block quotes, code blocks/spans, thematic breaks, raw HTML blocks,
/// and unparsed table-like text -- no extensions are enabled, so pipe-table syntax is never even
/// recognized as a table) falls back to its literal source text in a plain paragraph, per the issue's
/// "treat unsupported constructs as plain text rather than failing" task. Combined inline formatting
/// *inside* a link's own label (e.g. <c>[**bold**](url)</c>) is flattened to plain text in the
/// rendered link -- preserving nested formatting there specifically would meaningfully complicate the
/// renderer for a rare case; the label text itself is never lost, just its inner emphasis.
/// </para>
/// <para>
/// <b>Android AOT/trimming check (per this issue's task list) -- a proxy, not a proof:</b> with no
/// Android SDK/device available in this environment, the installed Markdig 1.3.2 assembly was
/// inspected via <c>strings</c> over the <c>net10.0</c> lib DLL for the reflection-heavy API *names*
/// that most commonly break under trimming (<c>Activator.CreateInstance</c>, <c>MakeGenericType</c>,
/// <c>Assembly.Load</c>, <c>Reflection.Emit</c>) and found none; the only hits are plain
/// <c>GetType()</c> calls inside debug-display strings, which are trim-safe. This is weak evidence,
/// not conclusive -- a string scan can't see e.g. <c>Type.GetType(string)</c> called by name,
/// generic <c>Activator.CreateInstance&lt;T&gt;</c>, or the same APIs invoked via other IL paths.
/// Markdig's extension pipeline being built from directly-referenced, statically-typed classes (not
/// reflection-based discovery) makes trim-breakage unlikely, but this is a smoke test pending real
/// confirmation once #35/#36 have actual device/CI Android access -- same no-device caveat PR #46's
/// SQLite check carried.
/// </para>
/// <para>
/// <b>Security:</b> raw HTML (<see cref="Markdig.Syntax.HtmlBlock"/>/<see cref="HtmlInline"/>) is
/// never given any special interpretation -- it is one more "unsupported construct" whose literal
/// source text becomes the <see cref="Avalonia.Controls.Documents.Run"/>'s <c>Text</c>, and since
/// this app has no WebView/HTML sink anywhere, inert display text is all it can ever become. Links
/// only become an actually-clickable control when <see cref="MarkdownLinkPolicy.IsAllowedLinkUri"/>
/// allows their scheme (<c>http</c>/<c>https</c> only); anything else renders as the link's plain,
/// non-clickable label text (see <see cref="AppendLink"/>). This type never throws for malformed or
/// adversarial input -- <see cref="Render"/> wraps the whole parse+build pass in a defensive
/// try/catch and falls back to the raw source as a single literal paragraph, since a note field must
/// never be able to crash the app it's typed into.
/// </para>
/// </remarks>
public sealed class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    private readonly Action<Uri>? _openLinkOverride;

    /// <param name="openLink">
    /// Invoked when the user clicks a rendered link whose scheme <see cref="MarkdownLinkPolicy"/>
    /// allowed, instead of the default platform-aware launch behavior (see
    /// <see cref="OpenLink"/>). Overridable purely so tests can capture what would have been opened
    /// without actually launching anything.
    /// </param>
    public MarkdownRenderer(Action<Uri>? openLink = null)
    {
        _openLinkOverride = openLink;
    }

    /// <summary>
    /// Parses <paramref name="markdownSource"/> and returns a control tree rendering it -- a
    /// <see cref="StackPanel"/> of one child per top-level block. Never throws and never returns
    /// <c>null</c>; empty/whitespace-only input yields an empty (but valid) panel, and any exception
    /// while parsing or rendering falls back to a single plain-text paragraph containing the raw
    /// source (see this class's remarks).
    /// </summary>
    public Control Render(string? markdownSource)
    {
        markdownSource ??= string.Empty;

        try
        {
            var document = Markdig.Markdown.Parse(markdownSource, Pipeline);
            var root = new StackPanel { Spacing = 8 }; // mirrors SpaceSM, see Styles/Spacing.axaml's note on Thickness/plain-double resources

            foreach (var block in document)
            {
                var rendered = RenderBlock(block);
                if (rendered is not null)
                {
                    root.Children.Add(rendered);
                }
            }

            return root;
        }
        catch (Exception)
        {
            // Defensive only -- a note field must never be able to crash the app it's typed into,
            // no matter how malformed or adversarial its content is. Falls back to showing the raw
            // text verbatim, which is exactly as inert as any other "unsupported construct".
            return BuildPlainTextBlock(markdownSource);
        }
    }

    private Control? RenderBlock(Block block) => block switch
    {
        HeadingBlock heading => BuildHeading(heading),
        ParagraphBlock paragraph => BuildParagraph(paragraph),
        ListBlock list => BuildList(list),
        _ => BuildFallbackBlock(block),
    };

    private Control BuildHeading(HeadingBlock heading)
    {
        var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, Inlines = new InlineCollection() };
        textBlock.Classes.Add(heading.Level switch
        {
            1 => "title",
            2 => "subtitle",
            _ => "body",
        });

        if (heading.Level >= 3)
        {
            textBlock.FontWeight = FontWeight.Bold;
        }

        BindThemeForeground(textBlock);
        AppendChildInlines(textBlock.Inlines, heading.Inline);
        return textBlock;
    }

    private Control BuildParagraph(ParagraphBlock paragraph)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Inlines = new InlineCollection(),
        };
        textBlock.Classes.Add("body");
        BindThemeForeground(textBlock);
        AppendChildInlines(textBlock.Inlines, paragraph.Inline);
        return textBlock;
    }

    private Control BuildList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 4 }; // mirrors SpaceXS
        var index = 0;

        foreach (var itemBlock in list)
        {
            index++;
            if (itemBlock is not ListItemBlock listItem)
            {
                continue;
            }

            var marker = list.IsOrdered ? $"{(listItem.Order > 0 ? listItem.Order : index)}." : "•";
            var markerBlock = new TextBlock
            {
                Text = marker,
                Margin = new Thickness(0, 0, 8, 0), // mirrors SpaceSM
            };
            markerBlock.Classes.Add("body");
            BindThemeForeground(markerBlock);

            var content = new StackPanel { Spacing = 4 }; // mirrors SpaceXS
            foreach (var child in listItem)
            {
                var rendered = RenderBlock(child);
                if (rendered is not null)
                {
                    content.Children.Add(rendered);
                }
            }

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(markerBlock, 0);
            Grid.SetColumn(content, 1);
            row.Children.Add(markerBlock);
            row.Children.Add(content);

            panel.Children.Add(row);
        }

        return panel;
    }

    /// <summary>
    /// Anything other than a heading/paragraph/list -- block quotes, code blocks, thematic breaks,
    /// raw HTML blocks, or any other block type Markdig's default pipeline can produce -- renders as
    /// its literal source text in a plain paragraph, per this class's remarks. Returns <c>null</c>
    /// (skip -- no empty row) if that source text is empty.
    /// </summary>
    private Control? BuildFallbackBlock(Block block)
    {
        var text = GetRawText(block).Trim('\r', '\n');
        return text.Length == 0 ? null : BuildPlainTextBlock(text);
    }

    private Control BuildPlainTextBlock(string text)
    {
        var textBlock = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        textBlock.Classes.Add("body");
        BindThemeForeground(textBlock);
        return textBlock;
    }

    /// <summary>
    /// A <see cref="LeafBlock"/> that Markdig has inline-parsed (e.g. a <see cref="ParagraphBlock"/>
    /// nested inside a block quote) has its <see cref="LeafBlock.Lines"/> cleared -- the content
    /// lives in <see cref="LeafBlock.Inline"/> instead, and reading <c>Lines</c> for it silently
    /// returns empty, which previously made block-quote content vanish rather than fall back to
    /// plain text. Blocks Markdig never inline-parses (fenced code, raw HTML, thematic breaks) keep
    /// their content in <c>Lines</c> as usual, so that's still the fallback source for them.
    /// </summary>
    private static string GetRawText(Block block) => block switch
    {
        LeafBlock { Inline: { } inline } => FlattenText(inline),
        LeafBlock leaf => leaf.Lines.ToString(),
        ContainerBlock container => string.Join(Environment.NewLine, container.Select(GetRawText)),
        _ => string.Empty,
    };

    private void AppendChildInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var child in container)
        {
            AppendInline(target, child);
        }
    }

    private void AppendInline(InlineCollection target, MarkdigInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Run(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                // DelimiterCount == 2 is strong emphasis (**bold**/__bold__); 1 is regular emphasis
                // (*italic*/_italic_). Markdig nests EmphasisInline for combined ***bold italic***,
                // so recursing here naturally supports that too.
                Span span = emphasis.DelimiterCount >= 2 ? new Bold() : new Italic();
                foreach (var child in emphasis)
                {
                    AppendInline(span.Inlines, child);
                }

                target.Add(span);
                break;

            case LineBreakInline lineBreak:
                target.Add(lineBreak.IsHard ? new LineBreak() : new Run(" "));
                break;

            case AutolinkInline autolink:
                AppendLink(target, autolink.Url, autolink.Url ?? string.Empty);
                break;

            case LinkInline { IsImage: false } link:
                AppendLink(target, link.Url, FlattenText(link));
                break;

            case LinkInline image:
                // Images aren't one of the 5 constructs this issue asks for, and there's no image
                // loading/sink here -- fall back to the alt text as inert literal text, same
                // treatment as any other unsupported construct.
                target.Add(new Run(FlattenText(image)));
                break;

            case CodeInline code:
                // Inline code isn't one of the 5 required constructs -- render its literal content,
                // no monospace styling, same "unsupported construct -> plain text" treatment.
                target.Add(new Run(code.Content.ToString()));
                break;

            case HtmlInline html:
                // Raw inline HTML (e.g. "<script>...") is never interpreted -- its exact source text
                // becomes inert literal Run text, exactly like any other unsupported construct. There
                // is no HTML sink anywhere in this app for it to be dangerous to.
                target.Add(new Run(html.Tag));
                break;

            case ContainerInline containerInline:
                // Generic fallback for any other container-like inline Markdig's pipeline might
                // produce -- recurse into its children rather than dropping the content.
                AppendChildInlines(target, containerInline);
                break;

            default:
                var text = inline.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    target.Add(new Run(text));
                }

                break;
        }
    }

    /// <summary>
    /// Renders a link as an actually-clickable control if <paramref name="url"/>'s scheme is
    /// allowed (see <see cref="MarkdownLinkPolicy"/>), or as the label's plain, non-clickable text
    /// otherwise -- the acceptance criterion is explicit that a disallowed-scheme link must still
    /// show its label text, not silently vanish. The button's tooltip always shows the real
    /// <c>uri.AbsoluteUri</c> rather than trusting the markdown label -- a link's visible text and
    /// its target can legitimately differ (<c>[trusted-looking-label](url)</c>), so hovering before
    /// clicking is the only way to know where it actually goes.
    /// </summary>
    private void AppendLink(InlineCollection target, string? url, string label)
    {
        if (MarkdownLinkPolicy.IsAllowedLinkUri(url, out var uri) && uri is not null)
        {
            var linkText = new TextBlock
            {
                Text = label,
                TextDecorations = TextDecorations.Underline,
                TextWrapping = TextWrapping.Wrap,
            };
            var button = new Button { Content = linkText };
            button.Classes.Add("link");
            ToolTip.SetTip(button, uri.AbsoluteUri);
            button.Click += (sender, _) => OpenLink(sender as Control, uri);
            target.Add(new InlineUIContainer { Child = button });
        }
        else
        {
            target.Add(new Run(label));
        }
    }

    /// <summary>Flattens a link's own inline children (its label, e.g. the "text" in
    /// <c>[text](url)</c>) down to plain text -- see this class's remarks on why formatting nested
    /// inside a link's label specifically isn't preserved.</summary>
    private static string FlattenText(ContainerInline container)
    {
        var builder = new StringBuilder();
        AppendPlainText(builder, container);
        return builder.ToString();
    }

    private static void AppendPlainText(StringBuilder builder, MarkdigInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content.ToString());
                break;
            case AutolinkInline autolink:
                builder.Append(autolink.Url);
                break;
            case HtmlInline html:
                builder.Append(html.Tag);
                break;
            case LineBreakInline:
                builder.Append(' ');
                break;
            case ContainerInline container:
                foreach (var child in container)
                {
                    AppendPlainText(builder, child);
                }

                break;
            default:
                builder.Append(inline.ToString());
                break;
        }
    }

    /// <summary>
    /// Binds <see cref="TextBlock.ForegroundProperty"/> to the app's own <c>TextBrush</c> theme
    /// resource (see <c>Styles/Colors.axaml</c>) -- matches how every other hand-placed
    /// <see cref="TextBlock"/> in this app's views sets its <c>Foreground</c> explicitly (Fluent's
    /// default doesn't match this app's sepia/dark custom palette), just done in code here since
    /// these controls are built programmatically rather than declared in XAML.
    /// </summary>
    private static void BindThemeForeground(TextBlock textBlock) =>
        textBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextBrush"));

    /// <summary>
    /// Opens a link the user clicked. If a test-only override was supplied to the constructor, that
    /// runs instead of anything platform-specific. Otherwise, launches via the clicked button's own
    /// <see cref="TopLevel.Launcher"/> -- Avalonia's cross-platform URL opener (Intent-based on
    /// Android, the OS's registered handler on desktop) -- rather than
    /// <see cref="System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)"/> with
    /// <c>UseShellExecute = true</c>, which has no Android implementation at all and throws
    /// <see cref="PlatformNotSupportedException"/> there. Fire-and-forget, and any failure (no
    /// <see cref="TopLevel"/> yet, no registered handler, the platform declines) is swallowed --
    /// clicking a link must never crash the app, the same bar <see cref="Render"/> holds itself to.
    /// </summary>
    private void OpenLink(Control? sender, Uri uri)
    {
        if (_openLinkOverride is not null)
        {
            _openLinkOverride(uri);
            return;
        }

        var topLevel = sender is null ? null : TopLevel.GetTopLevel(sender);
        if (topLevel is null)
        {
            return;
        }

        _ = LaunchSafelyAsync(topLevel, uri);
    }

    private static async Task LaunchSafelyAsync(TopLevel topLevel, Uri uri)
    {
        try
        {
            await topLevel.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception)
        {
            // See OpenLink's remarks -- a link that can't be opened is a no-op, not a crash.
        }
    }
}