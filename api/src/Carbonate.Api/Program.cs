using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application;
using Carbonate.Application.Common;
using Carbonate.Application.Platform.Auth;
using Carbonate.Infrastructure;
using Carbonate.Infrastructure.Platform.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
        options.Filters.Add<FinancialMaskingFilter>();
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = context => throw ProblemException.Validation(
            context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => JsonNamingPolicy.CamelCase.ConvertName(e.Key),
                    e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray())));

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(includeInternalTypes: true);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // Replace the framework's RFC link with the app's own problem types, but keep any type a service set.
    if (context.ProblemDetails.Type is null || context.ProblemDetails.Type.StartsWith("https://", StringComparison.Ordinal))
    {
        context.ProblemDetails.Type = context.ProblemDetails.Status switch
        {
            400 => "/problems/validation",
            401 => "/problems/unauthenticated",
            403 => "/problems/forbidden",
            404 => "/problems/not-found",
            429 => "/problems/rate-limited",
            _ => "/problems/server-error",
        };
    }
});
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

builder.Services.AddCarbonateOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Authentication: JWT bearer. The settings are read when the first request arrives, so they come from
// the final configuration (user-secrets locally, Key Vault in Azure).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = TokenValidation.Parameters(jwt.Value);
    });

// Authorisation is deny by default: anything without an explicit rule needs a signed-in user, and the
// short-lived MFA token never counts as signed in.
var signedIn = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireAssertion(context => !context.User.HasClaim(ClaimNames.Purpose, ClaimNames.MfaPurpose))
    .Build();
builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(signedIn)
    .SetFallbackPolicy(signedIn)
    .AddPolicy(AuthPolicies.MfaPending, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimNames.Purpose, ClaimNames.MfaPurpose));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
        }));
    // Sign-in and code checks are the brute-force targets, so they get a much tighter limit.
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = context.RequestServices.GetRequiredService<IConfiguration>()
                .GetValue("RateLimiting:LoginPermitPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
        }));
});

// App Service terminates TLS and forwards the client address, which the rate limiter keys on.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.UseCarbonateOpenApi();

// Flat 200, no dependency checks: the availability test hits this every five minutes and a
// database query here would stop the serverless database auto-pausing (decisions D-001).
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

static string ClientKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

public partial class Program;
