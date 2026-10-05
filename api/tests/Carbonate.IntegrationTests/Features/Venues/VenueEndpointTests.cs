using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Venues;

//FR-32: venues persist between events and are reused. crew only reach one through an event they're on (US-23)
[Collection(DatabaseApiCollection.Name)]
public class VenueEndpointTests(DatabaseApiFactory factory)
{
    private static object NewVenue(string name) => new
    {
        name,
        address = "Steenberg Estate, Tokai Road, Cape Town",
        accessRoute = "Gate 2 off Tokai Road, follow the service road past the cellar",
        loadingBayDetails = "One bay, 3.5 m clearance",
        operatingHoursStart = "07:00:00",
        operatingHoursEnd = "23:30:00",
        requiresSecurityClearance = true,
        requiresHealthSafetyFile = true,
        ppeRequirements = "Closed shoes in the kitchen",
    };

    //----------------------------------------------------------\\
    //                              CREATE AND UPDATE
    //----------------------------------------------------------\\

    [Fact]
    public async Task Ops_creates_a_venue_and_reads_it_back()
    {
        await using var db = factory.CreateContext();
        var ops = await new TestData(db).UserAsync("Ops Manager");
        var client = factory.ClientFor(ops, RoleNames.OperationsManager);
        var name = $"Steenberg {TestData.Suffix()}";

        var created = await client.PostAsJsonAsync("/api/venues", NewVenue(name));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var venue = await created.Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("venueId").GetGuid();
        created.Headers.Location!.AbsolutePath.ShouldBe($"/api/venues/{venueId}");
        venue.GetProperty("name").GetString().ShouldBe(name);
        venue.GetProperty("operatingHoursEnd").GetString().ShouldBe("23:30:00");
        venue.GetProperty("isActive").GetBoolean().ShouldBeTrue();

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/venues/{venueId}");
        read.GetProperty("accessRoute").GetString().ShouldStartWith("Gate 2");

        var audit = await db.AuditEntries.SingleAsync(a => a.EntityId == venueId.ToString());
        audit.Action.ShouldBe("venue.created");
        audit.UserId.ShouldBe(ops.UserId);
    }

    [Fact]
    public async Task Deactivating_records_the_change_and_hides_the_venue_from_the_list()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var venue = await data.VenueAsync($"Old Mill {TestData.Suffix()}");
        var client = factory.ClientFor(ops, RoleNames.OperationsManager);

        var update = await client.PutAsJsonAsync($"/api/venues/{venue.VenueId}", new
        {
            name = venue.Name,
            address = venue.Address,
            isActive = false,
        });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var search = Uri.EscapeDataString(venue.Name);
        var active = await client.GetFromJsonAsync<JsonElement>($"/api/venues?q={search}");
        active.GetProperty("total").GetInt32().ShouldBe(0);
        var all = await client.GetFromJsonAsync<JsonElement>($"/api/venues?q={search}&includeInactive=true");
        all.GetProperty("total").GetInt32().ShouldBe(1);

        var audit = await db.AuditEntries.SingleAsync(a => a.EntityId == venue.VenueId.ToString());
        audit.Action.ShouldBe("venue.updated");
        audit.BeforeJson.ShouldNotBeNull().ShouldContain("\"IsActive\":true");
        audit.AfterJson.ShouldNotBeNull().ShouldContain("\"IsActive\":false");
    }

    [Fact]
    public async Task The_list_searches_name_and_address_and_pages()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var em = await data.UserAsync();
        var tag = TestData.Suffix();
        await data.VenueAsync($"{tag} Alpha");
        await data.VenueAsync($"{tag} Bravo");
        await data.VenueAsync("Somewhere else", address: $"{tag} Harbour Road");
        var client = factory.ClientFor(em, RoleNames.EventManager);

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/venues?q={tag}&pageSize=2");
        var second = await client.GetFromJsonAsync<JsonElement>($"/api/venues?q={tag}&pageSize=2&page=2");

        first.GetProperty("total").GetInt32().ShouldBe(3);
        first.GetProperty("items").GetArrayLength().ShouldBe(2);
        first.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe($"{tag} Alpha");
        second.GetProperty("items").GetArrayLength().ShouldBe(1);
        second.GetProperty("page").GetInt32().ShouldBe(2);
    }

    //----------------------------------------------------------\\
    //                              WHO CAN DO WHAT
    //----------------------------------------------------------\\

    [Fact]
    public async Task Roles_without_venue_edit_cannot_create_or_change_venues()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var venue = await data.VenueAsync();
        var accounts = factory.ClientFor(await data.UserAsync(), RoleNames.Accounts);
        var crewLead = factory.ClientFor(await data.UserAsync(), RoleNames.CrewLead);

        var create = await accounts.PostAsJsonAsync("/api/venues", NewVenue("Not allowed"));
        var update = await crewLead.PutAsJsonAsync($"/api/venues/{venue.VenueId}", NewVenue("Not allowed"));

        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        update.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await db.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venue.VenueId)).Name.ShouldBe(venue.Name);
    }

    [Fact]
    public async Task Crew_cannot_list_venues()
    {
        await using var db = factory.CreateContext();
        var crew = factory.ClientFor(await new TestData(db).UserAsync(), RoleNames.CasualCrew);

        var response = await crew.GetAsync("/api/venues");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Crew_can_read_a_venue_only_through_an_event_they_are_on()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var manager = await data.UserAsync();
        var crewUser = await data.UserAsync("Priya R.");

        var theirs = await data.VenueAsync();
        await data.AssignAsync((await data.EventAsync(manager.UserId, theirs.VenueId)).EventId, crewUser.UserId);

        var unrelated = await data.VenueAsync();

        var deletedEventVenue = await data.VenueAsync();
        var deleted = await data.EventAsync(manager.UserId, deletedEventVenue.VenueId, active: false);
        await data.AssignAsync(deleted.EventId, crewUser.UserId);

        var crew = factory.ClientFor(crewUser, RoleNames.CasualCrew);

        (await crew.GetAsync($"/api/venues/{theirs.VenueId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        //a 404 rather than a 403, so crew can't tell which venues exist
        (await crew.GetAsync($"/api/venues/{unrelated.VenueId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await crew.GetAsync($"/api/venues/{deletedEventVenue.VenueId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_venue_is_404()
    {
        await using var db = factory.CreateContext();
        var director = factory.ClientFor(await new TestData(db).UserAsync(), RoleNames.Director);

        (await director.GetAsync($"/api/venues/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await director.PutAsJsonAsync($"/api/venues/{Guid.NewGuid()}", NewVenue("Nowhere")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    //----------------------------------------------------------\\
    //                              VALIDATION
    //----------------------------------------------------------\\

    [Fact]
    public async Task Bad_venue_details_come_back_as_field_errors()
    {
        await using var db = factory.CreateContext();
        var ops = factory.ClientFor(await new TestData(db).UserAsync(), RoleNames.OperationsManager);

        var response = await ops.PostAsJsonAsync("/api/venues", new
        {
            name = "",
            address = "Somewhere",
            accessRoute = new string('a', 2001),
            operatingHoursStart = "08:00:00",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("name", out _).ShouldBeTrue();
        errors.TryGetProperty("accessRoute", out _).ShouldBeTrue();
        errors.TryGetProperty("operatingHoursEnd", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task A_page_size_over_the_cap_is_400()
    {
        await using var db = factory.CreateContext();
        var em = factory.ClientFor(await new TestData(db).UserAsync(), RoleNames.EventManager);

        var response = await em.GetAsync("/api/venues?pageSize=500");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
