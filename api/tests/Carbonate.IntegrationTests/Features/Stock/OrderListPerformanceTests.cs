using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Stock;
using Carbonate.IntegrationTests.Support;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Stock;

/// <summary>
/// NFR-08: generating the order lists for a seeded December must complete in under five seconds.
/// </summary>
/// <remarks>
/// <para>
/// December is Carbon's heaviest month. The number below is deliberately larger than a real one —
/// twelve events, forty stock items, around four hundred requirement lines — so passing here means
/// comfortable headroom rather than scraping past.
/// </para>
/// <para>
/// This exists to catch an N+1: the obvious implementation queries per event or per supplier and
/// degrades quietly as the data grows. A correctness test would not notice. The threshold is generous
/// because CI runners vary; what it really guards is the shape of the query, not the exact timing.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class OrderListPerformanceTests(DatabaseApiFixture fixture)
{
    private const int Events = 12;
    private const int ItemsPerSupplier = 20;
    private const int Suppliers = 2;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private Scenario Scenario => new(fixture.Factory);

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Generating_a_whole_december_stays_under_five_seconds()
    {
        var (user, client) = await Scenario.SignedInAsync(RoleNames.EventManager);
        var (from, to) = await GivenASeededDecemberAsync(user.UserId);

        // Warm the pipeline first: the first request pays for JIT and the EF model, which is not what
        // NFR-08 is about.
        (await client.PostAsJsonAsync("/api/order-lists/generate", new
        {
            from = from.AddYears(-5).ToString("yyyy-MM-dd"),
            to = from.AddYears(-5).AddDays(1).ToString("yyyy-MM-dd"),
        })).EnsureSuccessStatusCode();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/api/order-lists/generate", new
        {
            from = from.ToString("yyyy-MM-dd"),
            to = to.ToString("yyyy-MM-dd"),
        });
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        stopwatch.Elapsed.ShouldBeLessThan(
            Budget,
            $"NFR-08 allows {Budget.TotalSeconds} s for a seeded December; this took {stopwatch.Elapsed.TotalSeconds:0.00} s.");
    }

    /// <summary>
    /// Twelve confirmed events across December, each needing every catalogue item from both suppliers.
    /// </summary>
    private async Task<(DateOnly From, DateOnly To)> GivenASeededDecemberAsync(Guid createdByUserId)
    {
        // A December far enough ahead that the transition worker has no interest in these events.
        var december = new DateOnly(DateTime.UtcNow.Year + 2, 12, 1);

        await Scenario.WithDbAsync(async db =>
        {
            var data = new TestData(db);
            var category = new StockCategory { Name = $"Perf {TestData.Suffix()}" };
            db.Add(category);

            var items = new List<StockItem>();
            for (var s = 0; s < Suppliers; s++)
            {
                var supplier = new Supplier
                {
                    Name = $"Perf supplier {TestData.Suffix()}",
                    LeadTimeDays = 3 + s,
                };
                db.Add(supplier);

                for (var i = 0; i < ItemsPerSupplier; i++)
                {
                    var item = new StockItem
                    {
                        CategoryId = category.CategoryId,
                        DefaultSupplierId = supplier.SupplierId,
                        Sku = $"PERF-{TestData.Suffix()}",
                        Name = $"Perf item {s}-{i}",
                        Unit = "units",
                        IsConsumable = true,
                        ConsumptionPerHundredGuests = 10m,
                        StandardUnitCost = 15.00m,
                    };
                    items.Add(item);
                    db.Add(item);
                }
            }

            await db.SaveChangesAsync();

            for (var e = 0; e < Events; e++)
            {
                var ev = await data.EventAsync(createdByUserId);
                var requiredBy = december.AddDays(e * 2);

                foreach (var item in items)
                {
                    db.Add(new EventStockRequirement
                    {
                        EventId = ev.EventId,
                        StockItemId = item.StockItemId,
                        QuantityRequired = 25m,
                        RequiredByDate = requiredBy,
                        // Order and Rent are the two that reach a supplier; alternating exercises both.
                        SourceMode = e % 2 == 0 ? SourceMode.Order : SourceMode.Rent,
                    });
                }
            }

            await db.SaveChangesAsync();
        });

        return (december, december.AddDays(31));
    }
}
