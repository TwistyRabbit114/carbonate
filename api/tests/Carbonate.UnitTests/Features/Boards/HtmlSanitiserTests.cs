using System.Text.RegularExpressions;
using Carbonate.Infrastructure.Common;
using Shouldly;

namespace Carbonate.UnitTests.Features.Boards;

//card descriptions are the one place html is allowed, so they get the owasp filter-evasion treatment (plan section 7.5)
public partial class HtmlSanitiserTests
{
    private readonly HtmlSanitiser _sanitiser = new();

    [Theory]
    [InlineData("<script>alert(1)</script>Load-in at six")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<IMG SRC=JaVaScRiPt:alert('XSS')>")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>")]
    [InlineData("<a href=\"javascript:alert(1)\">click</a>")]
    [InlineData("<a href=\"  JaVaScRiPt:alert(1)\">click</a>")]
    [InlineData("<a href=\"java&#x09;script:alert(1)\">click</a>")]
    [InlineData("<a href=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">click</a>")]
    [InlineData("<p onclick=\"alert(1)\" style=\"background:url(javascript:alert(1))\">Hi</p>")]
    [InlineData("<body onload=alert(1)>")]
    [InlineData("<math><mtext><table><mglyph><style><img src=x onerror=alert(1)></style>")]
    public void Strips_anything_that_could_run(string payload)
    {
        var clean = _sanitiser.Sanitise(payload);

        //escaped leftovers like "&lt;img onerror=...&gt;" are only text, so what matters is that every real
        //tag left is on the allowlist, with nothing but a safe href
        foreach (Match tag in Tag().Matches(clean))
        {
            Allowed.ShouldContain(tag.Groups["name"].Value.ToLowerInvariant());
            foreach (Match attribute in Attribute().Matches(tag.Groups["attributes"].Value))
            {
                new[] { "href", "rel" }.ShouldContain(attribute.Groups["name"].Value.ToLowerInvariant());
                if (attribute.Groups["name"].Value.Equals("href", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.Groups["value"].Value.ShouldMatch("^(https:|mailto:)");
                }
            }
        }
    }

    private static readonly string[] Allowed = ["p", "br", "strong", "em", "ul", "ol", "li", "a"];

    [GeneratedRegex("<\\s*/?\\s*(?<name>[a-zA-Z0-9]+)(?<attributes>[^>]*)>")]
    private static partial Regex Tag();

    [GeneratedRegex("(?<name>[a-zA-Z-]+)\\s*=\\s*\"(?<value>[^\"]*)\"")]
    private static partial Regex Attribute();

    [Fact]
    public void Keeps_the_formatting_the_plan_allows()
    {
        var clean = _sanitiser.Sanitise(
            "<p>Bring <strong>two</strong> <em>spare</em> kegs</p><ul><li>Ice</li></ul><ol><li>Cups</li></ol>line<br>break");

        clean.ShouldBe(
            "<p>Bring <strong>two</strong> <em>spare</em> kegs</p><ul><li>Ice</li></ul><ol><li>Cups</li></ol>line<br>break");
    }

    [Theory]
    [InlineData("https://carbon.example/run-sheet")]
    [InlineData("mailto:ops@carbon.example")]
    public void Keeps_safe_links_and_stops_them_reaching_back_to_the_page(string href)
    {
        var clean = _sanitiser.Sanitise($"<a href=\"{href}\" target=\"_blank\">run sheet</a>");

        clean.ShouldContain($"href=\"{href}\"");
        clean.ShouldContain("rel=\"noopener noreferrer\"");
        clean.ShouldNotContain("target");
    }

    [Fact]
    public void Plain_http_links_are_dropped()
    {
        _sanitiser.Sanitise("<a href=\"http://carbon.example\">site</a>").ShouldNotContain("href");
    }

    [Fact]
    public void Text_inside_unsupported_wrappers_survives_as_text()
    {
        _sanitiser.Sanitise("<div>Pasted from <span style=\"color:red\">an email</span></div>")
            .ShouldBe("Pasted from an email");
    }
}
