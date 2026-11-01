using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Carbonate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.IntegrationTests.Platform;

/// <summary>
/// Password reset (Task 2 feedback): an administrator issues a single-use reset, and the person sets their
/// own new password with it. Runs in its own database with the sign-in throttle raised.
/// </summary>
public class PasswordResetTests(DatabaseApiFixture fixture) : IClassFixture<DatabaseApiFixture>
{
    private const string OldPassword = "Correct-Horse-Battery-9!";
    private const string NewPassword = "Purple-Lantern-Harbour-42";
    private const string RefreshCookie = "carbonate.refresh";

    private readonly WebApplicationFactory<Program> _factory =
        fixture.Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:LoginPermitPerMinute", "1000"));

    private Scenario Scenario => new(fixture.Factory);

    private async Task<AppUser> UserWithPasswordAsync(string role)
    {
        var user = await Scenario.UserAsync(role);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CemDbContext>();
        var tracked = await db.Users.SingleAsync(u => u.UserId == user.UserId);
        tracked.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(tracked, OldPassword);
        await db.SaveChangesAsync();
        return tracked;
    }

    private async Task<string> IssueAsync(HttpClient admin, Guid userId)
    {
        var response = await admin.PostAsync($"/api/users/{userId}/password-reset", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private Task<HttpResponseMessage> ResetAsync(string token, string password = NewPassword) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/password/reset", new { token, newPassword = password });

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

    [Fact]
    public async Task An_issued_reset_sets_a_new_password_once_and_ends_existing_sessions()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        var login = await LoginAsync(user.Email, OldPassword);
        var oldCookie = login.Headers.GetValues("Set-Cookie").First(h => h.StartsWith(RefreshCookie + "=", StringComparison.Ordinal));

        var token = await IssueAsync(ops, user.UserId);
        var reset = await ResetAsync(token);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email, NewPassword)).StatusCode);

        // The session from before the reset is gone.
        var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refresh.Headers.Add("Cookie", oldCookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().SendAsync(refresh)).StatusCode);

        // Single use.
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, "Another-Long-Password-77")).StatusCode);
    }

    [Fact]
    public async Task The_token_is_never_stored_or_audited_in_plain_text()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var user = await UserWithPasswordAsync(RoleNames.EventManager);

        var token = await IssueAsync(ops, user.UserId);
        await ResetAsync(token);

        var stored = await Scenario.WithDbAsync(db => db.PasswordResetTokens.AnyAsync(t => t.TokenHash == token));
        var audited = await Scenario.WithDbAsync(db => db.AuditEntries
            .Where(a => a.EntityId == user.UserId.ToString() && (a.Action == "user.password_reset_issued" || a.Action == "user.password_reset"))
            .ToListAsync());

        Assert.False(stored);
        Assert.Equal(2, audited.Count);
        Assert.All(audited, a => Assert.DoesNotContain(token, (a.AfterJson ?? "") + (a.BeforeJson ?? "")));
    }

    [Fact]
    public async Task A_newer_reset_replaces_the_older_one()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var user = await UserWithPasswordAsync(RoleNames.EventManager);

        var first = await IssueAsync(ops, user.UserId);
        var second = await IssueAsync(ops, user.UserId);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(second)).StatusCode);
    }

    [Fact]
    public async Task An_expired_reset_is_refused()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        var token = await IssueAsync(ops, user.UserId);

        await Scenario.WithDbAsync(async db =>
        {
            var reset = await db.PasswordResetTokens.SingleAsync(t => t.UserId == user.UserId);
            reset.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token)).StatusCode);
    }

    [Fact]
    public async Task A_made_up_token_is_refused_on_the_token_field()
    {
        var made = await ResetAsync(Convert.ToBase64String(new byte[32]));
        var body = await made.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, made.StatusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("token", out _));
    }

    [Fact]
    public async Task A_weak_password_is_refused_and_the_reset_still_works_afterwards()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        var token = await IssueAsync(ops, user.UserId);

        var weak = await ResetAsync(token, "short");
        var body = await weak.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("newPassword", out _));
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token)).StatusCode);
    }

    [Fact]
    public async Task A_reset_clears_a_lockout()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(user.Email, "wrong-password-" + attempt);
        }

        await ResetAsync(await IssueAsync(ops, user.UserId));

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Only_the_Director_can_reset_a_Director()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var (_, director) = await Scenario.SignedInAsync(RoleNames.Director);
        var target = await UserWithPasswordAsync(RoleNames.Director);

        Assert.Equal(HttpStatusCode.Forbidden, (await ops.PostAsync($"/api/users/{target.UserId}/password-reset", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await director.PostAsync($"/api/users/{target.UserId}/password-reset", null)).StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.EventManager)]
    [InlineData(RoleNames.Accounts)]
    [InlineData(RoleNames.CasualCrew)]
    public async Task Roles_that_do_not_manage_users_cannot_issue_a_reset(string role)
    {
        var (_, client) = await Scenario.SignedInAsync(role);
        var target = await UserWithPasswordAsync(RoleNames.CasualCrew);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/users/{target.UserId}/password-reset", null)).StatusCode);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_be_reset()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var user = await UserWithPasswordAsync(RoleNames.CasualCrew);
        await Scenario.WithDbAsync(async db =>
        {
            var tracked = await db.Users.SingleAsync(u => u.UserId == user.UserId);
            tracked.IsActive = false;
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ops.PostAsync($"/api/users/{user.UserId}/password-reset", null)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_user_is_a_404()
    {
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);

        Assert.Equal(HttpStatusCode.NotFound, (await ops.PostAsync($"/api/users/{Guid.NewGuid()}/password-reset", null)).StatusCode);
    }
}
