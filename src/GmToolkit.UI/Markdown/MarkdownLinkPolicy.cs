namespace GmToolkit.UI.Markdown;

/// <summary>
/// Decides which markdown link URLs <see cref="MarkdownRenderer"/> is allowed to turn into an
/// actually-clickable control (issue #22's security acceptance criterion).
/// </summary>
/// <remarks>
/// <para>
/// <b>Only <c>http</c>/<c>https</c> are allowed, deliberately.</b> A clickable link opens via the
/// OS's default handler (<see cref="System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)"/>
/// with <c>UseShellExecute = true</c> -- the standard .NET way to hand a URL to the system, see
/// <see cref="MarkdownRenderer"/>'s remarks), which is safe for ordinary web links but not for an
/// arbitrary scheme a malicious or malformed note could contain: <c>javascript:</c> has no meaning
/// outside a browser but costs nothing to reject; <c>file://</c> could open an arbitrary local file;
/// a registered custom scheme (<c>mailto:</c>, a game-launcher's own protocol handler, etc.) could
/// trigger unexpected behavior the user never asked for just by viewing a note. Rejecting everything
/// except the two schemes an app with no other URL-consuming feature actually needs is simpler and
/// safer than trying to enumerate every scheme worth blocking.
/// </para>
/// <para>
/// Kept as its own tiny, pure, static class (rather than inlined into <see cref="MarkdownRenderer"/>)
/// so it's trivially unit-testable in isolation -- see <c>MarkdownLinkPolicyTests</c>.
/// </para>
/// </remarks>
public static class MarkdownLinkPolicy
{
    /// <summary>
    /// Returns whether <paramref name="url"/> is safe to render as a clickable link, and if so, the
    /// parsed absolute <see cref="Uri"/> to open. Anything that isn't an absolute <c>http</c>/<c>https</c>
    /// URI -- unparseable text, a relative reference, or any other scheme -- returns <c>false</c> with
    /// <paramref name="uri"/> left <c>null</c>; the caller is expected to fall back to rendering the
    /// link's label as plain, non-clickable text (see <see cref="MarkdownRenderer"/>) rather than
    /// dropping the content entirely.
    /// </summary>
    public static bool IsAllowedLinkUri(string? url, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // UriKind.Absolute rejects bare relative references (e.g. "notes.md") up front -- there is
        // no document base to resolve them against here, and an unresolvable relative link is exactly
        // as useless to render as a clickable control as a disallowed scheme.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}