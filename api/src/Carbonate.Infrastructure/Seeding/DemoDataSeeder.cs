using Carbonate.Application.Features.Stock;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Stock;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Seeding;

/// <summary>
/// The demo world from Appendix D: people, clients, venues, suppliers, stock and six events.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dev only.</b> Gated on <c>Seeding:Demo</c>, which is set on the dev App Service and not on
/// production. Demo passwords are documented in the README for the demo environment and nowhere else.
/// </para>
/// <para>
/// <b>Every date is relative to the seed time</b>, never hard-coded. An event seeded as "in three
/// weeks" is still in three weeks whenever the demo runs; a hard-coded November date quietly becomes a
/// past event in December and the statuses stop matching the dates.
/// </para>
/// <para>
/// Idempotent by a single check: if the demo events exist, it does nothing. There is no value in
/// half-seeding a demo world.
/// </para>
/// </remarks>
public sealed class DemoDataSeeder(
    CemDbContext db,
    IEventTemplateSeeder templateSeeder,
    IPasswordService passwords,
    ITotpService totp,
    ISecretProtector secrets,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    /// <summary>In the README for the demo environment only. Never a real client's data.</summary>
    private const string DemoPassword = "Carbonate-Demo-2026!";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Events.AnyAsync(ct))
        {
            logger.LogInformation("Demo data already present; nothing to seed.");
            return;
        }

        var divisions = await db.Divisions.ToDictionaryAsync(d => d.Code, d => d.DivisionId, ct);
        if (divisions.Count == 0)
        {
            logger.LogWarning("Divisions are not seeded yet; skipping the demo data.");
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        var users = await SeedUsersAsync(ct);
        var venues = SeedVenues();
        var clients = SeedClients();
        var suppliers = SeedSuppliers();
        var items = SeedStock(divisions["CE"], suppliers);

        await db.SaveChangesAsync(ct);

        await SeedEventsAsync(now, divisions, users, venues, clients, ct);
        await SeedAdminCardsAsync(now, users, ct);

        await db.SaveChangesAsync(ct);
        await ApplyDemoTrapsAsync(items, ct);

        logger.LogInformation("Demo data seeded.");
    }

    // ---- People ---------------------------------------------------------------------------

    private async Task<Dictionary<string, AppUser>> SeedUsersAsync(CancellationToken ct)
    {
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, ct);
        var people = new (string Role, string Name, string Email, EmploymentType Employment)[]
        {
            (RoleNames.Director, "Director", "director@carbon.demo", EmploymentType.Permanent),
            (RoleNames.OperationsManager, "Ops Manager", "ops@carbon.demo", EmploymentType.Permanent),
            (RoleNames.EventManager, "Sarah M.", "sarah@carbon.demo", EmploymentType.Permanent),
            (RoleNames.Accounts, "Bookkeeper", "accounts@carbon.demo", EmploymentType.Permanent),
            (RoleNames.CrewLead, "Thabo N.", "thabo@carbon.demo", EmploymentType.Permanent),
            (RoleNames.CasualCrew, "Priya R.", "priya@carbon.demo", EmploymentType.Casual),
        };

        var created = new Dictionary<string, AppUser>(StringComparer.Ordinal);
        var employeeNumber = 1;

        foreach (var (roleName, fullName, email, employment) in people)
        {
            var user = new AppUser
            {
                EmployeeNumber = $"CE{employeeNumber++:000}",
                Email = email,
                FullName = fullName,
                EmploymentType = employment,
            };

            user.PasswordHash = passwords.Hash(user, DemoPassword);

            // Director and Accounts need MFA (NFR-18), so they are enrolled up front rather than
            // leaving the demo to walk through enrolment on stage.
            if (RoleNames.MfaRequired.Contains(roleName))
            {
                user.MfaEnabled = true;
                user.MfaSecretEncrypted = secrets.Protect(totp.GenerateSecret());
            }

            user.UserRoles.Add(new UserRole { User = user, Role = roles[roleName], GrantedAt = DateTime.UtcNow });

            db.Users.Add(user);
            created[roleName] = user;
        }

        return created;
    }

    // ---- The world the events happen in -----------------------------------------------------

    private Dictionary<string, Venue> SeedVenues()
    {
        var venues = new Dictionary<string, Venue>(StringComparer.Ordinal)
        {
            ["Steenberg"] = new()
            {
                Name = "Steenberg Estate",
                Address = "Steenberg Road, Tokai, Cape Town",
                AccessRoute = "Service gate off Steenberg Road; no vehicles past the lawn.",
                LoadingBayDetails = "Gravel bay behind the cellar. No loading dock — tail lift needed.",
                OperatingHoursStart = new TimeOnly(7, 0),
                OperatingHoursEnd = new TimeOnly(23, 0),
                RequiresHealthSafetyFile = true,
            },
            ["Waterfront"] = new()
            {
                Name = "V&A Waterfront",
                Address = "Dock Road, V&A Waterfront, Cape Town",
                AccessRoute = "Loading via Dock Road service entrance. Permit required.",
                LoadingBayDetails = "Shared bay, 30-minute limit, strictly enforced.",
                OperatingHoursStart = new TimeOnly(6, 0),
                OperatingHoursEnd = new TimeOnly(2, 0),
                RequiresSecurityClearance = true,
                RequiresHealthSafetyFile = true,
                PpeRequirements = "Hi-vis and closed shoes in the service corridors.",
            },
            ["Point"] = new()
            {
                Name = "The Point Hotel",
                Address = "Beach Road, Sea Point, Cape Town",
                LoadingBayDetails = "Basement bay, 2.1 m height limit.",
                OperatingHoursStart = new TimeOnly(6, 0),
                OperatingHoursEnd = new TimeOnly(1, 0),
                RequiresSecurityClearance = true,
            },
            ["Riverside"] = new()
            {
                Name = "Riverside Grounds",
                Address = "Liesbeek Parkway, Observatory, Cape Town",
                AccessRoute = "Field access off Liesbeek Parkway. Soft ground after rain.",
                RequiresHealthSafetyFile = true,
                PpeRequirements = "Hi-vis at all times during build.",
            },
            ["SteenbergGolf"] = new()
            {
                Name = "Steenberg Golf Club",
                Address = "Steenberg Road, Tokai, Cape Town",
                OperatingHoursStart = new TimeOnly(6, 0),
                OperatingHoursEnd = new TimeOnly(20, 0),
            },
        };

        db.Venues.AddRange(venues.Values);
        return venues;
    }

    private Dictionary<string, Client> SeedClients()
    {
        var clients = new Dictionary<string, Client>(StringComparer.Ordinal)
        {
            // Confidentiality is a property of the client as well as the event: everything for this
            // family is redacted on the calendar, not only the wedding.
            ["Naidoo"] = new() { Name = "Naidoo Family", PaymentTermsDays = 0, ConfidentialityRequired = true },
            ["Vantage"] = new() { Name = "Vantage Brands", TradingName = "Vantage", PaymentTermsDays = 30 },
            ["Meridian"] = new() { Name = "Meridian Group", PaymentTermsDays = 30 },
            ["Riverlight"] = new() { Name = "Riverlight Events", PaymentTermsDays = 14 },
            ["Delacroix"] = new() { Name = "Delacroix Holdings", PaymentTermsDays = 30 },
        };

        db.Clients.AddRange(clients.Values);
        return clients;
    }

    private Dictionary<string, Supplier> SeedSuppliers()
    {
        var suppliers = new Dictionary<string, Supplier>(StringComparer.Ordinal)
        {
            ["CoastalIce"] = new()
            {
                Name = "Coastal Ice Co.",
                ContactName = "Riaan",
                Email = "orders@coastalice.demo",
                Phone = "021 555 0101",
                LeadTimeDays = 5,
            },
            ["Vine"] = new()
            {
                Name = "Vine & Co. Distributors",
                ContactName = "Lerato",
                Email = "orders@vineandco.demo",
                Phone = "021 555 0202",
                LeadTimeDays = 2,
                IsLiquorSupplier = true,
            },
        };

        db.Suppliers.AddRange(suppliers.Values);
        return suppliers;
    }

    private Dictionary<string, StockItem> SeedStock(Guid divisionId, Dictionary<string, Supplier> suppliers)
    {
        var disposables = new StockCategory { Name = "Disposables", DivisionId = divisionId };
        var consumables = new StockCategory { Name = "Consumables", DivisionId = divisionId };
        var glassware = new StockCategory { Name = "Glassware", DivisionId = divisionId };
        var barKit = new StockCategory { Name = "Bar kit", DivisionId = divisionId };
        db.StockCategories.AddRange(disposables, consumables, glassware, barKit);

        // TODO(plan): unit costs are placeholders. Appendix D says to use Carbon's real figures from
        // the Head of Logistics and the Bookkeeper. Listed in docs/seed-data.md.
        var items = new Dictionary<string, StockItem>(StringComparer.Ordinal)
        {
            ["Cups"] = new()
            {
                CategoryId = disposables.CategoryId, Sku = "CUP-500", Name = "Cups 500 ml", Unit = "cups",
                IsConsumable = true, ReorderLevel = 2000m, ConsumptionPerHundredGuests = 220m,
                StandardUnitCost = 1.80m,
            },
            ["Ice"] = new()
            {
                CategoryId = consumables.CategoryId, DefaultSupplierId = suppliers["CoastalIce"].SupplierId,
                Sku = "ICE-BULK-KG", Name = "Ice, bulk", Unit = "kg",
                IsConsumable = true, ReorderLevel = 200m, ConsumptionPerHundredGuests = 45m,
                StandardUnitCost = 12.50m,
            },
            ["Spirits"] = new()
            {
                CategoryId = consumables.CategoryId, DefaultSupplierId = suppliers["Vine"].SupplierId,
                Sku = "SPIRIT-MIX", Name = "Spirits, mixed", Unit = "bottles",
                IsConsumable = true, ReorderLevel = 24m, ConsumptionPerHundredGuests = 8m,
                StandardUnitCost = 210.00m,
            },
            ["WineGlass"] = new()
            {
                CategoryId = glassware.CategoryId, Sku = "GLASS-WINE", Name = "Wine glasses", Unit = "glasses",
                ConsumptionPerHundredGuests = 130m, StandardUnitCost = 14.00m,
            },
            ["ChampGlass"] = new()
            {
                CategoryId = glassware.CategoryId, Sku = "GLASS-CHAMP", Name = "Champagne flutes", Unit = "glasses",
                ConsumptionPerHundredGuests = 110m, StandardUnitCost = 16.00m,
            },
            // No default supplier on purpose: this is what exercises the "unassigned supplier"
            // path in FR-28.
            ["MobileBar"] = new()
            {
                CategoryId = barKit.CategoryId, Sku = "BAR-MOBILE", Name = "Mobile bar unit", Unit = "units",
                IsAsset = true, StandardUnitCost = 4500.00m,
            },
            ["IceMachine"] = new()
            {
                CategoryId = barKit.CategoryId, Sku = "ICE-MACHINE", Name = "Ice machine", Unit = "units",
                IsAsset = true, StandardUnitCost = 18500.00m,
            },
        };

        db.StockItems.AddRange(items.Values);

        // A serialised asset, so the FR-31 incident demo has something concrete to be reported against.
        db.EquipmentAssets.Add(new EquipmentAsset
        {
            StockItemId = items["IceMachine"].StockItemId,
            SerialNumber = "ICM-2024-0117",
            Condition = "Good",
            Status = "Available",
            PurchaseDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddYears(-2)),
        });

        db.StockLocations.Add(new StockLocation { Name = "Main store", Address = "Montague Gardens, Cape Town" });

        return items;
    }

    // ---- The six events -------------------------------------------------------------------

    private async Task SeedEventsAsync(
        DateTime now,
        Dictionary<string, Guid> divisions,
        Dictionary<string, AppUser> users,
        Dictionary<string, Venue> venues,
        Dictionary<string, Client> clients,
        CancellationToken ct)
    {
        var ce = divisions["CE"];
        var manager = users[RoleNames.EventManager].UserId;

        // Doors time, then everything else hangs off it. Each tuple is Appendix D's table.
        var specs = new (string Code, string Name, string Client, string Venue, EventType Type,
            EventStatus Status, int Pax, DateTime Doors, bool Confidential)[]
        {
            ("NAI-WED-26", "Naidoo Wedding", "Naidoo", "Steenberg", EventType.Wedding,
                EventStatus.ConfirmedInPlanning, 180, now.AddDays(21).Date.AddHours(16), true),

            // Four days out, which puts load-in inside Coastal Ice's five-day lead time (FR-29).
            ("VAN-ACT-26", "Vantage Brand Activation", "Vantage", "Waterfront", EventType.Activation,
                EventStatus.ConfirmedInPlanning, 600, now.AddDays(4).Date.AddHours(18), false),

            ("MER-YE-26", "Meridian Year-End Function", "Meridian", "Point", EventType.YearEnd,
                EventStatus.ConfirmedInPlanning, 240, now.AddDays(42).Date.AddHours(19), false),

            // Running right now, so the board shows a live event the moment the demo opens.
            ("RIV-FEST-26", "Riverlight Festival", "Riverlight", "Riverside", EventType.Festival,
                EventStatus.InProgress, 2500, now.AddHours(-2), false),

            ("DEL-GOLF-26", "Delacroix Corporate Golf Day", "Delacroix", "SteenbergGolf", EventType.Corporate,
                EventStatus.Finished, 90, now.AddDays(-14).Date.AddHours(11), false),
        };

        foreach (var spec in specs)
        {
            var loadIn = spec.Doors.AddHours(-8);
            var ends = spec.Status == EventStatus.InProgress ? now.AddHours(6) : spec.Doors.AddHours(6);

            var ev = new Event
            {
                EventCode = spec.Code,
                ClientId = clients[spec.Client].ClientId,
                VenueId = venues[spec.Venue].VenueId,
                DivisionId = ce,
                CreatedByUserId = manager,
                Name = spec.Name,
                EventType = spec.Type,
                Status = spec.Status,
                EventDate = DateOnly.FromDateTime(spec.Doors),
                HeadcountExpected = spec.Pax,
                HeadcountConfirmed = spec.Status == EventStatus.Enquired ? null : spec.Pax,
                PackSizeEstimated = spec.Pax,
                PackSizeActual = spec.Status is EventStatus.InProgress or EventStatus.Finished ? spec.Pax : null,
                PaymentMode = PaymentMode.PurchaseOrder,
                InfrastructureMode = InfrastructureMode.Owned,
                StaffRequired = Math.Max(2, spec.Pax / 50),
                IsConfidential = spec.Confidential,
                CreatedAt = now,
                StartsAt = loadIn,
                EndsAt = ends,
            };

            AddMilestones(ev, loadIn, spec.Doors, ends);
            db.Events.Add(ev);
        }

        // The enquiry. It must not appear on the events board (FR-01), which is the point of seeding
        // it: the demo proves a negative.
        var enquiry = new Event
        {
            EventCode = "ENQ-TBC-26",
            ClientId = clients["Meridian"].ClientId,
            VenueId = null,
            DivisionId = ce,
            CreatedByUserId = manager,
            Name = "Unnamed enquiry — awaiting brief",
            EventType = EventType.Corporate,
            Status = EventStatus.Enquired,
            EventDate = DateOnly.FromDateTime(now.AddDays(56)),
            PackSizeEstimated = 150,
            PaymentMode = PaymentMode.Deposit,
            InfrastructureMode = InfrastructureMode.Rented,
            StaffRequired = 3,
            CreatedAt = now,
            StartsAt = now.AddDays(56).Date.AddHours(10),
            EndsAt = now.AddDays(56).Date.AddHours(18),
        };
        db.Events.Add(enquiry);

        await db.SaveChangesAsync(ct);

        // Boards and stock requirements come from the FR-25 seeder rather than being written here, so
        // the demo exercises the real code path an event create would take.
        foreach (var ev in await db.Events.Where(e => e.Status != EventStatus.Enquired).ToListAsync(ct))
        {
            await templateSeeder.SeedAsync(
                ev.EventId, ev.EventType, ev.DivisionId, ev.PackSizeActual ?? ev.PackSizeEstimated, manager, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>The run sheet in the client's words: recce, load-in, doors, strike, load-out, debrief.</summary>
    private static void AddMilestones(Event ev, DateTime loadIn, DateTime doors, DateTime ends)
    {
        void Add(MilestoneType type, DateTime start, double hours, MilestoneStatus status) =>
            ev.Milestones.Add(new EventMilestone
            {
                MilestoneType = type,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(hours),
                Status = status,
            });

        var done = ev.Status == EventStatus.Finished ? MilestoneStatus.Done : MilestoneStatus.Planned;

        Add(MilestoneType.SiteVisit, loadIn.AddDays(-10), 2, done);
        Add(MilestoneType.LoadIn, loadIn, 4, done);
        Add(MilestoneType.Doors, doors, 0.5, done);
        Add(MilestoneType.Strike, ends, 2, done);
        Add(MilestoneType.LoadOut, ends.AddHours(2), 2, done);
        Add(MilestoneType.Debrief, ends.AddDays(1), 1, done);
        Add(MilestoneType.Reconciliation, ends.AddDays(3), 2, done);
    }

    // ---- The admin board ------------------------------------------------------------------

    private async Task SeedAdminCardsAsync(DateTime now, Dictionary<string, AppUser> users, CancellationToken ct)
    {
        var board = await db.Boards
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.BoardType == BoardType.Admin, ct);

        if (board is null || board.Columns.Count == 0 || await db.TaskCards.AnyAsync(ct))
        {
            return;
        }

        var columns = board.Columns.OrderBy(c => c.Position).ToList();
        var thabo = users[RoleNames.CrewLead];
        var priya = users[RoleNames.CasualCrew];
        var ops = users[RoleNames.OperationsManager].UserId;

        var cards = new (string Subject, CardPriority Priority, AppUser Assignee, int Column, int DueInDays)[]
        {
            ("Renew liquor licence — Riverside venue", CardPriority.High, thabo, 0, 12),
            ("Update PPE stock list", CardPriority.Normal, priya, 0, 20),
            ("Quarterly ice machine service", CardPriority.Normal, thabo, 1, 5),
            ("Health & safety file — October audit", CardPriority.Normal, priya, 2, -3),
        };

        foreach (var (subject, priority, assignee, columnIndex, dueInDays) in cards)
        {
            var column = columns[Math.Min(columnIndex, columns.Count - 1)];
            var card = new TaskCard
            {
                ColumnId = column.ColumnId,
                Subject = subject,
                Priority = priority,
                DueAt = now.AddDays(dueInDays),
                Position = column.Cards.Count,
                // The admin board keeps its own three-column vocabulary (D-009).
                Status = column.Name,
                CompletedAt = column.IsDoneColumn ? now.AddDays(dueInDays) : null,
                CreatedByUserId = ops,
                CreatedAt = now,
            };

            card.Assignments.Add(new TaskAssignment { UserId = assignee.UserId, AssignedAt = now });
            db.TaskCards.Add(card);
        }
    }

    // ---- The two deliberate traps -----------------------------------------------------------

    /// <summary>
    /// Appendix D asks for Vantage's ice to be planned <b>below</b> expected consumption, so FR-27's
    /// shortfall warning has something to warn about. Its load-in already falls inside Coastal Ice's
    /// five-day lead time, which gives FR-29 the same.
    /// </summary>
    private async Task ApplyDemoTrapsAsync(Dictionary<string, StockItem> items, CancellationToken ct)
    {
        var vantage = await db.Events.FirstOrDefaultAsync(e => e.EventCode == "VAN-ACT-26", ct);
        if (vantage is null)
        {
            return;
        }

        var ice = await db.EventStockRequirements
            .FirstOrDefaultAsync(r => r.EventId == vantage.EventId && r.StockItemId == items["Ice"].StockItemId, ct);

        if (ice is null)
        {
            logger.LogWarning("Vantage has no ice requirement; the FR-27 demo will not show a shortfall.");
            return;
        }

        // 600 guests at 45 kg per hundred is 270 expected. 200 planned, so the screen shows the gap.
        ice.QuantityRequired = 200m;
        ice.Notes = "Planned short deliberately — demonstrates the FR-27 shortfall warning.";

        await db.SaveChangesAsync(ct);
    }
}
