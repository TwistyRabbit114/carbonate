using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Stock;
using Carbonate.IntegrationTests.Support;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Stock;

/// <summary>
/// FR-24 and FR-26 through FR-30 against the real pipeline and a real database.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class StockEndpointTests(DatabaseApiFixture fixture)
{
    private Scenario Scenario => new(fixture.Factory);

    // ---- FR-24, the catalogue ---------------------------------------------------------------

    [Fact]
    public async Task Listing_items_needs_stock_view()
    {
        // Casual crew are the one role without stock.view.
        var (_, client) = await Scenario.SignedInAsync(RoleNames.CasualCrew);

        var response = await client.GetAsync("/api/stock/items");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_event_manager_sees_the_unit_cost()
    {
        await GivenItemAsync(cost: 12.50m);
        var (_, client) = await Scenario.SignedInAsync(RoleNames.EventManager);

        var body = await client.GetStringAsync("/api/stock/items");

        // Event Manager holds finance.view_internal_cost.
        body.ShouldContain("standardUnitCost");
    }

    [Fact]
    public async Task An_operations_manager_does_not_see_the_unit_cost()
    {
        await GivenItemAsync(cost: 12.50m);
        var (_, client) = await Scenario.SignedInAsync(RoleNames.OperationsManager);

        var body = await client.GetStringAsync("/api/stock/items");

        // Absent, not null and not zero: the masking rule is "omitted" (plan section 7.3).
        body.ShouldNotContain("standardUnitCost");
    }

    [Fact]
    public async Task Creating_an_item_needs_stock_manage()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var category = await GivenCategoryAsync();

        var response = await client.PostAsJsonAsync("/api/stock/items", NewItemBody(category));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_operations_manager_creates_an_item()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var category = await GivenCategoryAsync();

        var response = await client.PostAsJsonAsync("/api/stock/items", NewItemBody(category));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_duplicate_sku_is_refused()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.OperationsManager);
        var category = await GivenCategoryAsync();
        var body = NewItemBody(category);

        (await client.PostAsJsonAsync("/api/stock/items", body)).EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/stock/items", body);

        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).ShouldContain("sku");
    }

    [Fact]
    public async Task Editing_an_item_without_cost_access_does_not_wipe_the_cost()
    {
        // The trap this guards: an Operations Manager holds stock.manage but not
        // finance.view_internal_cost, so they read the item with the cost absent, send it back as
        // null, and would destroy a figure they were never shown.
        var item = await GivenItemAsync(cost: 99.99m);
        var (_, client) = await Scenario.SignedInAsync(RoleNames.OperationsManager);

        var body = NewItemBody(item.CategoryId);
        body["sku"] = item.Sku;
        body["name"] = "Renamed by someone who cannot see costs";
        body["standardUnitCost"] = null;

        var response = await client.PutAsJsonAsync($"/api/stock/items/{item.StockItemId}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stored = await Scenario.WithDbAsync(async db =>
            await db.StockItems.FindAsync(item.StockItemId));

        stored!.StandardUnitCost.ShouldBe(99.99m);
        stored.Name.ShouldBe("Renamed by someone who cannot see costs");
    }

    [Fact]
    public async Task A_director_can_change_the_cost()
    {
        var item = await GivenItemAsync(cost: 99.99m);
        var (_, client) = await Scenario.SignedInAsync(RoleNames.Director);

        var body = NewItemBody(item.CategoryId);
        body["sku"] = item.Sku;
        body["standardUnitCost"] = 150.00m;

        (await client.PutAsJsonAsync($"/api/stock/items/{item.StockItemId}", body))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var stored = await Scenario.WithDbAsync(async db => await db.StockItems.FindAsync(item.StockItemId));
        stored!.StandardUnitCost.ShouldBe(150.00m);
    }

    [Fact]
    public async Task Updating_an_item_that_does_not_exist_is_not_found()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.Director);
        var category = await GivenCategoryAsync();

        var response = await client.PutAsJsonAsync($"/api/stock/items/{Guid.NewGuid()}", NewItemBody(category));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- FR-26, FR-27, FR-29, the event plan -------------------------------------------------

    [Fact]
    public async Task A_plan_short_of_expected_consumption_carries_a_shortfall_warning()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventWithLoadInAsync(user.UserId, packSize: 600);

        // 45 per hundred guests at 600 guests expects 270. Plan 200.
        var item = await GivenItemAsync(cost: 10m, consumptionPerHundred: 45m);
        await GivenRequirementAsync(ev, item.StockItemId, quantity: 200m, SourceMode.Stock);

        var body = await client.GetStringAsync($"/api/events/{ev}/stock-requirements");

        body.ShouldContain("SHORTFALL");
        body.ShouldContain("\"expected\":270");
    }

    [Fact]
    public async Task A_plan_that_meets_expected_consumption_has_no_warning()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventWithLoadInAsync(user.UserId, packSize: 600);
        var item = await GivenItemAsync(cost: 10m, consumptionPerHundred: 45m);
        await GivenRequirementAsync(ev, item.StockItemId, quantity: 300m, SourceMode.Stock);

        var body = await client.GetStringAsync($"/api/events/{ev}/stock-requirements");

        body.ShouldNotContain("SHORTFALL");
    }

    [Fact]
    public async Task Requirements_for_an_unknown_event_are_not_found()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.EventManager);

        var response = await client.GetAsync($"/api/events/{Guid.NewGuid()}/stock-requirements");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_same_item_twice_in_one_plan_is_refused()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventWithLoadInAsync(user.UserId, packSize: 100);
        var item = await GivenItemAsync(cost: 10m);

        var line = new Dictionary<string, object?>
        {
            ["stockItemId"] = item.StockItemId,
            ["quantityRequired"] = 10m,
            ["requiredByDate"] = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)).ToString("yyyy-MM-dd"),
            ["sourceMode"] = "Stock",
        };

        var response = await client.PutAsJsonAsync(
            $"/api/events/{ev}/stock-requirements", new { items = new[] { line, line } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- FR-28 and FR-30, order lists ---------------------------------------------------------

    [Fact]
    public async Task Generating_groups_by_supplier_and_reports_items_with_none()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var ev = await GivenEventWithLoadInAsync(user.UserId, packSize: 100);

        var supplier = await GivenSupplierAsync(leadTimeDays: 2);
        var supplied = await GivenItemAsync(cost: 10m, supplierId: supplier);
        var unassigned = await GivenItemAsync(cost: 20m);

        var requiredBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await GivenRequirementAsync(ev, supplied.StockItemId, 10m, SourceMode.Order, requiredBy);
        await GivenRequirementAsync(ev, unassigned.StockItemId, 5m, SourceMode.Rent, requiredBy);

        var response = await client.PostAsJsonAsync("/api/order-lists/generate", new
        {
            from = requiredBy.AddDays(-5).ToString("yyyy-MM-dd"),
            to = requiredBy.AddDays(5).ToString("yyyy-MM-dd"),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // One list for the supplier that has one; the other is reported, never turned into a list.
        json.RootElement.GetProperty("orderLists").GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);
        json.RootElement.GetProperty("unassignedSupplier")
            .EnumerateArray()
            .Select(u => u.GetProperty("stockItemId").GetGuid())
            .ShouldContain(unassigned.StockItemId);
    }

    [Fact]
    public async Task Generating_needs_order_generate()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.CrewLead);

        var response = await client.PostAsJsonAsync("/api/order-lists/generate", new
        {
            from = "2026-12-01",
            to = "2026-12-31",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_backwards_period_is_refused()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.EventManager);

        var response = await client.PostAsJsonAsync("/api/order-lists/generate", new
        {
            from = "2026-12-31",
            to = "2026-12-01",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_person_who_generated_a_list_cannot_approve_it()
    {
        // Separation of duties, FR-30. The Director both generates and approves, which is exactly the
        // case the rule exists to stop.
        var (user, client) = await Scenario.SignedInAsync(RoleNames.Director);
        var list = await GivenSubmittedListAsync(user.UserId, client);

        var response = await client.PostAsJsonAsync(
            $"/api/order-lists/{list.Id}/approve", new { rowVersion = list.RowVersion });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Someone_else_with_approval_rights_can_approve_it()
    {
        var (generator, generatorClient) = await Scenario.SignedInAsync(RoleNames.Director);
        var list = await GivenSubmittedListAsync(generator.UserId, generatorClient);

        var (_, approverClient) = await Scenario.SignedInAsync(RoleNames.Accounts);
        var response = await approverClient.PostAsJsonAsync(
            $"/api/order-lists/{list.Id}/approve", new { rowVersion = list.RowVersion });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_list_cannot_skip_approval()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.Director);
        var list = await GivenDraftListAsync(user.UserId);

        // Draft straight to Placed: not on the map, so 409 rather than 422.
        var response = await client.PostAsJsonAsync(
            $"/api/order-lists/{list.Id}/mark-placed", new { rowVersion = list.RowVersion });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_unknown_order_list_is_not_found()
    {
        var (_, client) = await Scenario.SignedInAsync(RoleNames.Director);

        var response = await client.GetAsync($"/api/order-lists/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- arrangement -------------------------------------------------------------------------

    private static Dictionary<string, object?> NewItemBody(Guid categoryId) => new()
    {
        ["categoryId"] = categoryId,
        ["sku"] = $"SKU-{Guid.NewGuid():N}"[..16],
        ["name"] = "Test item",
        ["unit"] = "units",
        ["isConsumable"] = true,
        ["isAsset"] = false,
        ["isActive"] = true,
        ["standardUnitCost"] = 25.00m,
    };

    private async Task<Guid> GivenCategoryAsync() =>
        await Scenario.WithDbAsync(async db =>
        {
            var category = new StockCategory { Name = $"Category {TestData.Suffix()}" };
            db.Add(category);
            await db.SaveChangesAsync();
            return category.CategoryId;
        });

    private async Task<StockItem> GivenItemAsync(
        decimal cost, decimal? consumptionPerHundred = null, Guid? supplierId = null) =>
        await Scenario.WithDbAsync(async db =>
        {
            var category = new StockCategory { Name = $"Category {TestData.Suffix()}" };
            var item = new StockItem
            {
                CategoryId = category.CategoryId,
                DefaultSupplierId = supplierId,
                Sku = $"SKU-{TestData.Suffix()}",
                Name = $"Item {TestData.Suffix()}",
                Unit = "units",
                IsConsumable = true,
                ConsumptionPerHundredGuests = consumptionPerHundred,
                StandardUnitCost = cost,
            };
            db.AddRange(category, item);
            await db.SaveChangesAsync();
            return item;
        });

    private async Task<Guid> GivenSupplierAsync(int leadTimeDays) =>
        await Scenario.WithDbAsync(async db =>
        {
            var supplier = new Supplier { Name = $"Supplier {TestData.Suffix()}", LeadTimeDays = leadTimeDays };
            db.Add(supplier);
            await db.SaveChangesAsync();
            return supplier.SupplierId;
        });

    /// <summary>A confirmed event a month out, with a LoadIn so the shortfall rule has a deadline.</summary>
    private async Task<Guid> GivenEventWithLoadInAsync(Guid createdByUserId, int packSize) =>
        await Scenario.WithDbAsync(async db =>
        {
            var data = new TestData(db);
            var ev = await data.EventAsync(createdByUserId);
            await data.MilestoneAsync(ev.EventId);

            ev.PackSizeEstimated = packSize;
            await db.SaveChangesAsync();
            return ev.EventId;
        });

    private async Task GivenRequirementAsync(
        Guid eventId, Guid stockItemId, decimal quantity, SourceMode mode, DateOnly? requiredBy = null) =>
        await Scenario.WithDbAsync(async db =>
        {
            db.Add(new EventStockRequirement
            {
                EventId = eventId,
                StockItemId = stockItemId,
                QuantityRequired = quantity,
                RequiredByDate = requiredBy ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                SourceMode = mode,
            });
            await db.SaveChangesAsync();
        });

    private sealed record ListRef(Guid Id, string RowVersion);

    private async Task<ListRef> GivenDraftListAsync(Guid generatedByUserId) =>
        await Scenario.WithDbAsync(async db =>
        {
            var supplier = new Supplier { Name = $"Supplier {TestData.Suffix()}", LeadTimeDays = 3 };
            var list = new OrderList
            {
                SupplierId = supplier.SupplierId,
                GeneratedByUserId = generatedByUserId,
                Status = OrderListStatus.Draft,
                RequiredByDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                GeneratedAt = DateTime.UtcNow,
                PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow),
                PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            };
            db.AddRange(supplier, list);
            await db.SaveChangesAsync();
            return new ListRef(list.OrderListId, Convert.ToBase64String(list.RowVersion));
        });

    /// <summary>A list already moved to PendingApproval, ready for the approval tests.</summary>
    private async Task<ListRef> GivenSubmittedListAsync(Guid generatedByUserId, HttpClient client)
    {
        var draft = await GivenDraftListAsync(generatedByUserId);

        var submit = await client.PostAsJsonAsync(
            $"/api/order-lists/{draft.Id}/submit", new { rowVersion = draft.RowVersion });
        submit.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
        return new ListRef(draft.Id, json.RootElement.GetProperty("rowVersion").GetString()!);
    }
}
