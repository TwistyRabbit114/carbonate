using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.IntegrationTests.Commercial;

[Collection(DatabaseCollection.Name)]
public class CommercialEndpointTests(DatabaseApiFixture fixture)
{
    private readonly Scenario _scenario = new(fixture.Factory);

    /// <summary>10 x R100 (cost R40) and 2 x R400 (cost R250): R1 800 ex VAT, R2 070 with VAT, cost R900, margin 50%.</summary>
    private static readonly object[] StandardLines =
    [
        Line("Bar hire", 10, 40, 100, "Infrastructure"),
        Line("Bartenders", 2, 250, 400, "Crew"),
    ];

    /// <summary>One line of R90 000 ex VAT: R103 500 with VAT, which is above the placeholder threshold of R100 000.</summary>
    private static readonly object[] LargeLines = [Line("Full event package", 1, 60000, 90000, "Other")];

    // ---- creating and calculating ----------------------------------------------------------------

    [Fact]
    public async Task A_new_costing_is_calculated_on_the_server_with_vat_cost_margin_and_the_target_band()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);

        var response = await manager.PostAsJsonAsync($"/api/events/{eventId}/quotes", new { lines = StandardLines });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", quote.GetProperty("status").GetString());
        Assert.Equal(1, quote.GetProperty("version").GetInt32());
        Assert.Equal(1800m, quote.GetProperty("subtotalExVat").GetDecimal());
        Assert.Equal(270m, quote.GetProperty("vatAmount").GetDecimal());
        Assert.Equal(2070m, quote.GetProperty("totalIncVat").GetDecimal());
        Assert.Equal(900m, quote.GetProperty("internalCostTotal").GetDecimal());
        Assert.Equal(50m, quote.GetProperty("marginPercent").GetDecimal());
        Assert.False(quote.GetProperty("requiresApproval").GetBoolean());
        // 120 guests falls in the 101 to 300 band of the placeholder configuration.
        Assert.Equal(20m, quote.GetProperty("targetMarginBand").GetProperty("targetMinPct").GetDecimal());
        var lines = quote.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("description").GetString()!, l => l.GetProperty("lineTotal").GetDecimal());
        Assert.Equal(1000m, lines["Bar hire"]);
        Assert.Equal(800m, lines["Bartenders"]);
    }

    [Fact]
    public async Task Roles_without_costing_access_get_403_and_anonymous_gets_401()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);

        foreach (var role in new[] { RoleNames.OperationsManager, RoleNames.CrewLead, RoleNames.CasualCrew })
        {
            var (_, client) = await _scenario.SignedInAsync(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/events/{eventId}/quotes")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/events/{eventId}/quotes", new { lines = StandardLines })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Factory.CreateClient().GetAsync($"/api/events/{eventId}/quotes")).StatusCode);
    }

    [Fact]
    public async Task Costing_fields_are_present_in_the_raw_json_for_the_finance_roles()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, accounts) = await _scenario.SignedInAsync(RoleNames.Accounts);
        var eventId = await NewEventAsync(manager);
        var quote = await CreateQuoteAsync(manager, eventId);

        var raw = await (await accounts.GetAsync($"/api/quotes/{quote.GetProperty("quoteId").GetGuid()}")).Content.ReadAsStringAsync();

        Assert.Contains("\"unitCostToUs\"", raw);
        Assert.Contains("\"unitPriceToClient\"", raw);
        Assert.Contains("\"marginPercent\"", raw);
        Assert.Contains("\"lineTotal\"", raw);
    }

    [Fact]
    public async Task Invalid_lines_are_reported_with_camel_cased_nested_names()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);
        var lines = new object[]
        {
            new { description = "", quantity = 0, category = "Other", unitCostToUs = 10, unitPriceToClient = -5 },
            new { description = "No cost", quantity = 1, category = "Other", unitPriceToClient = 5 },
        };

        var response = await manager.PostAsJsonAsync($"/api/events/{eventId}/quotes", new { lines });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("lines[0].description", out _));
        Assert.True(errors.TryGetProperty("lines[0].quantity", out _));
        Assert.True(errors.TryGetProperty("lines[0].unitPriceToClient", out _));
        Assert.True(errors.TryGetProperty("lines[1].unitCostToUs", out _));
    }

    [Fact]
    public async Task A_quote_for_an_unknown_event_or_quote_is_404()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);

        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"/api/events/{Guid.NewGuid()}/quotes", new { lines = StandardLines })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/quotes/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Each_new_costing_on_an_event_takes_the_next_version()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);

        var first = await CreateQuoteAsync(manager, eventId);
        var second = await CreateQuoteAsync(manager, eventId);

        Assert.Equal(1, first.GetProperty("version").GetInt32());
        Assert.Equal(2, second.GetProperty("version").GetInt32());
        var list = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/quotes");
        Assert.Equal([2, 1], list.EnumerateArray().Select(q => q.GetProperty("version").GetInt32()));
    }

    // ---- editing and versions --------------------------------------------------------------------

    [Fact]
    public async Task Editing_a_draft_recalculates_the_totals_and_moves_the_row_version_on()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var quote = await CreateQuoteAsync(manager, await NewEventAsync(manager));
        var id = quote.GetProperty("quoteId").GetGuid();

        var response = await manager.PutAsJsonAsync($"/api/quotes/{id}", new
        {
            lines = new object[] { Line("Single item", 1, 50, 200, "Stock") },
            rowVersion = quote.GetProperty("rowVersion").GetString(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(200m, updated.GetProperty("subtotalExVat").GetDecimal());
        Assert.Equal(230m, updated.GetProperty("totalIncVat").GetDecimal());
        Assert.Equal(1, updated.GetProperty("lines").GetArrayLength());
        Assert.NotEqual(quote.GetProperty("rowVersion").GetString(), updated.GetProperty("rowVersion").GetString());
    }

    [Fact]
    public async Task A_stale_row_version_is_a_409_carrying_the_current_costing()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var quote = await CreateQuoteAsync(manager, await NewEventAsync(manager));
        var id = quote.GetProperty("quoteId").GetGuid();
        var stale = quote.GetProperty("rowVersion").GetString();
        await manager.PutAsJsonAsync($"/api/quotes/{id}", new { lines = new object[] { Line("First edit", 1, 1, 100, "Other") }, rowVersion = stale });

        var response = await manager.PutAsJsonAsync($"/api/quotes/{id}", new { lines = new object[] { Line("Second edit", 1, 1, 999, "Other") }, rowVersion = stale });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("/problems/concurrency-conflict", problem.GetProperty("type").GetString());
        Assert.Equal(100m, problem.GetProperty("current").GetProperty("subtotalExVat").GetDecimal());
    }

    [Fact]
    public async Task Editing_without_a_row_version_is_a_400()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var quote = await CreateQuoteAsync(manager, await NewEventAsync(manager));

        var response = await manager.PutAsJsonAsync($"/api/quotes/{quote.GetProperty("quoteId").GetGuid()}", new { lines = StandardLines });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editing_an_issued_costing_makes_a_new_version_and_the_old_one_keeps_its_totals()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);
        var issued = await IssueAsync(manager, eventId);
        var id = issued.GetProperty("quoteId").GetGuid();

        var response = await manager.PutAsJsonAsync($"/api/quotes/{id}", new
        {
            lines = new object[] { Line("Revised", 1, 100, 500, "Other") },
            rowVersion = issued.GetProperty("rowVersion").GetString(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var revised = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, revised.GetProperty("version").GetInt32());
        Assert.Equal("Draft", revised.GetProperty("status").GetString());
        Assert.NotEqual(id, revised.GetProperty("quoteId").GetGuid());

        var old = await manager.GetFromJsonAsync<JsonElement>($"/api/quotes/{id}");
        Assert.Equal("Superseded", old.GetProperty("status").GetString());
        Assert.Equal(2070m, old.GetProperty("totalIncVat").GetDecimal());
    }

    [Fact]
    public async Task An_accepted_costing_cannot_be_edited()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);
        var accepted = await AcceptAsync(manager, await IssueAsync(manager, eventId));

        var response = await manager.PutAsJsonAsync($"/api/quotes/{accepted.GetProperty("quoteId").GetGuid()}", new
        {
            lines = StandardLines,
            rowVersion = accepted.GetProperty("rowVersion").GetString(),
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ---- approval workflow (FR-14) ---------------------------------------------------------------

    [Fact]
    public async Task A_costing_above_the_threshold_cannot_be_issued_until_the_Director_approves_it()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var eventId = await NewEventAsync(manager);
        var draft = await CreateQuoteAsync(manager, eventId, LargeLines);
        Assert.True(draft.GetProperty("requiresApproval").GetBoolean());

        var submitted = await ActionAsync(manager, draft, "submit");
        Assert.Equal("PendingApproval", submitted.GetProperty("status").GetString());

        var blocked = await manager.PostAsJsonAsync($"/api/quotes/{submitted.GetProperty("quoteId").GetGuid()}/issue",
            new { rowVersion = submitted.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Equal("/problems/business-rule", (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());

        var approved = await ActionAsync(director, submitted, "approve");
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.False(approved.GetProperty("requiresApproval").GetBoolean());

        var issued = await ActionAsync(manager, approved, "issue");
        Assert.Equal("Issued", issued.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, issued.GetProperty("issuedAt").ValueKind);
    }

    [Fact]
    public async Task A_costing_at_or_below_the_threshold_can_be_issued_without_approval()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var draft = await CreateQuoteAsync(manager, await NewEventAsync(manager));

        var issued = await ActionAsync(manager, draft, "issue");

        Assert.Equal("Issued", issued.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_costing_the_Director_submits_is_approved_on_the_spot()
    {
        var (director, directorClient) = await _scenario.SignedInAsync(RoleNames.Director);
        var eventId = await NewEventAsync(directorClient);
        var draft = await CreateQuoteAsync(directorClient, eventId, LargeLines);

        var submitted = await ActionAsync(directorClient, draft, "submit");

        Assert.Equal("Approved", submitted.GetProperty("status").GetString());
        Assert.Equal(director.UserId, submitted.GetProperty("approvedByUserId").GetGuid());
    }

    [Fact]
    public async Task Only_someone_with_quote_approve_can_approve_and_only_a_pending_costing()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var draft = await CreateQuoteAsync(manager, await NewEventAsync(manager), LargeLines);
        var id = draft.GetProperty("quoteId").GetGuid();
        var rowVersion = draft.GetProperty("rowVersion").GetString();

        var byManager = await manager.PostAsJsonAsync($"/api/quotes/{id}/approve", new { rowVersion });
        var draftApproved = await director.PostAsJsonAsync($"/api/quotes/{id}/approve", new { rowVersion });

        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, draftApproved.StatusCode);
        Assert.Equal("/problems/invalid-transition", (await draftApproved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Changing_an_approved_costing_takes_it_back_to_draft_and_removes_the_approval()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var draft = await CreateQuoteAsync(manager, await NewEventAsync(manager), LargeLines);
        var approved = await ActionAsync(director, await ActionAsync(manager, draft, "submit"), "approve");

        var changed = await manager.PutAsJsonAsync($"/api/quotes/{approved.GetProperty("quoteId").GetGuid()}", new
        {
            lines = LargeLines,
            rowVersion = approved.GetProperty("rowVersion").GetString(),
        });

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("approvedByUserId").ValueKind);
        Assert.True(body.GetProperty("requiresApproval").GetBoolean());
    }

    [Fact]
    public async Task An_empty_costing_cannot_be_submitted_or_issued()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var empty = await CreateQuoteAsync(manager, await NewEventAsync(manager), []);
        var id = empty.GetProperty("quoteId").GetGuid();
        var rowVersion = empty.GetProperty("rowVersion").GetString();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await manager.PostAsJsonAsync($"/api/quotes/{id}/submit", new { rowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await manager.PostAsJsonAsync($"/api/quotes/{id}/issue", new { rowVersion })).StatusCode);
    }

    [Fact]
    public async Task Issuing_twice_or_accepting_a_draft_is_a_409()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var draft = await CreateQuoteAsync(manager, await NewEventAsync(manager));

        var acceptDraft = await manager.PostAsJsonAsync($"/api/quotes/{draft.GetProperty("quoteId").GetGuid()}/accept",
            new { rowVersion = draft.GetProperty("rowVersion").GetString() });
        var issued = await ActionAsync(manager, draft, "issue");
        var issueAgain = await manager.PostAsJsonAsync($"/api/quotes/{issued.GetProperty("quoteId").GetGuid()}/issue",
            new { rowVersion = issued.GetProperty("rowVersion").GetString() });

        Assert.Equal(HttpStatusCode.Conflict, acceptDraft.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, issueAgain.StatusCode);
    }

    // ---- copying (FR-15) -------------------------------------------------------------------------

    [Fact]
    public async Task A_costing_is_copied_between_events_for_the_same_client_with_a_link_to_its_source()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var earlier = await NewEventAsync(manager, reference);
        var later = await NewEventAsync(manager, reference);
        var source = await CreateQuoteAsync(manager, earlier);

        var response = await manager.PostAsJsonAsync($"/api/events/{later}/quotes/copy", new { sourceQuoteId = source.GetProperty("quoteId").GetGuid() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var copy = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(later, copy.GetProperty("eventId").GetGuid());
        Assert.Equal(source.GetProperty("quoteId").GetGuid(), copy.GetProperty("copiedFromQuoteId").GetGuid());
        Assert.Equal("Draft", copy.GetProperty("status").GetString());
        Assert.Equal(2070m, copy.GetProperty("totalIncVat").GetDecimal());
        Assert.Equal(2, copy.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task A_costing_cannot_be_copied_from_another_clients_event_or_from_nothing()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var mine = await NewEventAsync(manager);
        var theirs = await NewEventAsync(manager);
        var theirQuote = await CreateQuoteAsync(manager, theirs);

        var otherClient = await manager.PostAsJsonAsync($"/api/events/{mine}/quotes/copy", new { sourceQuoteId = theirQuote.GetProperty("quoteId").GetGuid() });
        var unknown = await manager.PostAsJsonAsync($"/api/events/{mine}/quotes/copy", new { sourceQuoteId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, otherClient.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // ---- confirmation (FR-12) --------------------------------------------------------------------

    [Fact]
    public async Task Recording_a_PO_confirms_the_event_and_writes_the_audit_entries()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);

        var response = await manager.PostAsJsonAsync($"/api/events/{eventId}/confirmation", PoBody("PO-77421"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var confirmation = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PurchaseOrder", confirmation.GetProperty("confirmationType").GetString());
        Assert.Equal("PO-77421", confirmation.GetProperty("clientPoNumber").GetString());
        Assert.Equal(25000m, confirmation.GetProperty("poAmount").GetDecimal());

        var ev = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}");
        Assert.Equal("ConfirmedInPlanning", ev.GetProperty("status").GetString());
        Assert.True(await _scenario.WithDbAsync(db => db.AuditEntries.AnyAsync(a => a.Action == "event.confirm" && a.EntityId == eventId.ToString())));
    }

    [Fact]
    public async Task A_deposit_confirmation_works_and_a_second_confirmation_is_refused()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);
        var deposit = new
        {
            confirmationType = "Deposit",
            depositAmount = 15000m,
            depositPaidDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            depositReference = "EFT-5521",
        };

        var first = await manager.PostAsJsonAsync($"/api/events/{eventId}/confirmation", deposit);
        var second = await manager.PostAsJsonAsync($"/api/events/{eventId}/confirmation", PoBody("PO-1"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    [Fact]
    public async Task A_confirmation_missing_what_its_type_needs_is_a_400()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);

        var po = await manager.PostAsJsonAsync($"/api/events/{eventId}/confirmation", new { confirmationType = "PurchaseOrder" });
        var deposit = await manager.PostAsJsonAsync($"/api/events/{eventId}/confirmation", new { confirmationType = "Deposit" });

        Assert.Equal(HttpStatusCode.BadRequest, po.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, deposit.StatusCode);
        var errors = (await deposit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("depositAmount", out _));
        Assert.True(errors.TryGetProperty("depositReference", out _));
        var event1 = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}");
        Assert.Equal("Enquired", event1.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Only_roles_that_may_record_a_confirmation_can_and_an_unknown_event_is_404()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var eventId = await NewEventAsync(manager);
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);

        Assert.Equal(HttpStatusCode.Forbidden, (await ops.PostAsJsonAsync($"/api/events/{eventId}/confirmation", PoBody("PO-9"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync($"/api/events/{Guid.NewGuid()}/confirmation", PoBody("PO-9"))).StatusCode);
    }

    // ---- invoices (FR-13) ------------------------------------------------------------------------

    [Fact]
    public async Task An_invoice_carries_the_PO_reference_defaults_its_amount_from_the_accepted_costing_and_is_due_after_the_payment_terms()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, accounts) = await _scenario.SignedInAsync(RoleNames.Accounts);
        var eventId = await NewEventAsync(manager);
        await AcceptAsync(manager, await IssueAsync(manager, eventId));
        var confirmationId = await ConfirmAsync(manager, eventId, "PO-4410");

        var response = await accounts.PostAsJsonAsync($"/api/events/{eventId}/invoices", new { confirmationId, issuedDate = "2026-11-20" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Matches(@"^INV-2026-\d{4}$", invoice.GetProperty("invoiceNumber").GetString());
        Assert.Equal("PO-4410", invoice.GetProperty("confirmationReference").GetString());
        Assert.Equal(2070m, invoice.GetProperty("amountIncVat").GetDecimal());
        Assert.Equal("Issued", invoice.GetProperty("status").GetString());
        Assert.Equal("2026-12-20", invoice.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task An_invoice_needs_a_confirmation_from_the_same_event_and_an_amount_from_somewhere()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, accounts) = await _scenario.SignedInAsync(RoleNames.Accounts);
        var reference = await _scenario.ReferenceDataAsync();
        var eventId = await NewEventAsync(manager, reference);
        var otherEvent = await NewEventAsync(manager, reference);
        var confirmationId = await ConfirmAsync(manager, eventId, "PO-1");
        var otherConfirmation = await ConfirmAsync(manager, otherEvent, "PO-2");

        var wrongEvent = await accounts.PostAsJsonAsync($"/api/events/{eventId}/invoices", new { confirmationId = otherConfirmation, issuedDate = "2026-11-20", amountIncVat = 500 });
        var unknown = await accounts.PostAsJsonAsync($"/api/events/{eventId}/invoices", new { confirmationId = Guid.NewGuid(), issuedDate = "2026-11-20", amountIncVat = 500 });
        var noAmount = await accounts.PostAsJsonAsync($"/api/events/{eventId}/invoices", new { confirmationId, issuedDate = "2026-11-20" });
        var withAmount = await accounts.PostAsJsonAsync($"/api/events/{eventId}/invoices", new { confirmationId, issuedDate = "2026-11-20", amountIncVat = 500 });

        Assert.Equal(HttpStatusCode.BadRequest, wrongEvent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noAmount.StatusCode);
        Assert.Equal(HttpStatusCode.Created, withAmount.StatusCode);
    }

    [Fact]
    public async Task Invoice_numbers_are_unique_even_when_invoices_are_raised_at_the_same_moment()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, accounts) = await _scenario.SignedInAsync(RoleNames.Accounts);
        var reference = await _scenario.ReferenceDataAsync();
        var jobs = new List<(Guid EventId, Guid ConfirmationId)>();
        for (var i = 0; i < 6; i++)
        {
            var eventId = await NewEventAsync(manager, reference);
            jobs.Add((eventId, await ConfirmAsync(manager, eventId, $"PO-RACE-{i}")));
        }

        var responses = await Task.WhenAll(jobs.Select(job => accounts.PostAsJsonAsync(
            $"/api/events/{job.EventId}/invoices",
            new { confirmationId = job.ConfirmationId, issuedDate = "2027-03-10", amountIncVat = 1000 })));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var numbers = new List<string>();
        foreach (var response in responses)
        {
            numbers.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceNumber").GetString()!);
        }

        Assert.Equal(6, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.Matches(@"^INV-2027-\d{4}$", n));
    }

    [Fact]
    public async Task Only_Director_and_Accounts_manage_invoices_but_Event_Managers_can_view_them()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var eventId = await NewEventAsync(manager);
        var confirmationId = await ConfirmAsync(manager, eventId, "PO-X");
        var body = new { confirmationId, issuedDate = "2026-11-20", amountIncVat = 500 };

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/api/events/{eventId}/invoices", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ops.PostAsJsonAsync($"/api/events/{eventId}/invoices", body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ops.GetAsync("/api/invoices")).StatusCode);
    }

    [Fact]
    public async Task An_invoice_is_marked_paid_once_with_a_valid_date_and_can_be_voided_before_payment()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, accounts) = await _scenario.SignedInAsync(RoleNames.Accounts);
        var reference = await _scenario.ReferenceDataAsync();
        var paidId = await NewInvoiceAsync(manager, accounts, reference);
        var voidId = await NewInvoiceAsync(manager, accounts, reference);

        var noDate = await accounts.PatchAsJsonAsync($"/api/invoices/{paidId}", new { status = "Paid" });
        var tooEarly = await accounts.PatchAsJsonAsync($"/api/invoices/{paidId}", new { status = "Paid", paidDate = "2020-01-01" });
        var paid = await accounts.PatchAsJsonAsync($"/api/invoices/{paidId}", new { status = "Paid", paidDate = "2026-12-01" });
        var paidAgain = await accounts.PatchAsJsonAsync($"/api/invoices/{paidId}", new { status = "Paid", paidDate = "2026-12-02" });
        var voided = await accounts.PatchAsJsonAsync($"/api/invoices/{voidId}", new { status = "Void" });
        var payVoided = await accounts.PatchAsJsonAsync($"/api/invoices/{voidId}", new { status = "Paid", paidDate = "2026-12-01" });

        Assert.Equal(HttpStatusCode.BadRequest, noDate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("Paid", (await paid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, paidAgain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, payVoided.StatusCode);
    }

    [Fact]
    public async Task The_invoice_list_filters_by_status_and_pages()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, accounts) = await _scenario.SignedInAsync(RoleNames.Accounts);
        var reference = await _scenario.ReferenceDataAsync();
        var paidId = await NewInvoiceAsync(manager, accounts, reference);
        var openId = await NewInvoiceAsync(manager, accounts, reference);
        await accounts.PatchAsJsonAsync($"/api/invoices/{paidId}", new { status = "Paid", paidDate = "2027-04-01" });

        var paid = await accounts.GetFromJsonAsync<JsonElement>("/api/invoices?status=Paid&pageSize=200");
        var issued = await accounts.GetFromJsonAsync<JsonElement>("/api/invoices?status=Issued&pageSize=200");
        var page = await accounts.GetFromJsonAsync<JsonElement>("/api/invoices?pageSize=1&page=1");

        Assert.Contains(paidId, paid.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("invoiceId").GetGuid()));
        Assert.DoesNotContain(openId, paid.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("invoiceId").GetGuid()));
        Assert.Contains(openId, issued.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("invoiceId").GetGuid()));
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.True(page.GetProperty("total").GetInt32() >= 2);
    }

    // ---- cost history (FR-09) --------------------------------------------------------------------

    [Fact]
    public async Task Cost_history_lists_the_same_clients_earlier_events_with_their_totals_cost_and_margin()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var otherReference = await _scenario.ReferenceDataAsync();
        var earlier = await NewEventAsync(manager, reference);
        var current = await NewEventAsync(manager, reference);
        var stranger = await NewEventAsync(manager, otherReference);
        await AcceptAsync(manager, await IssueAsync(manager, earlier));
        await CreateQuoteAsync(manager, stranger);

        var response = await manager.GetAsync($"/api/events/{current}/cost-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var item = Assert.Single(items);
        Assert.Equal(earlier, item.GetProperty("eventId").GetGuid());
        Assert.Equal(2070m, item.GetProperty("totalIncVat").GetDecimal());
        Assert.Equal(900m, item.GetProperty("internalCostTotal").GetDecimal());
        Assert.Equal(50m, item.GetProperty("marginPercent").GetDecimal());
    }

    [Fact]
    public async Task Cost_history_is_for_finance_roles_only_and_an_unknown_event_is_404()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var eventId = await NewEventAsync(manager);

        Assert.Equal(HttpStatusCode.Forbidden, (await ops.GetAsync($"/api/events/{eventId}/cost-history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/events/{Guid.NewGuid()}/cost-history")).StatusCode);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static object Line(string description, decimal quantity, decimal cost, decimal price, string category) =>
        new { description, quantity, category, unitCostToUs = cost, unitPriceToClient = price };

    private static object PoBody(string number) => new
    {
        confirmationType = "PurchaseOrder",
        clientPoNumber = number,
        poReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        poAmount = 25000m,
    };

    private async Task<Guid> NewEventAsync(HttpClient client, Scenario.ReferenceData? reference = null)
    {
        reference ??= await _scenario.ReferenceDataAsync();
        var response = await client.PostAsJsonAsync("/api/events", Scenario.NewEventBody(reference));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("eventId").GetGuid();
    }

    private static async Task<JsonElement> CreateQuoteAsync(HttpClient client, Guid eventId, object[]? lines = null)
    {
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/quotes", new { lines = lines ?? StandardLines });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> ActionAsync(HttpClient client, JsonElement quote, string action)
    {
        var response = await client.PostAsJsonAsync($"/api/quotes/{quote.GetProperty("quoteId").GetGuid()}/{action}",
            new { rowVersion = quote.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> IssueAsync(HttpClient client, Guid eventId) =>
        await ActionAsync(client, await CreateQuoteAsync(client, eventId), "issue");

    private static Task<JsonElement> AcceptAsync(HttpClient client, JsonElement issued) => ActionAsync(client, issued, "accept");

    private static async Task<Guid> ConfirmAsync(HttpClient client, Guid eventId, string poNumber)
    {
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/confirmation", PoBody(poNumber));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmationId").GetGuid();
    }

    private async Task<Guid> NewInvoiceAsync(HttpClient manager, HttpClient accounts, Scenario.ReferenceData reference)
    {
        var eventId = await NewEventAsync(manager, reference);
        var confirmationId = await ConfirmAsync(manager, eventId, $"PO-{Guid.NewGuid():N}"[..12]);
        var response = await accounts.PostAsJsonAsync($"/api/events/{eventId}/invoices",
            new { confirmationId, issuedDate = "2026-11-20", amountIncVat = 1500 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetGuid();
    }
}

internal static class HttpClientPatchExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string url, T value) =>
        client.PatchAsync(url, JsonContent.Create(value));
}
