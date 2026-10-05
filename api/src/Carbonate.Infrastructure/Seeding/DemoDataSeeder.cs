using Carbonate.Application.Features.Commercial;
using Carbonate.Application.Features.Stock;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Commercial;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Stock;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    ISecretProtector secrets,
    IOptions<FinanceOptions> finance,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    /// <summary>In the README for the demo environment only. Never a real client's data.</summary>
    private const string DemoPassword = "Carbonate-Demo-2026!";

    /// <summary>
    /// The authenticator secret for the demo Director and Accounts, documented in the README for the demo
    /// environment. It must be known: a random secret that is thrown away leaves two accounts that need a
    /// code nobody can produce. Demo accounts only; fictional people, and never seeded in production.
    /// </summary>
    public const string DemoTotpSecret = "CARBONATEDEMOSECRETFORTOTPAPPS23";

    private static readonly string[] MfaDemoEmails = ["director@carbon.demo", "accounts@carbon.demo"];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Runs first and every time, so accounts seeded before the secret was fixed are put right too.
        await EnsureDemoMfaAsync(ct);

        if (await db.Events.AnyAsync(ct))
        {
            // The world is already there. Fill in the money and the crew if they are missing, so a
            // demo seeded before costings or crew existed can still show what each role sees.
            await EnsureDemoCommercialAsync(ct);
            await SeedCrewAsync(clock.GetUtcNow().UtcDateTime, ct);
            await db.SaveChangesAsync(ct);
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
        await SeedCrewAsync(now, ct);
        await SeedAdminCardsAsync(now, users, ct);

        await db.SaveChangesAsync(ct);
        await ApplyDemoTrapsAsync(items, ct);
        await EnsureDemoCommercialAsync(ct);

        logger.LogInformation("Demo data seeded.");
    }

    // ---- Money ----------------------------------------------------------------------------

    private sealed record DemoLine(string Description, decimal Quantity, decimal Cost, decimal Price, QuoteLineCategory Category);

    private sealed record DemoCosting(
        string EventCode,
        decimal Budget,
        QuoteStatus Status,
        bool ApprovedByDirector,
        string PoNumber,
        decimal PoAmount,
        InvoiceStatus? Invoice,
        DemoLine[] Lines);

    private static readonly DemoCosting[] Costings =
    [
        new("DEL-GOLF-26", 24_000m, QuoteStatus.Accepted, false, "PO-DG-4410", 21_300m, InvoiceStatus.Paid,
        [
            new("Mobile bar unit hire", 1, 3_500m, 6_500m, QuoteLineCategory.Infrastructure),
            new("Bartenders (shifts)", 4, 650m, 1_100m, QuoteLineCategory.Crew),
            new("Ice, bulk (kg)", 120, 14m, 28m, QuoteLineCategory.Ice),
            new("Glassware hire", 90, 6m, 12m, QuoteLineCategory.Glassware),
            new("Transport and set-up", 1, 1_800m, 3_200m, QuoteLineCategory.Transportation),
        ]),
        new("RIV-FEST-26", 700_000m, QuoteStatus.Accepted, true, "PO-RF-0092", 718_000m, InvoiceStatus.Issued,
        [
            new("Festival bar infrastructure (6 units)", 6, 18_000m, 32_000m, QuoteLineCategory.Infrastructure),
            new("Bar staff (shifts)", 60, 700m, 1_150m, QuoteLineCategory.Crew),
            new("Ice, bulk (kg)", 2_400, 14m, 26m, QuoteLineCategory.Ice),
            new("Spirits and mixers", 1, 140_000m, 245_000m, QuoteLineCategory.Stock),
            new("Glassware and cups", 2_500, 3m, 7m, QuoteLineCategory.Glassware),
            new("Logistics and strike", 1, 22_000m, 38_000m, QuoteLineCategory.Transportation),
        ]),
        // Above the approval threshold, already approved and issued.
        new("NAI-WED-26", 150_000m, QuoteStatus.Issued, true, "", 0m, null,
        [
            new("Bar hire and set-up", 1, 9_000m, 16_000m, QuoteLineCategory.SetUpAndStrike),
            new("Bartenders (shifts)", 8, 650m, 1_100m, QuoteLineCategory.Crew),
            new("Ice, bulk (kg)", 160, 14m, 28m, QuoteLineCategory.Ice),
            new("Premium glassware", 180, 8m, 18m, QuoteLineCategory.Glassware),
            new("Wedding stock package", 1, 52_000m, 88_000m, QuoteLineCategory.Stock),
        ]),
        // Above the threshold and waiting for the Director, so the approval can be shown live.
        new("VAN-ACT-26", 320_000m, QuoteStatus.PendingApproval, false, "PO-VAN-0873", 330_000m, null,
        [
            new("Activation bars (4 units)", 4, 14_000m, 24_000m, QuoteLineCategory.Infrastructure),
            new("Staff (shifts)", 36, 700m, 1_200m, QuoteLineCategory.Crew),
            new("Ice, bulk (kg)", 1_800, 14m, 27m, QuoteLineCategory.Ice),
            new("Branded cups", 600, 4m, 9m, QuoteLineCategory.Glassware),
            new("Stock", 1, 60_000m, 105_000m, QuoteLineCategory.Stock),
        ]),
        // Below the threshold, still a draft.
        new("MER-YE-26", 40_000m, QuoteStatus.Draft, false, "PO-MER-5521", 31_000m, null,
        [
            new("Bar hire", 2, 3_000m, 5_500m, QuoteLineCategory.Infrastructure),
            new("Bartenders (shifts)", 6, 650m, 1_100m, QuoteLineCategory.Crew),
            new("Ice, bulk (kg)", 150, 14m, 28m, QuoteLineCategory.Ice),
            new("Glassware hire", 240, 6m, 12m, QuoteLineCategory.Glassware),
        ]),
    ];

    /// <summary>
    /// Budgets, costings in each state, confirmations and invoices for the demo events. Without money the
    /// demo cannot show the client's main requirement: finance fields visible to three roles and absent
    /// for everyone else. Does nothing if any costing already exists.
    /// </summary>
    private async Task EnsureDemoCommercialAsync(CancellationToken ct)
    {
        if (await db.Quotes.AnyAsync(ct))
        {
            return;
        }

        var codes = Costings.Select(c => c.EventCode).ToList();
        var events = await db.Events.Where(e => codes.Contains(e.EventCode)).ToDictionaryAsync(e => e.EventCode, ct);
        if (events.Count == 0)
        {
            return;
        }

        var director = await db.Users.FirstOrDefaultAsync(u => u.Email == "director@carbon.demo", ct);
        var clientTerms = await db.Clients.ToDictionaryAsync(c => c.ClientId, c => c.PaymentTermsDays, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var invoiceNumber = 0;

        foreach (var costing in Costings.Where(c => events.ContainsKey(c.EventCode)))
        {
            var ev = events[costing.EventCode];
            ev.BudgetAmount = costing.Budget;

            var totals = QuoteCalculator.Calculate(
                [.. costing.Lines.Select(l => new QuoteLineInput(l.Quantity, l.Cost, l.Price))],
                finance.Value.VatRate);
            var issued = costing.Status is QuoteStatus.Issued or QuoteStatus.Accepted;

            var quote = new Quote
            {
                EventId = ev.EventId,
                Version = 1,
                Status = costing.Status,
                SubtotalExVat = totals.SubtotalExVat,
                VatAmount = totals.VatAmount,
                TotalIncVat = totals.TotalIncVat,
                ValidUntil = today.AddDays(30),
                CreatedAt = now.AddDays(-14),
                IssuedAt = issued ? now.AddDays(-10) : null,
                AcceptedAt = costing.Status == QuoteStatus.Accepted ? now.AddDays(-8) : null,
                ApprovedByUserId = costing.ApprovedByDirector ? director?.UserId : null,
                ApprovedAt = costing.ApprovedByDirector ? now.AddDays(-11) : null,
                Lines =
                [
                    .. costing.Lines.Select((l, i) => new QuoteLine
                    {
                        Description = l.Description,
                        Quantity = l.Quantity,
                        UnitCostToUs = l.Cost,
                        UnitPriceToClient = l.Price,
                        LineTotal = totals.LineTotals[i],
                        Category = l.Category,
                    }),
                ],
            };
            db.Quotes.Add(quote);

            // An event past Enquired has a confirmation. Naidoo paid a deposit; the others sent a PO.
            var deposit = costing.EventCode == "NAI-WED-26";
            var confirmation = new EventConfirmation
            {
                EventId = ev.EventId,
                ConfirmationType = deposit ? ConfirmationType.Deposit : ConfirmationType.PurchaseOrder,
                ClientPoNumber = deposit ? null : costing.PoNumber,
                PoReceivedDate = deposit ? null : today.AddDays(-12),
                PoAmount = deposit ? null : costing.PoAmount,
                DepositAmount = deposit ? 30_000m : null,
                DepositPaidDate = deposit ? today.AddDays(-9) : null,
                DepositReference = deposit ? "EFT-NAI-221" : null,
                ConfirmedAt = now.AddDays(-9),
            };
            db.EventConfirmations.Add(confirmation);

            if (costing.Invoice is { } status)
            {
                var issuedOn = today.AddDays(-6);
                var terms = clientTerms.GetValueOrDefault(ev.ClientId);
                db.Invoices.Add(new Invoice
                {
                    EventId = ev.EventId,
                    ConfirmationId = confirmation.ConfirmationId,
                    InvoiceNumber = $"INV-{today.Year}-{++invoiceNumber:0000}",
                    IssuedDate = issuedOn,
                    DueDate = issuedOn.AddDays(terms),
                    AmountIncVat = totals.TotalIncVat,
                    Status = status,
                    PaidDate = status == InvoiceStatus.Paid ? issuedOn.AddDays(Math.Min(terms, 10)) : null,
                    CreatedAt = now.AddDays(-6),
                });
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo costings, confirmations and invoices seeded.");
    }

    // ---- People ---------------------------------------------------------------------------

    /// <summary>Gives the demo Director and Accounts the documented authenticator secret if they do not already have it.</summary>
    private async Task EnsureDemoMfaAsync(CancellationToken ct)
    {
        var accounts = await db.Users.Where(u => MfaDemoEmails.Contains(u.Email)).ToListAsync(ct);
        var repaired = 0;

        foreach (var account in accounts.Where(a => !HasDemoSecret(a)))
        {
            account.MfaEnabled = true;
            account.MfaSecretEncrypted = secrets.Protect(DemoTotpSecret);
            repaired++;
        }

        if (repaired > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Gave {Count} demo account(s) the documented authenticator secret.", repaired);
        }
    }

    private bool HasDemoSecret(AppUser account)
    {
        if (!account.MfaEnabled || account.MfaSecretEncrypted is null)
        {
            return false;
        }

        try
        {
            return secrets.Unprotect(account.MfaSecretEncrypted) == DemoTotpSecret;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            // Encrypted under a key we no longer hold, so it cannot be used. Replace it.
            return false;
        }
    }

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
                user.MfaSecretEncrypted = secrets.Protect(DemoTotpSecret);
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

    // ---- Crew on events -------------------------------------------------------------------

    /// <summary>
    /// FR-07. Appendix D names Thabo and Priya but never puts them on an event, so "My events" was
    /// empty for both crew accounts and the crew-scoped visibility rule had nothing to scope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Priya holds only the casual crew role, so <see cref="Carbonate.Domain.Platform.CrewExpiry"/>
    /// would deactivate her account once every shift is behind her (FR-36). She is given a shift six
    /// weeks out as well as the live one on purpose — with only past work, the demo account stops
    /// being able to sign in a day after the world is seeded.
    /// </para>
    /// <para>
    /// Naidoo is left out deliberately. It is the confidential event, so excluding it gives the
    /// visibility rules a negative case the demo can prove rather than assert.
    /// </para>
    /// </remarks>
    private async Task SeedCrewAsync(DateTime now, CancellationToken ct)
    {
        if (await db.CrewAssignments.AnyAsync(ct))
        {
            return;
        }

        // Looked up rather than passed in, so this also runs against a world that was seeded before
        // crew existed — the same reason EnsureDemoCommercialAsync backfills money.
        var crew = await db.Users
            .Where(u => u.Email == "thabo@carbon.demo" || u.Email == "priya@carbon.demo")
            .ToDictionaryAsync(u => u.Email, u => u.UserId, ct);

        if (crew.Count < 2)
        {
            logger.LogWarning("The demo crew accounts are missing; no crew assignments seeded.");
            return;
        }

        var thabo = crew["thabo@carbon.demo"];
        var priya = crew["priya@carbon.demo"];

        // Rates are $staff-tier money (FR-35): an Event Manager sees them, a Crew Lead does not.
        // Seeding them is what gives that masking tier something to demonstrate.
        var specs = new (string Code, Guid UserId, string CrewRole, decimal Rate, bool Confirmed)[]
        {
            // Running now, so both crew accounts have something live the moment the demo opens.
            ("RIV-FEST-26", thabo, "Crew lead", 185.00m, true),
            ("RIV-FEST-26", priya, "Bar staff", 140.00m, true),

            // Four days out. Also the event carrying the FR-27 and FR-29 stock warnings.
            ("VAN-ACT-26", thabo, "Crew lead", 185.00m, true),

            // Finished a fortnight ago: past work, so the crew view is not only what is ahead.
            ("DEL-GOLF-26", thabo, "Crew lead", 185.00m, true),

            // Six weeks out and not yet confirmed, which keeps Priya's account alive and shows the
            // unconfirmed state.
            ("MER-YE-26", priya, "Bar staff", 140.00m, false),
        };

        var codes = specs.Select(s => s.Code).Distinct().ToArray();
        var events = await db.Events
            .Where(e => codes.Contains(e.EventCode))
            .ToDictionaryAsync(e => e.EventCode, ct);

        foreach (var spec in specs)
        {
            if (!events.TryGetValue(spec.Code, out var ev))
            {
                logger.LogWarning("Demo event {Code} is missing; no crew seeded for it.", spec.Code);
                continue;
            }

            db.CrewAssignments.Add(new CrewAssignment
            {
                EventId = ev.EventId,
                UserId = spec.UserId,
                CrewRole = spec.CrewRole,
                // Crew are on site from load-in until load-out is finished, which is two hours after
                // strike plus the two hours load-out itself takes (see AddMilestones).
                ShiftStart = ev.StartsAt,
                ShiftEnd = ev.EndsAt.AddHours(4),
                Confirmed = spec.Confirmed,
                HourlyRate = spec.Rate,
            });
        }

        logger.LogInformation("Seeded {Count} crew assignments as at {Now:u}.", specs.Length, now);
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
