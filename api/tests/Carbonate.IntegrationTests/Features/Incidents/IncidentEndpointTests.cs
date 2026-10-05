using System.Net;
using System.Net.Http.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Stock;
using Carbonate.IntegrationTests.Support;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Incidents;

/// <summary>
/// FR-31. Reporting is multipart because the form carries a photo; these tests send the form without
/// one, so nothing touches blob storage.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class IncidentEndpointTests(DatabaseApiFixture fixture)
{
    private Scenario Scenario => new(fixture.Factory);

    [Fact]
    public async Task An_event_manager_reports_an_incident()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventAsync(user.UserId);
        var asset = await GivenAssetAsync();

        var response = await client.PostAsync(
            $"/api/events/{ev}/incidents",
            Form(IncidentType.Breakage, assetId: asset, description: "Two racks of glassware went over."));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadAsStringAsync()).ShouldContain("went over");
    }

    [Fact]
    public async Task Accounts_cannot_report_an_incident()
    {
        // Accounts is the one role without incident.create.
        var (user, _) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventAsync(user.UserId);
        var asset = await GivenAssetAsync();

        var (_, accounts) = await Scenario.SignedInAsync(RoleNames.Accounts);
        var response = await accounts.PostAsync(
            $"/api/events/{ev}/incidents", Form(IncidentType.Breakage, assetId: asset));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Crew_cannot_report_against_an_event_they_are_not_on()
    {
        var (manager, _) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventAsync(manager.UserId);
        var asset = await GivenAssetAsync();

        var (_, crew) = await Scenario.SignedInAsync(RoleNames.CrewLead);
        var response = await crew.PostAsync(
            $"/api/events/{ev}/incidents", Form(IncidentType.Breakage, assetId: asset));

        // 404 rather than 403, so the endpoint never reveals that the event exists.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Crew_can_report_against_an_event_they_are_on()
    {
        var (manager, _) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventAsync(manager.UserId);
        var asset = await GivenAssetAsync();

        var (crewUser, crew) = await Scenario.SignedInAsync(RoleNames.CrewLead);
        await Scenario.WithDbAsync(async db => await new TestData(db).AssignAsync(ev, crewUser.UserId));

        var response = await crew.PostAsync(
            $"/api/events/{ev}/incidents",
            Form(IncidentType.EquipmentFailure, assetId: asset, description: "Ice machine stopped at doors."));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_incident_against_nothing_is_refused()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventAsync(user.UserId);

        // Neither an asset nor a stock item. The database has the same CHECK constraint.
        var response = await client.PostAsync(
            $"/api/events/{ev}/incidents", Form(IncidentType.StockShortfall, description: "Ran out."));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_incident_with_no_description_is_refused()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventAsync(user.UserId);
        var asset = await GivenAssetAsync();

        var response = await client.PostAsync(
            $"/api/events/{ev}/incidents", Form(IncidentType.Breakage, assetId: asset, description: ""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reporting_against_an_unknown_event_is_not_found()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var asset = await GivenAssetAsync();

        var response = await client.PostAsync(
            $"/api/events/{Guid.NewGuid()}/incidents", Form(IncidentType.Breakage, assetId: asset));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_events_incidents_can_be_listed()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventAsync(user.UserId);
        var asset = await GivenAssetAsync();

        (await client.PostAsync($"/api/events/{ev}/incidents",
            Form(IncidentType.Breakage, assetId: asset, description: "Six glasses."))).EnsureSuccessStatusCode();

        var body = await client.GetStringAsync($"/api/events/{ev}/incidents");

        body.ShouldContain("Six glasses.");
    }

    [Fact]
    public async Task Resolving_needs_stock_manage()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var incidentId = await GivenIncidentAsync(user.UserId, client);

        // The Event Manager who reported it cannot resolve it: resolution carries a $cost field.
        var response = await client.PatchAsJsonAsync(
            $"/api/incidents/{incidentId}", new { resolutionNotes = "Replaced." });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_director_resolves_an_incident_with_a_cost()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var incidentId = await GivenIncidentAsync(user.UserId, client);

        var (_, director) = await Scenario.SignedInAsync(RoleNames.Director);
        var response = await director.PatchAsJsonAsync(
            $"/api/incidents/{incidentId}", new { resolutionNotes = "Replaced.", replacementCost = 450.00m });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stored = await Scenario.WithDbAsync(async db => await db.IncidentReports.FindAsync(incidentId));
        stored!.ReplacementCost.ShouldBe(450.00m);
    }

    [Fact]
    public async Task Resolving_without_cost_access_does_not_wipe_the_cost()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var incidentId = await GivenIncidentAsync(user.UserId, client);

        var (_, director) = await Scenario.SignedInAsync(RoleNames.Director);
        (await director.PatchAsJsonAsync(
            $"/api/incidents/{incidentId}", new { replacementCost = 450.00m })).EnsureSuccessStatusCode();

        // The Operations Manager holds stock.manage but not finance.view_internal_cost, so they never
        // saw the figure and must not be able to destroy it by sending it back as null.
        var (_, ops) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        (await ops.PatchAsJsonAsync(
            $"/api/incidents/{incidentId}",
            new { resolutionNotes = "Logged with the supplier." })).EnsureSuccessStatusCode();

        var stored = await Scenario.WithDbAsync(async db => await db.IncidentReports.FindAsync(incidentId));
        stored!.ReplacementCost.ShouldBe(450.00m);
        stored.ResolutionNotes.ShouldBe("Logged with the supplier.");
    }

    [Fact]
    public async Task A_negative_replacement_cost_is_refused()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var incidentId = await GivenIncidentAsync(user.UserId, client);

        var (_, director) = await Scenario.SignedInAsync(RoleNames.Director);
        var response = await director.PatchAsJsonAsync(
            $"/api/incidents/{incidentId}", new { replacementCost = -1m });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resolving_an_unknown_incident_is_not_found()
    {
        var (_, director) = await Scenario.SignedInAsync(RoleNames.Director);

        var response = await director.PatchAsJsonAsync(
            $"/api/incidents/{Guid.NewGuid()}", new { resolutionNotes = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- arrangement -------------------------------------------------------------------------

    /// <summary>The multipart body the mobile form sends, without a photo.</summary>
    private static MultipartFormDataContent Form(
        IncidentType type, Guid? assetId = null, Guid? stockItemId = null, string description = "Something broke.")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(type.ToString()), "IncidentType" },
            { new StringContent(description), "Description" },
        };

        if (assetId is { } asset)
        {
            form.Add(new StringContent(asset.ToString()), "AssetId");
        }

        if (stockItemId is { } item)
        {
            form.Add(new StringContent(item.ToString()), "StockItemId");
        }

        return form;
    }

    private async Task<Guid> GivenEventAsync(Guid createdByUserId) =>
        await Scenario.WithDbAsync(async db => (await new TestData(db).EventAsync(createdByUserId)).EventId);

    private async Task<Guid> GivenAssetAsync() =>
        await Scenario.WithDbAsync(async db =>
        {
            var category = new StockCategory { Name = $"Category {TestData.Suffix()}" };
            var item = new StockItem
            {
                CategoryId = category.CategoryId,
                Sku = $"SKU-{TestData.Suffix()}",
                Name = "Ice machine",
                Unit = "units",
                IsAsset = true,
            };
            var asset = new EquipmentAsset
            {
                StockItemId = item.StockItemId,
                SerialNumber = $"SN-{TestData.Suffix()}",
                Condition = "Good",
                Status = "Available",
            };
            db.AddRange(category, item, asset);
            await db.SaveChangesAsync();
            return asset.AssetId;
        });

    private async Task<Guid> GivenIncidentAsync(Guid reporterUserId, HttpClient client)
    {
        var ev = await GivenEventAsync(reporterUserId);
        var asset = await GivenAssetAsync();

        var response = await client.PostAsync(
            $"/api/events/{ev}/incidents", Form(IncidentType.Breakage, assetId: asset));
        response.EnsureSuccessStatusCode();

        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("incidentId").GetGuid();
    }
}
