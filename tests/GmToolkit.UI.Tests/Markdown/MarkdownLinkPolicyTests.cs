using GmToolkit.UI.Markdown;

namespace GmToolkit.UI.Tests.Markdown;

public class MarkdownLinkPolicyTests
{
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("https://example.com/path?query=1#fragment")]
    [InlineData("HTTPS://EXAMPLE.COM")] // scheme comparison is case-insensitive, per RFC 3986
    public void IsAllowedLinkUri_allows_http_and_https(string url)
    {
        var allowed = MarkdownLinkPolicy.IsAllowedLinkUri(url, out var uri);

        Assert.True(allowed);
        Assert.NotNull(uri);
        Assert.Equal(url, uri!.OriginalString, ignoreCase: true);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://example.com")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("customscheme://launch")]
    public void IsAllowedLinkUri_rejects_every_scheme_except_http_and_https(string url)
    {
        var allowed = MarkdownLinkPolicy.IsAllowedLinkUri(url, out var uri);

        Assert.False(allowed);
        Assert.Null(uri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url at all")]
    [InlineData("relative/path.md")]
    [InlineData("//example.com")] // scheme-relative -- Uri.TryCreate(..., Absolute) rejects this too
    public void IsAllowedLinkUri_rejects_unparseable_or_relative_input(string? url)
    {
        var allowed = MarkdownLinkPolicy.IsAllowedLinkUri(url, out var uri);

        Assert.False(allowed);
        Assert.Null(uri);
    }
}