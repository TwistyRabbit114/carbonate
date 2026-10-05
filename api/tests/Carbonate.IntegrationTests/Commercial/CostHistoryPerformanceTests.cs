using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Commercial;
using Carbonate.Domain.Features.Events;
using Carbonate.IntegrationTests.Support;
using Xunit.Abstractions;

namespace Carbonate.IntegrationTests.Commercial;

/// <summary>
/// NFR-09: looking up a past costing, and comparing against earlier events, takes under 2 seconds on ten
/// years of history. Ten years at 60 events a year for one client is far more than the business holds.
/// It has a database of its own, because 600 extra events would crowd the paged event lists other tests read.
/// </summary>
public class CostHistoryPerformanceTests(DatabaseApiFixture fixture, ITestOutputHelper output) : IClassFixture<DatabaseApiFixture>
{
    private const int YearsOfHistory = 10;
    private const int EventsPerYear = 60;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    private readonly Scenario _scenario = new(fixture.Factory);

    [Fact]
    public async Task A_past_costing_and_the_cost_comparison_open_within_two_seconds_on_ten_years_of_history()
    {
        var (user, manager) = await _scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await _scenario.ReferenceDataAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var oldest = Guid.Empty;
        var newest = Guid.Empty;

        await _scenario.WithDbAsync(async db =>
        {
            for (var n = 0; n < YearsOfHistory * EventsPerYear; n++)
            {
                var date = today.AddDays(-(n * 365 / EventsPerYear) - 10);
                var ev = new Event
                {
                    EventCode = $"PERF-{Guid.NewGuid():N}"[..16],
                    ClientId = reference.ClientId,
                    VenueId = reference.VenueId,
                    DivisionId = reference.DivisionId,
                    CreatedByUserId = user.UserId,
                    Name = $"History {n}",
                    EventType = EventType.Corporate,
                    Status = EventStatus.Finished,
                    EventDate = date,
                    PackSizeEstimated = 100 + n,
                    StartsAt = date.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc),
                    EndsAt = date.ToDateTime(new TimeOnly(23, 0), DateTimeKind.Utc),
                    CreatedAt = DateTime.UtcNow,
                };
                db.Events.Add(ev);
                oldest = n == YearsOfHistory * EventsPerYear - 1 ? ev.EventId : oldest;
                newest = n == 0 ? ev.EventId : newest;

                var quote = new Quote
                {
                    EventId = ev.EventId,
                    Status = QuoteStatus.Accepted,
                    SubtotalExVat = 100000,
                    VatAmount = 15000,
                    TotalIncVat = 115000,
                    CreatedAt = DateTime.UtcNow,
                };
                for (var line = 0; line < 8; line++)
                {
                    quote.Lines.Add(new QuoteLine
                    {
                        QuoteId = quote.QuoteId,
                        Description = $"Line {line}",
                        Quantity = 10,
                        UnitCostToUs = 800,
                        UnitPriceToClient = 1250,
                        LineTotal = 12500,
                    });
                }

                db.Quotes.Add(quote);
            }

            await db.SaveChangesAsync();
        });

        // One request to warm the connection and the query plan, as any real session would have done.
        await manager.GetAsync($"/api/events/{newest}/cost-history");

        var history = Stopwatch.StartNew();
        var comparison = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{oldest}/cost-history");
        history.Stop();

        var costing = Stopwatch.StartNew();
        var quotes = await manager.GetFromJsonAsync<JsonElement>($"/api/events/{oldest}/quotes");
        costing.Stop();

        output.WriteLine($"cost comparison {history.ElapsedMilliseconds} ms, past costing {costing.ElapsedMilliseconds} ms");
        Assert.True(comparison.GetArrayLength() > 0);
        Assert.Equal(1, quotes.GetArrayLength());
        Assert.True(history.Elapsed < Budget, $"The cost comparison took {history.ElapsedMilliseconds} ms.");
        Assert.True(costing.Elapsed < Budget, $"The past costing took {costing.ElapsedMilliseconds} ms.");
    }
}
