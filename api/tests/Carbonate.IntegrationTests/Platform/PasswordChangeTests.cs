using System.Net;
using System.Net.Http.Headers;
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
/// Changing your own password from the Your account page (FR-34). Runs in its own database with the
/// sign-in throttle raised.
/// </summary>
public class PasswordChangeTests(DatabaseApiFixture fixture) : IClassFixture<DatabaseApiFixture>
{
    private const string OldPassword = "Correct-Horse-Battery-9!";
    private const string NewPassword = "Purple-Lantern-Harbour-42";
    private const string RefreshCookie = "carbonate.refresh";

    private readonly WebApplicationFactory<Program> _factory =
        fixture.Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:LoginPermitPerMinute", "1000"));

    private Scenario Scenario => new(fixture.Factory);

    private sealed record Session(HttpClient Client, string Cookie);

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

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task<Session> SignInAsync(string email)
    {
        var login = await LoginAsync(email, OldPassword);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var cookie = login.Headers.GetValues("Set-Cookie").First(h => h.StartsWith(RefreshCookie + "=", StringComparison.Ordinal)).Split(';')[0];

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return new Session(client, cookie);
    }

    private static Task<HttpResponseMessage> ChangeAsync(Session session, string current, string next)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password")
        {
            Content = JsonContent.Create(new { currentPassword = current, newPassword = next }),
        };
        request.Headers.Add("Cookie", session.Cookie);
        return session.Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> RefreshAsync(string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return _factory.CreateClient().SendAsync(request);
    }

    private static async Task<JsonElement> ErrorsAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

    [Fact]
    public async Task Changing_the_password_keeps_this_device_and_signs_out_the_others()
    {
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        var here = await SignInAsync(user.Email);
        var elsewhere = await SignInAsync(user.Email);

        var change = await ChangeAsync(here, OldPassword, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(here.Cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(elsewhere.Cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email, NewPassword)).StatusCode);
        Assert.True(await Scenario.WithDbAsync(db => db.AuditEntries.AnyAsync(a =>
            a.Action == "user.password_changed" && a.EntityId == user.UserId.ToString())));
    }

    [Fact]
    public async Task A_wrong_current_password_is_a_400_on_that_field_not_a_401()
    {
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        var session = await SignInAsync(user.Email);

        var change = await ChangeAsync(session, "not-my-password", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        Assert.True((await ErrorsAsync(change)).TryGetProperty("currentPassword", out _));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task Wrong_guesses_count_towards_lockout()
    {
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        var session = await SignInAsync(user.Email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await ChangeAsync(session, "wrong-" + attempt, NewPassword);
        }

        // Locked: even the right current password is refused, and so is signing in.
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(session, OldPassword, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task A_weak_or_unchanged_new_password_is_a_400_on_the_new_password()
    {
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        var session = await SignInAsync(user.Email);

        var weak = await ChangeAsync(session, OldPassword, "short");
        var same = await ChangeAsync(session, OldPassword, OldPassword);

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.True((await ErrorsAsync(weak)).TryGetProperty("newPassword", out _));
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.True((await ErrorsAsync(same)).TryGetProperty("newPassword", out _));
    }

    [Fact]
    public async Task Signing_in_is_required()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/password",
            new { currentPassword = OldPassword, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_passwords_never_reach_the_audit_trail()
    {
        var user = await UserWithPasswordAsync(RoleNames.EventManager);
        var session = await SignInAsync(user.Email);
        await ChangeAsync(session, "not-my-password", NewPassword);
        await ChangeAsync(session, OldPassword, NewPassword);

        var entries = await Scenario.WithDbAsync(db => db.AuditEntries.Where(a => a.EntityId == user.UserId.ToString()).ToListAsync());

        Assert.All(entries, a =>
        {
            var json = (a.AfterJson ?? "") + (a.BeforeJson ?? "");
            Assert.DoesNotContain(OldPassword, json);
            Assert.DoesNotContain(NewPassword, json);
        });
    }
}
