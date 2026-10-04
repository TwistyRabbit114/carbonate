namespace Carbonate.Api.Common;

internal static class SecurityHeaders
{
    /// <summary>
    /// Headers every response carries (plan section 7.5). The Content Security Policy for the SPA is
    /// set where the static files are served, not here.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
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

            await next();
        });
}
