using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;

namespace Carbonate.Api.Common;

/// <summary>
/// Serves the built React app from wwwroot (hosting option 3, decisions D-003) so the page and the API
/// share one origin, which the SameSite=Strict refresh cookie needs.
/// </summary>
internal static class SpaHosting
{
    private const int MaxCspReportBytes = 16 * 1024;

    /// <summary>A new instance each time: the framework fills in the file provider, so one shared instance would leak between hosts.</summary>
    private static StaticFileOptions NewFileOptions() => new()
    {
        OnPrepareResponse = context =>
        {
            // Built assets have a hash in their name, so they can be cached for good. The page that
            // points at them must never be, or a new release would not be picked up.
            var isAsset = context.Context.Request.Path.StartsWithSegments("/assets");
            context.Context.Response.Headers.CacheControl = isAsset
                ? "public, max-age=31536000, immutable"
                : "no-cache";
        },
    };

    /// <summary>Static files first, so they are served without a sign-in or any rate-limit cost.</summary>
    public static IApplicationBuilder UseSpaFiles(this IApplicationBuilder app)
    {
        app.UseDefaultFiles();
        return app.UseStaticFiles(NewFileOptions());
    }

    /// <summary>
    /// Any other path that is not an API call, health check or API explorer gets the app, so a refresh on
    /// a client-side route such as /events/123 works. A path that looks like a file (it ends in an extension)
    /// is a 404 if the file is missing, not the app. Anonymous, because the app has to load before
    /// anyone can sign in. API paths are left alone so a wrong URL is an error, not a web page.
    /// </summary>
    public static void MapSpaFallback(this IEndpointRouteBuilder app) =>
        app.MapFallbackToFile(
                @"{*path:regex(^(?!api(/|$)|health$|openapi|swagger)(?!.*\.[A-Za-z0-9]+$).*$)}",
                "index.html",
                NewFileOptions())
            .AllowAnonymous();

    /// <summary>
    /// Where browsers report policy violations (plan section 7.2 allows this endpoint to be anonymous: a
    /// browser sends it without credentials). It only logs the directive and a trimmed address, never the
    /// body, and refuses anything large.
    /// </summary>
    public static void MapCspReport(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/csp-report", async (HttpContext context, ILoggerFactory loggers) =>
        {
            if (context.Request.ContentLength is > MaxCspReportBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var logger = loggers.CreateLogger("CspReport");
            try
            {
                using var buffer = new MemoryStream();
                await context.Request.Body.CopyToAsync(buffer);
                if (buffer.Length > MaxCspReportBytes)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                buffer.Position = 0;
                using var document = await JsonDocument.ParseAsync(buffer);
                var report = ReportBody(document.RootElement);

                logger.LogWarning("CSP violation: {Directive} blocked {Blocked} on {Page}",
                    Text(report, "violated-directive") ?? Text(report, "effectiveDirective"),
                    Trim(Text(report, "blocked-uri") ?? Text(report, "blockedURL")),
                    Trim(Text(report, "document-uri") ?? Text(report, "documentURL")));
            }
            catch (JsonException)
            {
                // A malformed report is not worth an error. The browser will not retry it either.
            }

            return Results.NoContent();
        }).AllowAnonymous();

    /// <summary>
    /// Older browsers send {"csp-report": {...}}; newer ones send an array of {"type": ..., "body": {...}}.
    /// Anything else yields an empty object, so an odd report is logged without detail and never fails.
    /// </summary>
    private static JsonElement ReportBody(JsonElement root)
    {
        var first = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 ? root[0] : root;
        if (first.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        if (first.TryGetProperty("csp-report", out var wrapped))
        {
            return wrapped;
        }

        return first.TryGetProperty("body", out var body) ? body : first;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Drops the query string and fragment and caps the length, so a report cannot put personal data or a flood in the log.</summary>
    private static string? Trim(string? address)
    {
        if (address is null)
        {
            return null;
        }

        var end = address.IndexOfAny(['?', '#']);
        var trimmed = end >= 0 ? address[..end] : address;
        return trimmed.Length > 200 ? trimmed[..200] : trimmed;
    }
}
