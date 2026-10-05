using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.IntegrationTests.Users;

/// <summary>
/// The "at least one active Director" rule needs a database where the test controls every Director, so
/// this class gets its own, not the shared one.
/// </summary>
public class LastDirectorTests(DatabaseApiFixture fixture) : IClassFixture<DatabaseApiFixture>
{
    private readonly Scenario _scenario = new(fixture.Factory);

    [Fact]
    public async Task A_change_that_would_leave_no_active_Director_is_refused_and_undone()
    {
        var first = await _scenario.UserAsync(RoleNames.Director);
        var second = await _scenario.UserAsync(RoleNames.Director);
        var byFirst = _scenario.ClientFor(first, RoleNames.Director);
        var bySecond = _scenario.ClientFor(second, RoleNames.Director);

        // The second Director retires the first. One active Director remains, so this is fine.
        var retire = await bySecond.PatchAsync($"/api/users/{first.UserId}", JsonContent.Create(new { isActive = false }));
        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);

        // The first Director's access token is still valid for a few minutes. Using it to retire the
        // second would leave nobody, so it must be refused.
        var last = await byFirst.PatchAsync($"/api/users/{second.UserId}", JsonContent.Create(new { isActive = false }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, last.StatusCode);
        var problem = await last.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("at least one active Director", problem.GetProperty("detail").GetString());
        Assert.True((await _scenario.WithDbAsync(db => db.Users.AsNoTracking().FirstAsync(u => u.UserId == second.UserId))).IsActive);
        Assert.Equal(1, await ActiveDirectorsAsync());
    }

    [Fact]
    public async Task Removing_the_Director_role_from_the_last_Director_is_refused_and_undone()
    {
        // Reset to exactly one active Director.
        await _scenario.WithDbAsync(async db =>
        {
            await db.Users.Where(u => u.UserRoles.Any(r => r.Role.Name == RoleNames.Director))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        });
        var only = await _scenario.UserAsync(RoleNames.Director);
        var ops = await _scenario.UserAsync(RoleNames.Director, RoleNames.OperationsManager);
        await _scenario.WithDbAsync(db => db.Users.Where(u => u.UserId == ops.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false)));
        var actor = _scenario.ClientFor(ops, RoleNames.Director, RoleNames.OperationsManager);

        var response = await actor.PatchAsync($"/api/users/{only.UserId}", JsonContent.Create(new { roles = new[] { RoleNames.CasualCrew } }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var roles = await _scenario.WithDbAsync(db => db.UserRoles.Where(r => r.UserId == only.UserId).Select(r => r.Role.Name).ToListAsync());
        Assert.Equal([RoleNames.Director], roles);
    }

    private Task<int> ActiveDirectorsAsync() => _scenario.WithDbAsync(db =>
        db.Users.CountAsync(u => u.IsActive && u.UserRoles.Any(r => r.Role.Name == RoleNames.Director)));
}
