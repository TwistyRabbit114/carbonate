using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Infrastructure.Seeding;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace Carbonate.IntegrationTests.Users;

/// <summary>
/// The demo environment is only useful if every demo account can actually be signed into. The Director and
/// Accounts need a two-step code, so the secret behind it has to be known (it is in the README). Runs in
/// its own database so the full demo seed happens from scratch.
/// </summary>
public class DemoAccountsTests(DatabaseApiFixture fixture) : IClassFixture<DatabaseApiFixture>, IAsyncLifetime
{
    private const string DemoPassword = "Carbonate-Demo-2026!";

    public async Task InitializeAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TemplateSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("director@carbon.demo", "Director")]
    [InlineData("accounts@carbon.demo", "Accounts")]
    public async Task The_demo_Director_and_Accounts_complete_two_step_sign_in_with_the_documented_secret(string email, string role)
    {
        var token = await SignInWithCodeAsync(email);

        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal([role], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Contains("finance.view_client_price", me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
    }

    [Theory]
    [InlineData("ops@carbon.demo")]
    [InlineData("sarah@carbon.demo")]
    [InlineData("thabo@carbon.demo")]
    [InlineData("priya@carbon.demo")]
    public async Task The_other_demo_accounts_sign_in_with_just_the_password(string email)
    {
        var response = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(session.GetProperty("mfaRequired").GetBoolean());
        Assert.False(string.IsNullOrEmpty(session.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task A_wrong_code_is_still_refused_for_the_demo_Director()
    {
        var login = await (await fixture.Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = "director@carbon.demo", password = DemoPassword }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var response = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/mfa/verify",
            new { mfaToken = login.GetProperty("mfaToken").GetString(), code = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Accounts_seeded_with_an_unknown_secret_are_put_right_the_next_time_the_seeder_runs()
    {
        // The state the hosted dev site was in: MFA enabled, but with a random secret nobody kept.
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Carbonate.Infrastructure.Persistence.CemDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
            foreach (var account in await db.Users.Where(u => u.Email == "director@carbon.demo" || u.Email == "accounts@carbon.demo").ToListAsync())
            {
                account.MfaSecretEncrypted = protector.Protect(Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20)));
            }

            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }

        Assert.False(string.IsNullOrEmpty(await SignInWithCodeAsync("director@carbon.demo")));
        Assert.False(string.IsNullOrEmpty(await SignInWithCodeAsync("accounts@carbon.demo")));
    }

    [Fact]
    public async Task Running_the_seeder_again_changes_nothing_else()
    {
        var before = await CountsAsync();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }

        Assert.Equal(before, await CountsAsync());
    }

    // ---- the money the demo exists to show -------------------------------------------------------

    [Fact]
    public async Task The_same_event_shows_its_budget_to_the_Event_Manager_and_leaves_it_out_for_Operations()
    {
        var sarah = await SignedInClientAsync("sarah@carbon.demo");
        var ops = await SignedInClientAsync("ops@carbon.demo");
        var eventId = await EventIdAsync(sarah, "NAI-WED-26");

        var forSarah = await sarah.GetStringAsync($"/api/events/{eventId}");
        var forOps = await ops.GetStringAsync($"/api/events/{eventId}");

        Assert.Contains("\"budgetAmount\":150000", forSarah);
        Assert.DoesNotContain("budgetAmount", forOps);
    }

    [Fact]
    public async Task Costings_show_price_cost_and_margin_to_finance_roles_and_are_closed_to_everyone_else()
    {
        var sarah = await SignedInClientAsync("sarah@carbon.demo");
        var ops = await SignedInClientAsync("ops@carbon.demo");
        var priya = await SignedInClientAsync("priya@carbon.demo");
        var eventId = await EventIdAsync(sarah, "NAI-WED-26");

        var quotes = await sarah.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/quotes");

        var quote = Assert.Single(quotes.EnumerateArray());
        Assert.Equal("Issued", quote.GetProperty("status").GetString());
        Assert.Equal(120520m, quote.GetProperty("subtotalExVat").GetDecimal());
        Assert.True(quote.TryGetProperty("internalCostTotal", out _));
        Assert.True(quote.TryGetProperty("marginPercent", out _));
        Assert.True(quote.GetProperty("lines")[0].TryGetProperty("unitCostToUs", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await ops.GetAsync($"/api/events/{eventId}/quotes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await priya.GetAsync($"/api/events/{eventId}/quotes")).StatusCode);
    }

    [Fact]
    public async Task The_Director_can_approve_the_waiting_costing_live()
    {
        var sarah = await SignedInClientAsync("sarah@carbon.demo");
        var director = fixture.Factory.CreateClient();
        director.DefaultRequestHeaders.Authorization = new("Bearer", await SignInWithCodeAsync("director@carbon.demo"));
        var eventId = await EventIdAsync(sarah, "VAN-ACT-26");

        var waiting = (await sarah.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/quotes")).EnumerateArray().Single();
        Assert.Equal("PendingApproval", waiting.GetProperty("status").GetString());
        Assert.True(waiting.GetProperty("requiresApproval").GetBoolean());

        var approved = await director.PostAsJsonAsync($"/api/quotes/{waiting.GetProperty("quoteId").GetGuid()}/approve",
            new { rowVersion = waiting.GetProperty("rowVersion").GetString() });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Approved", (await approved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Accounts_see_the_demo_invoices_and_the_Event_Manager_sees_them_too_but_Operations_does_not()
    {
        var accounts = fixture.Factory.CreateClient();
        accounts.DefaultRequestHeaders.Authorization = new("Bearer", await SignInWithCodeAsync("accounts@carbon.demo"));
        var ops = await SignedInClientAsync("ops@carbon.demo");

        var invoices = (await accounts.GetFromJsonAsync<JsonElement>("/api/invoices?pageSize=50")).GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(["Issued", "Paid"], invoices.Select(i => i.GetProperty("status").GetString()!).Order());
        Assert.All(invoices, i => Assert.True(i.TryGetProperty("amountIncVat", out _)));
        Assert.All(invoices, i => Assert.Matches(@"^INV-\d{4}-\d{4}$", i.GetProperty("invoiceNumber").GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await ops.GetAsync("/api/invoices")).StatusCode);
    }

    [Fact]
    public async Task Every_demo_event_past_enquiry_has_a_confirmation_and_a_budget()
    {
        var sarah = await SignedInClientAsync("sarah@carbon.demo");

        foreach (var code in new[] { "DEL-GOLF-26", "RIV-FEST-26", "NAI-WED-26", "VAN-ACT-26", "MER-YE-26" })
        {
            var eventId = await EventIdAsync(sarah, code);
            Assert.Contains("\"budgetAmount\"", await sarah.GetStringAsync($"/api/events/{eventId}"));
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Carbonate.Infrastructure.Persistence.CemDbContext>();
        var unconfirmed = await db.Events.Where(e => e.Status != Carbonate.Domain.Common.EventStatus.Enquired
                && !db.EventConfirmations.Any(c => c.EventId == e.EventId)).CountAsync();
        Assert.Equal(0, unconfirmed);
    }

    [Fact]
    public async Task A_demo_seeded_before_costings_existed_gets_its_money_the_next_time_the_seeder_runs()
    {
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Carbonate.Infrastructure.Persistence.CemDbContext>();
            db.Invoices.RemoveRange(db.Invoices);
            db.EventConfirmations.RemoveRange(db.EventConfirmations);
            db.Quotes.RemoveRange(db.Quotes);
            await db.SaveChangesAsync();
            await db.Events.ExecuteUpdateAsync(s => s.SetProperty(e => e.BudgetAmount, (decimal?)null));
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }

        var sarah = await SignedInClientAsync("sarah@carbon.demo");
        var eventId = await EventIdAsync(sarah, "RIV-FEST-26");
        Assert.Contains("\"budgetAmount\":700000", await sarah.GetStringAsync($"/api/events/{eventId}"));
        Assert.Equal(5, (await CountsAsync()).Quotes);
    }

    private async Task<HttpClient> SignedInClientAsync(string email)
    {
        var client = fixture.Factory.CreateClient();
        var session = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> EventIdAsync(HttpClient client, string code)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/events?q={code}");
        return page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("eventCode").GetString() == code).GetProperty("eventId").GetGuid();
    }

    private async Task<(int Users, int Events, int Cards, int Quotes, int Invoices)> CountsAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Carbonate.Infrastructure.Persistence.CemDbContext>();
        return (await db.Users.CountAsync(), await db.Events.CountAsync(), await db.TaskCards.CountAsync(),
            await db.Quotes.CountAsync(), await db.Invoices.CountAsync());
    }

    private async Task<string> SignInWithCodeAsync(string email)
    {
        var client = fixture.Factory.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(login.GetProperty("mfaRequired").GetBoolean());
        Assert.False(login.GetProperty("mfaEnrolmentRequired").GetBoolean());

        var code = new Totp(Base32Encoding.ToBytes(DemoDataSeeder.DemoTotpSecret)).ComputeTotp();
        var verify = await client.PostAsJsonAsync("/api/auth/mfa/verify", new { mfaToken = login.GetProperty("mfaToken").GetString(), code });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }
}
