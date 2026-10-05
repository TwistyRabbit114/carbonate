using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.IntegrationTests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.IntegrationTests.Contract;

public class ContractTests : IClassFixture<ContractTests.ContractFactory>
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private readonly ContractFactory _factory;

    public ContractTests(ContractFactory factory) => _factory = factory;

    public class ContractFactory : ApiFactory
    {
        public ContractFactory() => IncludeProbes = false;
    }

    /// <summary>
    /// docs/api/openapi.json is the contract the front end generates its types from. This fails when the
    /// API has changed and the file has not. Regenerate it with:
    /// UPDATE_CONTRACT=1 dotnet test api/tests/Carbonate.IntegrationTests --filter ContractTests
    /// </summary>
    [Fact]
    public async Task The_committed_contract_matches_the_api()
    {
        var response = await _factory.CreateClient().GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var generated = Normalise(await response.Content.ReadAsStringAsync());

        var path = Path.Combine(RepositoryRoot(), "docs", "api", "openapi.json");

        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, generated);
            return;
        }

        Assert.True(File.Exists(path), "docs/api/openapi.json is missing. Regenerate it (see this test's summary).");
        var committed = Normalise(await File.ReadAllTextAsync(path));
        Assert.True(committed == generated,
            "docs/api/openapi.json is out of date. Regenerate it: UPDATE_CONTRACT=1 dotnet test api/tests/Carbonate.IntegrationTests --filter ContractTests");
    }

    /// <summary>
    /// Every endpoint states who may call it (plan section 7.2). The only anonymous ones are the five
    /// the plan allows; csp-report arrives with the CSP work.
    /// </summary>
    [Fact]
    public void Every_endpoint_declares_its_authorisation_and_only_the_allowed_ones_are_anonymous()
    {
        var endpoints = _factory.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).ToList();
        var anonymous = new List<string>();
        var undeclared = new List<string>();

        foreach (var endpoint in endpoints.OfType<RouteEndpoint>())
        {
            var route = (endpoint.RoutePattern.RawText ?? "").TrimStart('/');
            // The API explorer and the app fallback (which serves the page, never data) are not API endpoints.
            if (route.StartsWith("openapi", StringComparison.Ordinal)
                || route.Contains("swagger", StringComparison.Ordinal)
                || route.StartsWith("{*path", StringComparison.Ordinal))
            {
                continue;
            }

            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                anonymous.Add(route);
            }
            else if (!endpoint.Metadata.OfType<IAuthorizeData>().Any())
            {
                undeclared.Add($"{route} ({endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.ActionName})");
            }
        }

        Assert.Empty(undeclared);
        Assert.Equivalent(
            new[] { "api/auth/login", "api/auth/mfa/verify", "api/auth/refresh", "api/csp-report", "health" },
            anonymous);
    }

    [Fact]
    public async Task A_permitted_caller_reaching_an_unbuilt_endpoint_gets_a_clear_501_problem()
    {
        var client = _factory.CreateClientWith(["Director"], RolePermissionMatrix.PermissionsFor(RoleNames.Director));

        var response = await client.GetAsync("/api/venues");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("/problems/not-implemented", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task The_contract_describes_the_security_scheme_and_the_error_shapes()
    {
        var json = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        Assert.Equal("bearer", json.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("Bearer").GetProperty("scheme").GetString());

        var listEvents = json.GetProperty("paths").GetProperty("/api/events").GetProperty("get");
        Assert.True(listEvents.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(listEvents.TryGetProperty("security", out _));

        var login = json.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post");
        Assert.False(login.TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
    }

    private static string Normalise(string json) =>
        JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json), Pretty).Replace("\r\n", "\n") + "\n";

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "api", "Carbonate.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
    }
}
