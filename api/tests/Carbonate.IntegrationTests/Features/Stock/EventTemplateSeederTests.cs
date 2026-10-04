using System.Text.Json;
using System.Text.Json.Serialization;
using Carbonate.Application.Features.Stock;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Stock;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Features.Stock;
using Carbonate.Infrastructure.Persistence;
using Carbonate.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Stock;

/// <summary>
/// FR-25 end to end against a real SQL Server, because the rules that matter here are relational:
/// the board/event CHECK constraint, the unique (BoardId, Position) index and the card FK to Users.
/// </summary>
public class EventTemplateSeederTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly DateTime LoadIn = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Doors = new(2026, 11, 14, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Seeds_the_board_its_cards_and_the_stock_requirements()
    {
        await using var db = fixture.CreateContext();
        var world = await ArrangeAsync(db, EventType.Corporate, packSize: 180);

        var result = await Seed(db).SeedAsync(
            world.EventId, EventType.Corporate, world.DivisionId, packSize: 180, world.UserId);
        await db.SaveChangesAsync();

        result.TemplateId.ShouldBe(world.TemplateId);
        result.ColumnsCreated.ShouldBe(3);
        result.CardsCreated.ShouldBe(2);
        result.StockRequirementsCreated.ShouldBe(1);
        result.UnknownSkus.ShouldBeEmpty();

        var board = await db.Boards
            .Include(b => b.Columns).ThenInclude(c => c.Cards)
            .SingleAsync(b => b.EventId == world.EventId);

        board.BoardType.ShouldBe(BoardType.Event);
        board.TemplateId.ShouldBe(world.TemplateId);
        board.Columns.Select(c => c.Position).ShouldBe([0, 1, 2]);
        board.Columns.Single(c => c.Name == "Complete").IsDoneColumn.ShouldBeTrue();
    }

    [Fact]
    public async Task Card_due_dates_are_the_milestone_time_plus_the_offset()
    {
        await using var db = fixture.CreateContext();
        var world = await ArrangeAsync(db, EventType.Corporate, packSize: 180);

        await Seed(db).SeedAsync(world.EventId, EventType.Corporate, world.DivisionId, 180, world.UserId);
        await db.SaveChangesAsync();

        var cards = await db.TaskCards
            .Where(c => db.BoardColumns
                .Where(col => db.Boards.Any(b => b.BoardId == col.BoardId && b.EventId == world.EventId))
                .Select(col => col.ColumnId)
                .Contains(c.ColumnId))
            .ToListAsync();

        // -48 hours from a 14 Nov 06:00 load-in.
        cards.Single(c => c.Subject == "Pull stock").DueAt.ShouldBe(LoadIn.AddHours(-48));
        // -2 hours from an 18:00 doors.
        cards.Single(c => c.Subject == "Bar set-up").DueAt.ShouldBe(Doors.AddHours(-2));
    }

    [Fact]
    public async Task Stock_requirement_quantity_is_rounded_up_from_the_pack_size()
    {
        await using var db = fixture.CreateContext();
        var world = await ArrangeAsync(db, EventType.Corporate, packSize: 90);

        await Seed(db).SeedAsync(world.EventId, EventType.Corporate, world.DivisionId, 90, world.UserId);
        await db.SaveChangesAsync();

        var requirement = await db.EventStockRequirements.SingleAsync(r => r.EventId == world.EventId);

        // 45 per hundred guests x 90 guests = 40.5, which rounds up.
        requirement.QuantityRequired.ShouldBe(41m);
        requirement.SourceMode.ShouldBe(SourceMode.Order);
        requirement.RequiredByDate.ShouldBe(DateOnly.FromDateTime(LoadIn));
        requirement.QuantityAllocated.ShouldBe(0m);
    }

    [Fact]
    public async Task Does_nothing_when_the_division_has_no_template()
    {
        await using var db = fixture.CreateContext();
        var world = await ArrangeAsync(db, EventType.Corporate, packSize: 180, withTemplate: false);

        var result = await Seed(db).SeedAsync(
            world.EventId, EventType.Corporate, world.DivisionId, 180, world.UserId);
        await db.SaveChangesAsync();

        // An event with no template is still a valid event — it just starts with an empty board.
        result.TemplateId.ShouldBeNull();
        result.CardsCreated.ShouldBe(0);
        (await db.Boards.AnyAsync(b => b.EventId == world.EventId)).ShouldBeFalse();
        (await db.EventStockRequirements.AnyAsync(r => r.EventId == world.EventId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Reports_a_sku_the_catalogue_no_longer_has_instead_of_failing()
    {
        await using var db = fixture.CreateContext();
        var world = await ArrangeAsync(db, EventType.Corporate, packSize: 180, unknownSku: true);

        var result = await Seed(db).SeedAsync(
            world.EventId, EventType.Corporate, world.DivisionId, 180, world.UserId);
        await db.SaveChangesAsync();

        result.UnknownSkus.ShouldContain("GONE-999");
        result.StockRequirementsCreated.ShouldBe(1);
    }

    [Fact]
    public async Task Does_not_save_on_its_own()
    {
        await using var db = fixture.CreateContext();
        var world = await ArrangeAsync(db, EventType.Corporate, packSize: 180);

        await Seed(db).SeedAsync(world.EventId, EventType.Corporate, world.DivisionId, 180, world.UserId);

        // The caller owns the transaction (see IEventTemplateSeeder), so nothing is committed yet.
        await using var other = fixture.CreateContext();
        (await other.Boards.AnyAsync(b => b.EventId == world.EventId)).ShouldBeFalse();
    }

    private static EventTemplateSeeder Seed(CemDbContext db) =>
        new(db, TimeProvider.System, NullLogger<EventTemplateSeeder>.Instance);

    private static async Task<World> ArrangeAsync(
        CemDbContext db,
        EventType eventType,
        int packSize,
        bool withTemplate = true,
        bool unknownSku = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var division = new Division { Code = $"T{suffix[..3]}", Name = $"Test division {suffix}" };
        var user = new AppUser
        {
            EmployeeNumber = $"E{suffix}",
            Email = $"{suffix}@example.test",
            FullName = "Test User",
            PasswordHash = "x",
            SecurityStamp = suffix,
            EmploymentType = EmploymentType.Permanent,
        };
        var client = new Client { Name = $"Client {suffix}", PaymentTermsDays = 30 };
        var category = new StockCategory { Name = $"Category {suffix}" };
        var item = new StockItem
        {
            CategoryId = category.CategoryId,
            Sku = $"ICE-{suffix}",
            Name = "Ice, bulk",
            Unit = "kg",
            IsConsumable = true,
            ConsumptionPerHundredGuests = 45m,
        };

        db.AddRange(division, user, client, category, item);

        var ev = new Event
        {
            EventCode = $"TST-{suffix[..6]}".ToUpperInvariant(),
            ClientId = client.ClientId,
            DivisionId = division.DivisionId,
            CreatedByUserId = user.UserId,
            Name = $"Test event {suffix}",
            EventType = eventType,
            Status = EventStatus.ConfirmedInPlanning,
            EventDate = DateOnly.FromDateTime(Doors),
            PackSizeEstimated = packSize,
            StartsAt = LoadIn,
            EndsAt = Doors.AddHours(6),
            CreatedAt = DateTime.UtcNow,
        };
        ev.Milestones.Add(new EventMilestone
        {
            MilestoneType = MilestoneType.LoadIn,
            ScheduledStart = LoadIn,
            ScheduledEnd = LoadIn.AddHours(4),
        });
        ev.Milestones.Add(new EventMilestone
        {
            MilestoneType = MilestoneType.Doors,
            ScheduledStart = Doors,
            ScheduledEnd = Doors.AddHours(5),
        });
        db.Add(ev);

        Guid? templateId = null;
        if (withTemplate)
        {
            var requirements = new List<TemplateStockRequirement>
            {
                new() { Sku = item.Sku, QuantityPerHundredGuests = 45m, SourceMode = SourceMode.Order },
            };
            if (unknownSku)
            {
                requirements.Add(new TemplateStockRequirement
                {
                    Sku = "GONE-999",
                    QuantityPerHundredGuests = 10m,
                    SourceMode = SourceMode.Order,
                });
            }

            var definition = new TemplateDefinition
            {
                EventTypes = [eventType],
                Columns =
                [
                    new() { Name = "Assigned", Position = 0 },
                    new() { Name = "In Progress / Needs Review", Position = 1 },
                    new() { Name = "Complete", Position = 2, IsDoneColumn = true },
                ],
                Cards =
                [
                    new()
                    {
                        Subject = "Pull stock",
                        ColumnName = "Assigned",
                        MilestoneType = MilestoneType.LoadIn,
                        OffsetHours = -48,
                    },
                    new()
                    {
                        Subject = "Bar set-up",
                        ColumnName = "Assigned",
                        MilestoneType = MilestoneType.Doors,
                        OffsetHours = -2,
                        Priority = CardPriority.Critical,
                    },
                ],
                DefaultStockRequirements = requirements,
            };

            var template = new ChecklistTemplate
            {
                DivisionId = division.DivisionId,
                Name = $"Run sheet {suffix}",
                BoardType = BoardType.Event,
                DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions),
            };
            db.Add(template);
            templateId = template.TemplateId;
        }

        await db.SaveChangesAsync();

        return new World(ev.EventId, division.DivisionId, user.UserId, templateId);
    }

    private sealed record World(Guid EventId, Guid DivisionId, Guid UserId, Guid? TemplateId);
}
