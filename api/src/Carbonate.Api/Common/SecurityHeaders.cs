namespace Carbonate.Api.Common;

internal static class SecurityHeaders
{
    /// <summary>
    /// The Content Security Policy for the SPA (plan section 7.5). A static host cannot add a nonce per
    /// request, so the SPA ships no inline scripts or styles and the policy allows only its own files.
    /// Images may also come from the blob storage host, for incident photos and attachments.
    /// </summary>
    public static string ContentSecurityPolicy(string? blobHost)
    {
        var images = string.IsNullOrWhiteSpace(blobHost) ? "'self' data:" : $"'self' data: {blobHost}";
        // font-src is not in the plan's policy, so fonts fell back to default-src and Vite's small inlined
        // data: fonts were blocked on the hosted site. A font cannot run script, so data: is allowed for
        // fonts only (decisions D-014).
        return "default-src 'self'; script-src 'self'; style-src 'self'; "
            + $"img-src {images}; font-src 'self' data:; connect-src 'self'; object-src 'none'; "
            + "base-uri 'self'; frame-ancestors 'none'; form-action 'self'; report-uri /api/csp-report";
    }

    /// <summary>
    /// Headers every response carries (plan section 7.5). The policy header goes on everything that is
    /// not an API call: the page itself and its scripts, styles and images.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, string? blobHost = null)
    {
        var policy = ContentSecurityPolicy(blobHost);

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers["Cache-Control"] = "no-store";
            }
            else
            {
                headers["Content-Security-Policy"] = policy;
            }

            await next();
        });
    }
}
