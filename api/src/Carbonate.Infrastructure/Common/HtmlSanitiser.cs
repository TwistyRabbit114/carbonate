using AngleSharp.Html.Dom;
using Carbonate.Application.Common;
using Ganss.Xss;

namespace Carbonate.Infrastructure.Common;

//an allowlist, so anything not named here is stripped: paragraphs, line breaks, bold, italic, lists and
//links. links keep only https and mailto, and always open without handing over the opener page
internal sealed class HtmlSanitiser : IHtmlSanitiser
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitiser()
    {
        _sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "br", "strong", "em", "ul", "ol", "li", "a",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
            AllowedCssProperties = new HashSet<string>(),
            AllowedAtRules = new HashSet<AngleSharp.Css.Dom.CssRuleType>(),
        })
        {
            //text inside a stripped wrapper (a pasted div or span) survives as plain text
            KeepChildNodes = true,
        };

        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IHtmlAnchorElement link)
            {
                link.SetAttribute("rel", "noopener noreferrer");
            }
        };
    }

    public string Sanitise(string html) => _sanitizer.Sanitize(html).Trim();
}
