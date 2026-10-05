using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.IntegrationTests.Events;

[Collection(DatabaseCollection.Name)]
public class EventsEndpointTests(DatabaseApiFixture fixture)
{
    private readonly Scenario _scenario = new(fixture.Factory);

    private static readonly MilestoneType[] ChainOrder =
    [
        MilestoneType.SiteVisit, MilestoneType.LoadIn, MilestoneType.Rehearsal, MilestoneType.Doors,
        MilestoneType.Strike, MilestoneType.LoadOut, MilestoneType.Debrief, MilestoneType.Invoice,
        MilestoneType.Reconciliation,
    ];

    // ---- pack size (FR-08) -----------------------------------------------------------------------

    [Fact]
    public async Task Recording_the_actual_pack_size_keeps_the_estimate_and_gives_the_event_a_new_version()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var eventId = created.GetProperty("eventId").GetGuid();
        var estimate = created.GetProperty("packSizeEstimated").GetInt32();

        var response = await manager.PatchAsJsonAsync($"/api/events/{eventId}/pack-size",
            new { packSizeActual = 412, rowVersion = created.GetProperty("rowVersion").GetString() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(412, updated.GetProperty("packSizeActual").GetInt32());
        Assert.Equal(estimate, updated.GetProperty("packSizeEstimated").GetInt32());
        Assert.NotEqual(created.GetProperty("rowVersion").GetString(), updated.GetProperty("rowVersion").GetString());
        Assert.True(await _scenario.WithDbAsync(db =>
            db.AuditEntries.AnyAsync(a => a.Action == "event.pack_size" && a.EntityId == eventId.ToString())));
    }

    [Fact]
    public async Task A_pack_size_change_on_a_stale_version_is_a_409()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var eventId = created.GetProperty("eventId").GetGuid();
        var stale = created.GetProperty("rowVersion").GetString();
        await manager.PatchAsJsonAsync($"/api/events/{eventId}/pack-size", new { packSizeEstimated = 150, rowVersion = stale });

        var response = await manager.PatchAsJsonAsync($"/api/events/{eventId}/pack-size", new { packSizeEstimated = 999, rowVersion = stale });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_pack_size_request_with_no_size_or_a_negative_one_is_a_400()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var path = $"/api/events/{created.GetProperty("eventId").GetGuid()}/pack-size";
        var version = created.GetProperty("rowVersion").GetString();

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PatchAsJsonAsync(path, new { rowVersion = version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PatchAsJsonAsync(path, new { packSizeActual = -5, rowVersion = version })).StatusCode);
    }

    [Fact]
    public async Task Crew_cannot_change_a_pack_size()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, crew) = await _scenario.SignedInAsync(RoleNames.CasualCrew);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);

        var response = await crew.PatchAsJsonAsync($"/api/events/{created.GetProperty("eventId").GetGuid()}/pack-size",
            new { packSizeActual = 10, rowVersion = created.GetProperty("rowVersion").GetString() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- lookups for the event form and crew picker ----------------------------------------------

    [Fact]
    public async Task The_event_form_can_read_clients_and_divisions()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();

        var clients = await manager.GetFromJsonAsync<JsonElement>("/api/clients?pageSize=200");
        var divisions = await manager.GetFromJsonAsync<JsonElement>("/api/divisions");

        var first = clients.GetProperty("items").EnumerateArray().First();
        Assert.False(string.IsNullOrEmpty(first.GetProperty("name").GetString()));
        Assert.Contains(clients.GetProperty("items").EnumerateArray(), c => c.GetProperty("clientId").GetGuid() == reference.ClientId);
        Assert.Contains(divisions.EnumerateArray(), d => d.GetProperty("divisionId").GetGuid() == reference.DivisionId);
        Assert.False(string.IsNullOrEmpty(divisions.EnumerateArray().First().GetProperty("code").GetString()));
    }

    [Theory]
    [InlineData(RoleNames.Accounts)]
    [InlineData(RoleNames.CasualCrew)]
    public async Task Clients_and_divisions_are_refused_to_roles_that_cannot_write_events(string role)
    {
        var (_, client) = await _scenario.SignedInAsync(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/divisions")).StatusCode);
    }

    [Fact]
    public async Task An_event_manager_can_pick_crew_without_seeing_contact_details()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (crewUser, _) = await _scenario.SignedInAsync(RoleNames.CasualCrew);

        var response = await manager.GetAsync("/api/crew/candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidates = await response.Content.ReadFromJsonAsync<JsonElement>();
        var picked = candidates.EnumerateArray().Single(c => c.GetProperty("userId").GetGuid() == crewUser.UserId);
        Assert.False(string.IsNullOrEmpty(picked.GetProperty("fullName").GetString()));
        Assert.False(picked.TryGetProperty("email", out _));
        Assert.False(picked.TryGetProperty("employeeNumber", out _));
    }

    [Fact]
    public async Task Crew_cannot_list_crew_candidates()
    {
        var (_, crew) = await _scenario.SignedInAsync(RoleNames.CasualCrew);

        Assert.Equal(HttpStatusCode.Forbidden, (await crew.GetAsync("/api/crew/candidates")).StatusCode);
    }

    // ---- create ----------------------------------------------------------------------------------

    [Fact]
    public async Task An_event_manager_creates_an_event_with_its_milestone_chain_and_an_audit_entry()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();

        var response = await manager.PostAsJsonAsync("/api/events", Scenario.NewEventBody(reference));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = created.GetProperty("eventId").GetGuid();
        Assert.Equal($"/api/events/{eventId}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Enquired", created.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(created.GetProperty("rowVersion").GetString()));
        Assert.Equal(85000m, created.GetProperty("budgetAmount").GetDecimal());

        var milestones = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/milestones");
        var types = milestones.EnumerateArray().Select(m => Enum.Parse<MilestoneType>(m.GetProperty("milestoneType").GetString()!)).ToList();
        Assert.Equal(ChainOrder, types);

        var audited = await _scenario.WithDbAsync(db =>
            db.AuditEntries.AnyAsync(a => a.Action == "event.create" && a.EntityId == eventId.ToString()));
        Assert.True(audited);
    }

    [Fact]
    public async Task The_times_in_the_response_end_in_Z()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();

        var created = await CreateAsync(manager, reference);

        Assert.EndsWith("Z", created.GetProperty("startsAt").GetString());
        Assert.EndsWith("Z", created.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task Operations_can_create_an_event_but_not_set_its_budget()
    {
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var reference = await _scenario.ReferenceDataAsync();

        var withBudget = await ops.PostAsJsonAsync("/api/events", Scenario.NewEventBody(reference));
        var body = Scenario.NewEventBody(reference);
        body.Remove("budgetAmount");
        var withoutBudget = await ops.PostAsJsonAsync("/api/events", body);

        Assert.Equal(HttpStatusCode.Forbidden, withBudget.StatusCode);
        Assert.Equal(HttpStatusCode.Created, withoutBudget.StatusCode);
        Assert.DoesNotContain("\"budgetAmount\"", await withoutBudget.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(RoleNames.CasualCrew)]
    [InlineData(RoleNames.CrewLead)]
    [InlineData(RoleNames.Accounts)]
    public async Task Roles_without_event_create_get_403(string role)
    {
        var (_, client) = await _scenario.SignedInAsync(role);
        var reference = await _scenario.ReferenceDataAsync();

        var response = await client.PostAsJsonAsync("/api/events", Scenario.NewEventBody(reference));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Creating_without_signing_in_is_401()
    {
        var response = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/events", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_fields_are_reported_together_as_400()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var body = Scenario.NewEventBody(reference, code: "ab");
        body["endsAt"] = ((DateTime)body["startsAt"]!).AddHours(-1);
        body["packSizeEstimated"] = -5;

        var response = await manager.PostAsJsonAsync("/api/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("eventCode", out _));
        Assert.True(errors.TryGetProperty("endsAt", out _));
        Assert.True(errors.TryGetProperty("packSizeEstimated", out _));
    }

    [Theory]
    [InlineData("bad code")]
    [InlineData("bad_code_1")]
    [InlineData("<script>alert(1)</script>")]
    public async Task An_event_code_outside_the_allowlist_is_rejected(string code)
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();

        var response = await manager.PostAsJsonAsync("/api/events", Scenario.NewEventBody(reference, code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_duplicate_event_code_is_a_400_on_the_code_field()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var first = await CreateAsync(manager, reference);

        var response = await manager.PostAsJsonAsync("/api/events",
            Scenario.NewEventBody(reference, first.GetProperty("eventCode").GetString()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("eventCode", out _));
    }

    [Fact]
    public async Task An_unknown_client_and_a_missing_venue_are_400s_on_those_fields()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var body = Scenario.NewEventBody(reference);
        body["clientId"] = Guid.NewGuid();
        body["venueId"] = null;

        var response = await manager.PostAsJsonAsync("/api/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("clientId", out _));
        Assert.True(errors.TryGetProperty("venueId", out _));
    }

    [Fact]
    public async Task A_rejected_create_adds_no_event_and_no_milestones()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var code = $"RB-{Guid.NewGuid():N}"[..14];
        await CreateAsync(manager, reference, body => body["eventCode"] = code);
        var events = await _scenario.WithDbAsync(db => db.Events.CountAsync());
        var milestones = await _scenario.WithDbAsync(db => db.EventMilestones.CountAsync());

        var duplicate = await manager.PostAsJsonAsync("/api/events", Scenario.NewEventBody(reference, code));

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(events, await _scenario.WithDbAsync(db => db.Events.CountAsync()));
        Assert.Equal(milestones, await _scenario.WithDbAsync(db => db.EventMilestones.CountAsync()));
    }

    // ---- read and visibility ---------------------------------------------------------------------

    [Fact]
    public async Task Operations_see_any_event_and_masked_fields_are_absent_from_the_raw_json()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();

        var forOps = await ops.GetAsync($"/api/events/{id}");
        var forManager = await manager.GetAsync($"/api/events/{id}");

        Assert.Equal(HttpStatusCode.OK, forOps.StatusCode);
        Assert.DoesNotContain("\"budgetAmount\"", await forOps.Content.ReadAsStringAsync());
        Assert.Contains("\"budgetAmount\"", await forManager.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Crew_see_only_events_they_are_assigned_to_and_everything_else_is_404()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var crew = await _scenario.UserAsync(RoleNames.CasualCrew);
        var crewClient = _scenario.ClientFor(crew, RoleNames.CasualCrew);
        var reference = await _scenario.ReferenceDataAsync();
        var mine = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();
        var other = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();
        await AssignAsync(manager, mine, crew.UserId);

        var assigned = await crewClient.GetAsync($"/api/events/{mine}");
        var notAssigned = await crewClient.GetAsync($"/api/events/{other}");
        var milestonesOfOther = await crewClient.GetAsync($"/api/events/{other}/milestones");
        var crewOfOther = await crewClient.GetAsync($"/api/events/{other}/crew");

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notAssigned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, milestonesOfOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crewOfOther.StatusCode);
    }

    [Fact]
    public async Task The_list_for_crew_holds_only_their_events_and_the_list_for_Operations_holds_all()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var crew = await _scenario.UserAsync(RoleNames.CrewLead);
        var crewClient = _scenario.ClientFor(crew, RoleNames.CrewLead);
        var reference = await _scenario.ReferenceDataAsync();
        var mine = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();
        var other = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();
        await AssignAsync(manager, mine, crew.UserId);

        var crewIds = await ListIdsAsync(crewClient, "/api/events?pageSize=200");
        var opsIds = await ListIdsAsync(ops, "/api/events?pageSize=200");

        Assert.Equal([mine], crewIds);
        Assert.Contains(mine, opsIds);
        Assert.Contains(other, opsIds);
    }

    [Fact]
    public async Task The_board_view_leaves_out_enquiries_and_the_list_pages_and_filters()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var enquiry = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();
        var confirmed = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();
        await _scenario.WithDbAsync(async db =>
        {
            var ev = await db.Events.FirstAsync(e => e.EventId == confirmed);
            ev.Status = EventStatus.ConfirmedInPlanning;
            await db.SaveChangesAsync();
        });

        var board = await ListIdsAsync(manager, $"/api/events?board=true&clientId={reference.ClientId}");
        var all = await ListIdsAsync(manager, $"/api/events?clientId={reference.ClientId}");
        var onlyConfirmed = await ListIdsAsync(manager, $"/api/events?status=ConfirmedInPlanning&clientId={reference.ClientId}");
        var page = await manager.GetFromJsonAsync<JsonElement>($"/api/events?clientId={reference.ClientId}&pageSize=1&page=2");

        Assert.Equal([confirmed], board);
        Assert.Contains(enquiry, all);
        Assert.Equal([confirmed], onlyConfirmed);
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(1, page.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task The_list_item_shape_matches_what_the_front_end_reads()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        await CreateAsync(manager, reference);

        var page = await manager.GetFromJsonAsync<JsonElement>($"/api/events?clientId={reference.ClientId}");
        var item = page.GetProperty("items")[0];

        foreach (var key in new[]
                 {
                     "eventId", "eventCode", "name", "status", "eventType", "divisionCode", "eventDate", "startsAt",
                     "endsAt", "venueName", "packSizeEstimated", "isConfidential", "rowVersion",
                 })
        {
            Assert.True(item.TryGetProperty(key, out _), $"missing {key}");
        }

        Assert.Equal("CE", item.GetProperty("divisionCode").GetString());
        Assert.DoesNotContain("budgetAmount", item.GetRawText());
    }

    [Fact]
    public async Task An_unknown_event_is_404_and_a_bad_page_size_is_400()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);

        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/events/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/events?pageSize=500")).StatusCode);
    }

    // ---- update and concurrency ------------------------------------------------------------------

    [Fact]
    public async Task Updating_changes_the_event_and_moves_the_row_version_on()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();

        var body = UpdateBody(created, name: "Renamed event");
        var response = await manager.PutAsJsonAsync($"/api/events/{id}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed event", updated.GetProperty("name").GetString());
        Assert.NotEqual(created.GetProperty("rowVersion").GetString(), updated.GetProperty("rowVersion").GetString());
    }

    [Fact]
    public async Task A_stale_row_version_is_a_409_that_carries_the_current_event()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();
        await manager.PutAsJsonAsync($"/api/events/{id}", UpdateBody(created, name: "Changed first"));

        var stale = await manager.PutAsJsonAsync($"/api/events/{id}", UpdateBody(created, name: "Changed second"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("/problems/concurrency-conflict", problem.GetProperty("type").GetString());
        Assert.Equal("Changed first", problem.GetProperty("current").GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_current_event_in_a_409_is_masked_for_someone_who_cannot_see_the_budget()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();
        await manager.PutAsJsonAsync($"/api/events/{id}", UpdateBody(created, name: "Manager edit"));

        var stale = await ops.PutAsJsonAsync($"/api/events/{id}", UpdateBody(created, name: "Ops edit", budget: null));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.DoesNotContain("budgetAmount", await stale.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Someone_who_cannot_see_the_budget_neither_changes_nor_erases_it()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, ops) = await _scenario.SignedInAsync(RoleNames.OperationsManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();

        var opsEdit = await ops.PutAsJsonAsync($"/api/events/{id}", UpdateBody(created, name: "Ops rename", budget: null));
        var opsSetsBudget = await ops.PutAsJsonAsync($"/api/events/{id}", UpdateBody(created, name: "Ops rename", budget: 1m));

        Assert.Equal(HttpStatusCode.OK, opsEdit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, opsSetsBudget.StatusCode);
        var afterwards = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{id}");
        Assert.Equal(85000m, afterwards.GetProperty("budgetAmount").GetDecimal());
    }

    [Fact]
    public async Task Update_without_permission_is_403_and_for_an_unknown_event_is_404()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, crew) = await _scenario.SignedInAsync(RoleNames.CasualCrew);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await crew.PutAsJsonAsync($"/api/events/{id}", UpdateBody(created))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PutAsJsonAsync($"/api/events/{Guid.NewGuid()}", UpdateBody(created))).StatusCode);
    }

    [Fact]
    public async Task Updating_without_a_row_version_is_a_400()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var body = UpdateBody(created);
        body["rowVersion"] = "";

        var response = await manager.PutAsJsonAsync($"/api/events/{created.GetProperty("eventId").GetGuid()}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- delete ---------------------------------------------------------------------------------

    [Fact]
    public async Task Only_the_Director_deletes_and_a_deleted_event_disappears_but_is_kept()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var reference = await _scenario.ReferenceDataAsync();
        var id = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"/api/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await director.DeleteAsync($"/api/events/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/events/{id}")).StatusCode);
        Assert.DoesNotContain(id, await ListIdsAsync(director, "/api/events?pageSize=200"));
        Assert.True(await _scenario.WithDbAsync(db => db.Events.AnyAsync(e => e.EventId == id && !e.IsActive)));
        Assert.True(await _scenario.WithDbAsync(db => db.AuditEntries.AnyAsync(a => a.Action == "event.delete" && a.EntityId == id.ToString())));
    }

    // ---- milestones and the cascade --------------------------------------------------------------

    [Fact]
    public async Task Rescheduling_Doors_moves_it_and_everything_after_it_and_moves_the_row_version_on()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();
        var doors = await MilestoneAsync(manager, id, MilestoneType.Doors);
        var loadInBefore = (await MilestoneAsync(manager, id, MilestoneType.LoadIn)).GetProperty("scheduledStart").GetDateTime();

        var response = await manager.PostAsJsonAsync(
            $"/api/events/{id}/milestones/{doors.GetProperty("milestoneId").GetGuid()}/reschedule",
            new
            {
                newStart = doors.GetProperty("scheduledStart").GetDateTime().AddHours(2),
                newEnd = doors.GetProperty("scheduledEnd").GetDateTime().AddHours(2),
                rowVersion = created.GetProperty("rowVersion").GetString(),
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var moved = result.GetProperty("milestones").EnumerateArray()
            .Select(m => Enum.Parse<MilestoneType>(m.GetProperty("milestoneType").GetString()!)).ToList();
        Assert.Equal(
            [MilestoneType.Doors, MilestoneType.Strike, MilestoneType.LoadOut, MilestoneType.Debrief, MilestoneType.Invoice, MilestoneType.Reconciliation],
            moved.OrderBy(m => (int)m).ToList());
        Assert.NotEqual(created.GetProperty("rowVersion").GetString(), result.GetProperty("rowVersion").GetString());

        var after = await MilestoneAsync(manager, id, MilestoneType.Doors);
        Assert.Equal(doors.GetProperty("scheduledStart").GetDateTime().AddHours(2), after.GetProperty("scheduledStart").GetDateTime());
        var loadInAfter = (await MilestoneAsync(manager, id, MilestoneType.LoadIn)).GetProperty("scheduledStart").GetDateTime();
        Assert.Equal(loadInBefore, loadInAfter);
    }

    [Fact]
    public async Task Rescheduling_with_a_stale_row_version_is_a_409()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();
        var doors = await MilestoneAsync(manager, id, MilestoneType.Doors);
        var url = $"/api/events/{id}/milestones/{doors.GetProperty("milestoneId").GetGuid()}/reschedule";
        var start = doors.GetProperty("scheduledStart").GetDateTime();
        var end = doors.GetProperty("scheduledEnd").GetDateTime();
        var first = await manager.PostAsJsonAsync(url, new { newStart = start.AddHours(1), newEnd = end.AddHours(1), rowVersion = created.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = await manager.PostAsJsonAsync(url, new { newStart = start.AddHours(3), newEnd = end.AddHours(3), rowVersion = created.GetProperty("rowVersion").GetString() });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("/problems/concurrency-conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_change_that_would_move_a_milestone_that_has_started_is_a_422()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();
        var doors = await MilestoneAsync(manager, id, MilestoneType.Doors);
        var strike = await MilestoneAsync(manager, id, MilestoneType.Strike);
        await _scenario.WithDbAsync(async db =>
        {
            var row = await db.EventMilestones.FirstAsync(m => m.MilestoneId == strike.GetProperty("milestoneId").GetGuid());
            row.ActualStart = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });

        var response = await manager.PostAsJsonAsync(
            $"/api/events/{id}/milestones/{doors.GetProperty("milestoneId").GetGuid()}/reschedule",
            new
            {
                newStart = doors.GetProperty("scheduledStart").GetDateTime().AddHours(2),
                newEnd = doors.GetProperty("scheduledEnd").GetDateTime().AddHours(2),
                rowVersion = created.GetProperty("rowVersion").GetString(),
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("/problems/business-rule", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
        var unchanged = await MilestoneAsync(manager, id, MilestoneType.Doors);
        Assert.Equal(doors.GetProperty("scheduledStart").GetDateTime(), unchanged.GetProperty("scheduledStart").GetDateTime());
    }

    [Fact]
    public async Task Rescheduling_an_unknown_milestone_is_404_a_backwards_window_is_400_and_crew_get_403()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, crew) = await _scenario.SignedInAsync(RoleNames.CasualCrew);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();
        var doors = await MilestoneAsync(manager, id, MilestoneType.Doors);
        var doorsId = doors.GetProperty("milestoneId").GetGuid();
        var start = doors.GetProperty("scheduledStart").GetDateTime();
        var rowVersion = created.GetProperty("rowVersion").GetString();

        var unknown = await manager.PostAsJsonAsync($"/api/events/{id}/milestones/{Guid.NewGuid()}/reschedule",
            new { newStart = start, newEnd = start.AddHours(1), rowVersion });
        var backwards = await manager.PostAsJsonAsync($"/api/events/{id}/milestones/{doorsId}/reschedule",
            new { newStart = start, newEnd = start.AddHours(-1), rowVersion });
        var forbidden = await crew.PostAsJsonAsync($"/api/events/{id}/milestones/{doorsId}/reschedule",
            new { newStart = start, newEnd = start.AddHours(1), rowVersion });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    // ---- crew -----------------------------------------------------------------------------------

    [Fact]
    public async Task An_event_manager_assigns_crew_but_cannot_set_an_hourly_rate()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var worker = await _scenario.UserAsync(RoleNames.CasualCrew);
        var reference = await _scenario.ReferenceDataAsync();
        var id = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();

        var plain = await AssignRawAsync(manager, id, worker.UserId, rate: null);
        var withRate = await AssignRawAsync(manager, id, worker.UserId, rate: 150m);

        Assert.Equal(HttpStatusCode.Created, plain.StatusCode);
        Assert.DoesNotContain("hourlyRate", await plain.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, withRate.StatusCode);
    }

    [Fact]
    public async Task Director_sees_every_rate_and_each_crew_member_sees_only_their_own()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var a = await _scenario.UserAsync(RoleNames.CasualCrew);
        var b = await _scenario.UserAsync(RoleNames.CasualCrew);
        var reference = await _scenario.ReferenceDataAsync();
        var id = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await AssignRawAsync(director, id, a.UserId, 150m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AssignRawAsync(director, id, b.UserId, 200m)).StatusCode);

        var asDirector = await director.GetFromJsonAsync<JsonElement>($"/api/events/{id}/crew");
        var asManager = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{id}/crew");
        var asA = await _scenario.ClientFor(a, RoleNames.CasualCrew).GetFromJsonAsync<JsonElement>($"/api/events/{id}/crew");

        Assert.All(asDirector.EnumerateArray(), c => Assert.True(c.TryGetProperty("hourlyRate", out _)));
        Assert.All(asManager.EnumerateArray(), c => Assert.False(c.TryGetProperty("hourlyRate", out _)));
        var ownRate = asA.EnumerateArray().Single(c => c.GetProperty("userId").GetGuid() == a.UserId);
        var othersRate = asA.EnumerateArray().Single(c => c.GetProperty("userId").GetGuid() == b.UserId);
        Assert.Equal(150m, ownRate.GetProperty("hourlyRate").GetDecimal());
        Assert.False(othersRate.TryGetProperty("hourlyRate", out _));
    }

    [Fact]
    public async Task Assigning_an_unknown_person_is_a_400_and_removing_works_once()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var worker = await _scenario.UserAsync(RoleNames.CasualCrew);
        var reference = await _scenario.ReferenceDataAsync();
        var id = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();

        var unknown = await AssignRawAsync(manager, id, Guid.NewGuid(), rate: null);
        var assigned = await (await AssignRawAsync(manager, id, worker.UserId, rate: null)).Content.ReadFromJsonAsync<JsonElement>();
        var assignmentId = assigned.GetProperty("assignmentId").GetGuid();

        var first = await manager.DeleteAsync($"/api/events/{id}/crew/{assignmentId}");
        var second = await manager.DeleteAsync($"/api/events/{id}/crew/{assignmentId}");

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task Crew_cannot_assign_or_remove_crew()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var worker = await _scenario.UserAsync(RoleNames.CrewLead);
        var workerClient = _scenario.ClientFor(worker, RoleNames.CrewLead);
        var reference = await _scenario.ReferenceDataAsync();
        var id = (await CreateAsync(manager, reference)).GetProperty("eventId").GetGuid();
        await AssignAsync(manager, id, worker.UserId);

        Assert.Equal(HttpStatusCode.Forbidden, (await AssignRawAsync(workerClient, id, worker.UserId, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await workerClient.DeleteAsync($"/api/events/{id}/crew/{Guid.NewGuid()}")).StatusCode);
    }

    // ---- stage moves -----------------------------------------------------------------------------

    [Fact]
    public async Task An_invalid_stage_move_is_a_409_and_a_valid_cancellation_works()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();

        var invalid = await manager.PostAsJsonAsync($"/api/events/{id}/transitions",
            new { to = "Finished", rowVersion = created.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        Assert.Equal("/problems/invalid-transition", (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());

        await _scenario.WithDbAsync(async db =>
        {
            var ev = await db.Events.FirstAsync(e => e.EventId == id);
            ev.Status = EventStatus.ConfirmedInPlanning;
            await db.SaveChangesAsync();
        });
        var current = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{id}");

        var allowed = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{id}/allowed-transitions");
        var cancel = await manager.PostAsJsonAsync($"/api/events/{id}/transitions",
            new { to = "Cancelled", rowVersion = current.GetProperty("rowVersion").GetString() });

        Assert.Contains("Cancelled", allowed.GetProperty("allowed").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Moving_to_Enquired_is_a_400_crew_get_403_and_an_invisible_event_is_404()
    {
        var (_, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var (_, crew) = await _scenario.SignedInAsync(RoleNames.CasualCrew);
        var reference = await _scenario.ReferenceDataAsync();
        var created = await CreateAsync(manager, reference);
        var id = created.GetProperty("eventId").GetGuid();
        var rowVersion = created.GetProperty("rowVersion").GetString();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.PostAsJsonAsync($"/api/events/{id}/transitions", new { to = "Enquired", rowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await crew.PostAsJsonAsync($"/api/events/{id}/transitions", new { to = "Cancelled", rowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await crew.GetAsync($"/api/events/{id}/allowed-transitions")).StatusCode);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static async Task<JsonElement> CreateAsync(
        HttpClient client, Scenario.ReferenceData reference, Action<Dictionary<string, object?>>? edit = null)
    {
        var body = Scenario.NewEventBody(reference);
        edit?.Invoke(body);
        var response = await client.PostAsJsonAsync("/api/events", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Dictionary<string, object?> UpdateBody(JsonElement created, string? name = null, decimal? budget = 85000m)
    {
        var body = new Dictionary<string, object?>
        {
            ["eventCode"] = created.GetProperty("eventCode").GetString(),
            ["clientId"] = created.GetProperty("clientId").GetGuid(),
            ["venueId"] = created.GetProperty("venueId").GetGuid(),
            ["divisionId"] = created.GetProperty("divisionId").GetGuid(),
            ["name"] = name ?? created.GetProperty("name").GetString(),
            ["eventType"] = created.GetProperty("eventType").GetString(),
            ["eventDate"] = created.GetProperty("eventDate").GetString(),
            ["startsAt"] = created.GetProperty("startsAt").GetDateTime(),
            ["endsAt"] = created.GetProperty("endsAt").GetDateTime(),
            ["packSizeEstimated"] = created.GetProperty("packSizeEstimated").GetInt32(),
            ["paymentMode"] = created.GetProperty("paymentMode").GetString(),
            ["infrastructureMode"] = created.GetProperty("infrastructureMode").GetString(),
            ["staffRequired"] = created.GetProperty("staffRequired").GetInt32(),
            ["isConfidential"] = created.GetProperty("isConfidential").GetBoolean(),
            ["rowVersion"] = created.GetProperty("rowVersion").GetString(),
        };
        if (budget is not null)
        {
            body["budgetAmount"] = budget;
        }

        return body;
    }

    private static async Task<JsonElement> MilestoneAsync(HttpClient client, Guid eventId, MilestoneType type)
    {
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/milestones");
        return list.EnumerateArray().Single(m => m.GetProperty("milestoneType").GetString() == type.ToString());
    }

    private static async Task<HttpResponseMessage> AssignRawAsync(HttpClient client, Guid eventId, Guid userId, decimal? rate)
    {
        var start = DateTime.UtcNow.AddDays(30);
        var body = new Dictionary<string, object?>
        {
            ["userId"] = userId,
            ["crewRole"] = "Bartender",
            ["shiftStart"] = start,
            ["shiftEnd"] = start.AddHours(8),
        };
        if (rate is not null)
        {
            body["hourlyRate"] = rate;
        }

        return await client.PostAsJsonAsync($"/api/events/{eventId}/crew", body);
    }

    private static async Task AssignAsync(HttpClient client, Guid eventId, Guid userId) =>
        Assert.Equal(HttpStatusCode.Created, (await AssignRawAsync(client, eventId, userId, rate: null)).StatusCode);

    private static async Task<List<Guid>> ListIdsAsync(HttpClient client, string url)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(url);
        return [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("eventId").GetGuid())];
    }
}
