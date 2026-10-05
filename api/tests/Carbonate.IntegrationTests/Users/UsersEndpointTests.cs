using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.IntegrationTests.Users;

[Collection(DatabaseCollection.Name)]
public class UsersEndpointTests(DatabaseApiFixture fixture)
{
    private const string GoodPassword = "correct-horse-battery-staple";

    private readonly Scenario _scenario = new(fixture.Factory);

    // ---- create ----------------------------------------------------------------------------------

    [Fact]
    public async Task The_Director_creates_a_user_who_can_then_sign_in_and_the_password_is_not_stored_or_audited()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var body = NewUserBody([RoleNames.EventManager]);

        var response = await director.PostAsJsonAsync("/api/users", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([RoleNames.EventManager], created.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.True(created.GetProperty("isActive").GetBoolean());
        var userId = created.GetProperty("userId").GetGuid();

        var stored = await _scenario.WithDbAsync(db => db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId));
        Assert.DoesNotContain(GoodPassword, stored.PasswordHash);
        var audit = await _scenario.WithDbAsync(db => db.AuditEntries.FirstAsync(a => a.Action == "user.create" && a.EntityId == userId.ToString()));
        Assert.DoesNotContain(GoodPassword, audit.AfterJson ?? "");
        Assert.DoesNotContain("password", (audit.AfterJson ?? "").ToLowerInvariant());

        var signIn = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = (string)body["email"]!, password = GoodPassword });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.False(string.IsNullOrEmpty((await signIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task A_user_given_a_finance_role_must_set_up_two_step_sign_in_on_first_login()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var body = NewUserBody([RoleNames.Accounts]);
        Assert.Equal(HttpStatusCode.Created, (await director.PostAsJsonAsync("/api/users", body)).StatusCode);

        var signIn = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = (string)body["email"]!, password = GoodPassword });

        var session = await signIn.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(session.GetProperty("mfaRequired").GetBoolean());
        Assert.True(session.GetProperty("mfaEnrolmentRequired").GetBoolean());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("accessToken").ValueKind);
    }

    [Fact]
    public async Task Operations_manage_users_but_cannot_create_a_Director()
    {
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);

        var crew = await ops.PostAsJsonAsync("/api/users", NewUserBody([RoleNames.CasualCrew]));
        var director = await ops.PostAsJsonAsync("/api/users", NewUserBody([RoleNames.Director]));

        Assert.Equal(HttpStatusCode.Created, crew.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, director.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.EventManager)]
    [InlineData(RoleNames.Accounts)]
    [InlineData(RoleNames.CrewLead)]
    [InlineData(RoleNames.CasualCrew)]
    public async Task Roles_that_do_not_manage_users_get_403_everywhere(string role)
    {
        var (_, client) = await _scenario.SignedInAsync(role);
        var target = await _scenario.UserAsync(RoleNames.CasualCrew);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", NewUserBody([RoleNames.CasualCrew]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsync($"/api/users/{target.UserId}", JsonContent.Create(new { isActive = false }))).StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_get_401()
    {
        var anonymous = fixture.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/audit")).StatusCode);
    }

    [Fact]
    public async Task Bad_input_is_reported_field_by_field()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var existing = await _scenario.UserAsync(RoleNames.CasualCrew);
        var body = NewUserBody(["Wizard"]);
        body["email"] = existing.Email;
        body["employeeNumber"] = existing.EmployeeNumber;
        body["initialPassword"] = "short";

        var response = await director.PostAsJsonAsync("/api/users", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "roles", "email", "employeeNumber", "initialPassword" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"missing {field}");
        }
    }

    [Fact]
    public async Task A_password_found_in_a_breach_is_refused()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var body = NewUserBody([RoleNames.CasualCrew]);
        body["initialPassword"] = "a-breached-password-123";

        var response = await director.PostAsJsonAsync("/api/users", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("initialPassword", out _));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task An_invalid_email_is_a_400(string email)
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var body = NewUserBody([RoleNames.CasualCrew]);
        body["email"] = email;

        Assert.Equal(HttpStatusCode.BadRequest, (await director.PostAsJsonAsync("/api/users", body)).StatusCode);
    }

    // ---- update ----------------------------------------------------------------------------------

    [Fact]
    public async Task Changing_roles_updates_them_ends_the_persons_sessions_and_is_audited()
    {
        var (admin, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var target = await _scenario.UserAsync(RoleNames.CasualCrew);
        var oldStamp = target.SecurityStamp;
        await GiveRefreshTokenAsync(target.UserId);

        var response = await director.PatchAsync($"/api/users/{target.UserId}", JsonContent.Create(new { roles = new[] { RoleNames.CrewLead } }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([RoleNames.CrewLead], updated.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        await AssertSessionsEndedAsync(target.UserId, oldStamp);
        var entry = await _scenario.WithDbAsync(db => db.AuditEntries.FirstAsync(a => a.Action == "user.update" && a.EntityId == target.UserId.ToString()));
        Assert.Equal(admin.UserId, entry.UserId);
        Assert.Contains(RoleNames.CrewLead, entry.AfterJson);
    }

    [Fact]
    public async Task Changing_only_the_name_does_not_end_anyones_sessions()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var target = await _scenario.UserAsync(RoleNames.CasualCrew);
        await GiveRefreshTokenAsync(target.UserId);

        var response = await director.PatchAsync($"/api/users/{target.UserId}", JsonContent.Create(new { fullName = "Renamed Person" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Renamed Person", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fullName").GetString());
        var tokens = await _scenario.WithDbAsync(db => db.RefreshTokens.Where(t => t.UserId == target.UserId).ToListAsync());
        Assert.All(tokens, t => Assert.Null(t.RevokedAt));
    }

    [Fact]
    public async Task Deactivating_blocks_sign_in_and_ends_sessions_and_reactivating_restores_it()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var body = NewUserBody([RoleNames.CasualCrew]);
        var created = await (await director.PostAsJsonAsync("/api/users", body)).Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("userId").GetGuid();
        var credentials = new { email = (string)body["email"]!, password = GoodPassword };
        await GiveRefreshTokenAsync(userId);
        var stamp = (await _scenario.WithDbAsync(db => db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId))).SecurityStamp;

        var off = await director.PatchAsync($"/api/users/{userId}", JsonContent.Create(new { isActive = false }));
        var blocked = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/login", credentials);

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await off.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
        await AssertSessionsEndedAsync(userId, stamp);

        var on = await director.PatchAsync($"/api/users/{userId}", JsonContent.Create(new { isActive = true }));
        var back = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/login", credentials);

        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.Equal(HttpStatusCode.OK, back.StatusCode);
    }

    [Fact]
    public async Task Nobody_can_change_their_own_roles_or_deactivate_themselves()
    {
        var admin = await _scenario.UserAsync(RoleNames.Director, RoleNames.OperationsManager);
        var client = _scenario.ClientFor(admin, RoleNames.Director, RoleNames.OperationsManager);

        var roles = await client.PatchAsync($"/api/users/{admin.UserId}", JsonContent.Create(new { roles = new[] { RoleNames.Director } }));
        var deactivate = await client.PatchAsync($"/api/users/{admin.UserId}", JsonContent.Create(new { isActive = false }));
        var rename = await client.PatchAsync($"/api/users/{admin.UserId}", JsonContent.Create(new { fullName = "Still Me" }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, roles.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    [Fact]
    public async Task Operations_cannot_change_a_Director_or_hand_out_the_Director_role_but_the_Director_can()
    {
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var otherDirector = await _scenario.UserAsync(RoleNames.Director);
        var crew = await _scenario.UserAsync(RoleNames.CasualCrew);

        var touchDirector = await ops.PatchAsync($"/api/users/{otherDirector.UserId}", JsonContent.Create(new { fullName = "Hijacked" }));
        var promote = await ops.PatchAsync($"/api/users/{crew.UserId}", JsonContent.Create(new { roles = new[] { RoleNames.Director } }));
        var byDirector = await director.PatchAsync($"/api/users/{crew.UserId}", JsonContent.Create(new { roles = new[] { RoleNames.Director } }));

        Assert.Equal(HttpStatusCode.Forbidden, touchDirector.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byDirector.StatusCode);
    }

    [Fact]
    public async Task Updating_an_unknown_user_is_404_an_empty_change_is_400_and_an_unknown_role_is_400()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var target = await _scenario.UserAsync(RoleNames.CasualCrew);

        var unknown = await director.PatchAsync($"/api/users/{Guid.NewGuid()}", JsonContent.Create(new { isActive = false }));
        var empty = await director.PatchAsync($"/api/users/{target.UserId}", JsonContent.Create(new { }));
        var badRole = await director.PatchAsync($"/api/users/{target.UserId}", JsonContent.Create(new { roles = new[] { "Wizard" } }));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
    }

    // ---- list ------------------------------------------------------------------------------------

    [Fact]
    public async Task The_list_filters_by_text_role_and_active_state_and_pages()
    {
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var marker = $"Zq{Guid.NewGuid():N}"[..10];
        foreach (var name in new[] { "Alpha", "Bravo" })
        {
            var body = NewUserBody([RoleNames.CasualCrew]);
            body["fullName"] = $"{marker} {name}";
            Assert.Equal(HttpStatusCode.Created, (await director.PostAsJsonAsync("/api/users", body)).StatusCode);
        }

        var all = await ops.GetFromJsonAsync<JsonElement>($"/api/users?q={marker}");
        var crew = await ops.GetFromJsonAsync<JsonElement>($"/api/users?q={marker}&role={RoleNames.CasualCrew}");
        var directors = await ops.GetFromJsonAsync<JsonElement>($"/api/users?q={marker}&role={RoleNames.Director}");
        var inactive = await ops.GetFromJsonAsync<JsonElement>($"/api/users?q={marker}&isActive=false");
        var page = await ops.GetFromJsonAsync<JsonElement>($"/api/users?q={marker}&pageSize=1&page=2");

        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.Equal(2, crew.GetProperty("total").GetInt32());
        Assert.Equal(0, directors.GetProperty("total").GetInt32());
        Assert.Equal(0, inactive.GetProperty("total").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.DoesNotContain("passwordHash", all.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mfaSecret", all.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_page_size_over_the_cap_is_a_400()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);

        Assert.Equal(HttpStatusCode.BadRequest, (await director.GetAsync("/api/users?pageSize=1000")).StatusCode);
    }

    // ---- audit view (FR-37) ----------------------------------------------------------------------

    [Fact]
    public async Task The_Director_reads_the_audit_trail_filtered_by_entity_and_date_and_nobody_else_can()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var created = await (await director.PostAsJsonAsync("/api/users", NewUserBody([RoleNames.CasualCrew]))).Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("userId").GetGuid();

        var trail = await director.GetFromJsonAsync<JsonElement>("/api/audit?entity=AppUser&pageSize=200");
        var future = await director.GetFromJsonAsync<JsonElement>($"/api/audit?from={Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("O"))}");

        var entry = trail.GetProperty("items").EnumerateArray().First(i => i.GetProperty("entityId").GetString() == userId.ToString());
        Assert.Equal("user.create", entry.GetProperty("action").GetString());
        Assert.False(string.IsNullOrEmpty(entry.GetProperty("userName").GetString()));
        Assert.All(trail.GetProperty("items").EnumerateArray(), i => Assert.Equal("AppUser", i.GetProperty("entityName").GetString()));
        Assert.Equal(0, future.GetProperty("total").GetInt32());

        foreach (var role in new[] { RoleNames.OperationsManager, RoleNames.EventManager, RoleNames.Accounts, RoleNames.CrewLead })
        {
            var (_, client) = await _scenario.SignedInAsync(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit")).StatusCode);
        }
    }

    [Fact]
    public async Task The_audit_trail_lists_newest_first_and_an_end_before_the_start_is_a_400()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        await director.PostAsJsonAsync("/api/users", NewUserBody([RoleNames.CasualCrew]));

        var trail = await director.GetFromJsonAsync<JsonElement>("/api/audit?pageSize=50");
        var times = trail.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("occurredAt").GetDateTime()).ToList();
        var backwards = await director.GetAsync($"/api/audit?from={Uri.EscapeDataString(DateTime.UtcNow.ToString("O"))}&to={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"))}");

        Assert.Equal(times.OrderByDescending(t => t), times);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static Dictionary<string, object?> NewUserBody(string[] roles)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new Dictionary<string, object?>
        {
            ["employeeNumber"] = $"E-{unique[..10]}",
            ["email"] = $"{unique}@example.test",
            ["fullName"] = "New Person",
            ["employmentType"] = "Permanent",
            ["roles"] = roles,
            ["initialPassword"] = GoodPassword,
        };
    }

    private Task GiveRefreshTokenAsync(Guid userId) => _scenario.WithDbAsync(async db =>
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            CreatedByIp = "127.0.0.1",
        });
        await db.SaveChangesAsync();
    });

    private async Task AssertSessionsEndedAsync(Guid userId, string oldStamp)
    {
        var user = await _scenario.WithDbAsync(db => db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId));
        var tokens = await _scenario.WithDbAsync(db => db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync());

        Assert.NotEqual(oldStamp, user.SecurityStamp);
        Assert.NotEmpty(tokens);
        Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
    }
}
