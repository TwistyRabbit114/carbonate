using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Venues;

//FR-33: the recce, with vehicle, plate, driver, crew names and the health and safety file
[Collection(DatabaseApiCollection.Name)]
public class SiteVisitEndpointTests(DatabaseApiFactory factory)
{
    private static object FullRecce(Guid? conductedBy = null) => new
    {
        visitDate = "2026-11-02",
        conductedByUserId = conductedBy,
        vehicleType = "4-ton truck",
        licencePlate = "ca 123-456",
        driverName = "Sipho M.",
        requiredDriverDetails = "ID number at the gate",
        crewNames = "Thabo N., Priya R.",
        signInProcedure = "Sign in at the main gate, collect lanyards",
        securityCheckpoint = "Main gate",
        healthSafetyFileRef = "HS-2026-114",
        notes = "Power at the back wall only",
    };

    //----------------------------------------------------------\\
    //                              RECORDING
    //----------------------------------------------------------\\

    [Fact]
    public async Task A_recce_with_every_FR33_field_round_trips()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var sarah = await data.UserAsync("Sarah M.");
        var ev = await data.EventAsync(sarah.UserId);
        var client = factory.ClientFor(sarah, RoleNames.EventManager);

        var created = await client.PostAsJsonAsync($"/api/events/{ev.EventId}/site-visits", FullRecce());

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var visit = await created.Content.ReadFromJsonAsync<JsonElement>();
        visit.GetProperty("conductedBy").GetProperty("userId").GetGuid().ShouldBe(sarah.UserId);
        visit.GetProperty("conductedBy").GetProperty("fullName").GetString().ShouldBe("Sarah M.");
        visit.GetProperty("licencePlate").GetString().ShouldBe("CA 123-456");

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/events/{ev.EventId}/site-visits");
        var read = list[0];
        read.GetProperty("visitDate").GetString().ShouldBe("2026-11-02");
        read.GetProperty("vehicleType").GetString().ShouldBe("4-ton truck");
        read.GetProperty("driverName").GetString().ShouldBe("Sipho M.");
        read.GetProperty("requiredDriverDetails").GetString().ShouldBe("ID number at the gate");
        read.GetProperty("crewNames").GetString().ShouldBe("Thabo N., Priya R.");
        read.GetProperty("signInProcedure").GetString().ShouldStartWith("Sign in");
        read.GetProperty("securityCheckpoint").GetString().ShouldBe("Main gate");
        read.GetProperty("healthSafetyFileRef").GetString().ShouldBe("HS-2026-114");
        read.GetProperty("notes").GetString().ShouldBe("Power at the back wall only");

        var siteVisitId = visit.GetProperty("siteVisitId").GetGuid().ToString();
        (await db.AuditEntries.SingleAsync(a => a.EntityId == siteVisitId)).Action.ShouldBe("site_visit.created");
    }

    [Fact]
    public async Task Someone_else_can_be_named_as_having_done_the_recce()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var thabo = await data.UserAsync("Thabo N.");
        var ev = await data.EventAsync(ops.UserId);
        var client = factory.ClientFor(ops, RoleNames.OperationsManager);

        var created = await client.PostAsJsonAsync($"/api/events/{ev.EventId}/site-visits", FullRecce(thabo.UserId));

        var visit = await created.Content.ReadFromJsonAsync<JsonElement>();
        visit.GetProperty("conductedBy").GetProperty("fullName").GetString().ShouldBe("Thabo N.");
    }

    [Fact]
    public async Task Naming_someone_without_an_active_account_is_a_field_error()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var leaver = await data.UserAsync(active: false);
        var ev = await data.EventAsync(ops.UserId);
        var client = factory.ClientFor(ops, RoleNames.OperationsManager);

        foreach (var conductedBy in new[] { leaver.UserId, Guid.NewGuid() })
        {
            var response = await client.PostAsJsonAsync($"/api/events/{ev.EventId}/site-visits", FullRecce(conductedBy));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            errors.TryGetProperty("conductedByUserId", out _).ShouldBeTrue();
        }
    }

    //casual crew are deactivated a week after debrief, which mustn't lock their recces against edits
    [Fact]
    public async Task A_recce_stays_editable_after_whoever_did_it_is_deactivated()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var leaver = await data.UserAsync("Former Crew", active: false);
        var ev = await data.EventAsync(ops.UserId);
        var visit = await data.SiteVisitAsync(ev.EventId, leaver.UserId, notes: "Old notes");
        var client = factory.ClientFor(ops, RoleNames.OperationsManager);

        var response = await client.PutAsJsonAsync($"/api/site-visits/{visit.SiteVisitId}", new
        {
            visitDate = "2026-11-03",
            notes = "Corrected notes",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("conductedBy").GetProperty("fullName").GetString().ShouldBe("Former Crew");
        updated.GetProperty("notes").GetString().ShouldBe("Corrected notes");

        var audit = await db.AuditEntries.SingleAsync(a => a.EntityId == visit.SiteVisitId.ToString());
        audit.Action.ShouldBe("site_visit.updated");
        audit.BeforeJson.ShouldNotBeNull().ShouldContain("Old notes");
        audit.AfterJson.ShouldNotBeNull().ShouldContain("Corrected notes");
    }

    //----------------------------------------------------------\\
    //                              WHO CAN SEE AND RECORD
    //----------------------------------------------------------\\

    [Fact]
    public async Task Crew_on_the_event_can_read_its_recces_and_other_crew_get_404()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var manager = await data.UserAsync();
        var assigned = await data.UserAsync();
        var other = await data.UserAsync();
        var ev = await data.EventAsync(manager.UserId);
        await data.AssignAsync(ev.EventId, assigned.UserId);
        await data.SiteVisitAsync(ev.EventId, manager.UserId);

        var onEvent = await factory.ClientFor(assigned, RoleNames.CasualCrew)
            .GetAsync($"/api/events/{ev.EventId}/site-visits");
        var offEvent = await factory.ClientFor(other, RoleNames.CrewLead)
            .GetAsync($"/api/events/{ev.EventId}/site-visits");

        onEvent.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await onEvent.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().ShouldBe(1);
        offEvent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Accounts_can_read_recces_but_not_record_or_change_them()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var bookkeeper = await data.UserAsync();
        var ev = await data.EventAsync(bookkeeper.UserId);
        var visit = await data.SiteVisitAsync(ev.EventId, bookkeeper.UserId);
        var accounts = factory.ClientFor(bookkeeper, RoleNames.Accounts);

        (await accounts.GetAsync($"/api/events/{ev.EventId}/site-visits")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await accounts.PostAsJsonAsync($"/api/events/{ev.EventId}/site-visits", FullRecce()))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await accounts.PutAsJsonAsync($"/api/site-visits/{visit.SiteVisitId}", FullRecce()))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unknown_or_deleted_events_and_visits_are_404()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var director = await data.UserAsync();
        var deleted = await data.EventAsync(director.UserId, active: false);
        var client = factory.ClientFor(director, RoleNames.Director);

        (await client.PostAsJsonAsync($"/api/events/{Guid.NewGuid()}/site-visits", FullRecce()))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/events/{deleted.EventId}/site-visits")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/api/site-visits/{Guid.NewGuid()}", FullRecce()))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    //----------------------------------------------------------\\
    //                              VALIDATION
    //----------------------------------------------------------\\

    [Fact]
    public async Task A_missing_date_and_a_plate_with_symbols_come_back_as_field_errors()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var em = await data.UserAsync();
        var ev = await data.EventAsync(em.UserId);
        var client = factory.ClientFor(em, RoleNames.EventManager);

        var response = await client.PostAsJsonAsync($"/api/events/{ev.EventId}/site-visits", new
        {
            licencePlate = "<script>",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("visitDate", out _).ShouldBeTrue();
        errors.TryGetProperty("licencePlate", out _).ShouldBeTrue();
        (await db.SiteVisits.AnyAsync(v => v.EventId == ev.EventId)).ShouldBeFalse();
    }
}
