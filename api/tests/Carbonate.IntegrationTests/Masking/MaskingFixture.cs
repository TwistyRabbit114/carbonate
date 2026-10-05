using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Stock;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Carbonate.Infrastructure.Seeding;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace Carbonate.IntegrationTests.Masking;

//the suite's own world: one user per role signed in through the real login (and the real two-step
//code for the Director and Accounts), and one event carrying every kind of money the api has.
//it never touches the demo seed, so changing the demo can't make this pass or fail
public sealed class MaskingFixture : IAsyncLifetime
{
    private const string Password = "Masking-Suite-Only-Password-1";
    private const string TotpSecret = "MASKINGSUITESECRETFORTOTPCODES23";

    private readonly Dictionary<string, HttpClient> _clients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _userIds = new(StringComparer.Ordinal);

    public DbApiFactory Factory { get; private set; } = null!;

    public Guid EventId { get; private set; }
    public Guid QuoteId { get; private set; }
    public Guid OrderListId { get; private set; }
    public Guid AssetId { get; private set; }
    public DateOnly OrderFrom { get; private set; }
    public DateOnly OrderTo { get; private set; }

    //what the fixture puts on the crew rows, so the staff test can tell whose rate is whose
    public IReadOnlyDictionary<string, decimal> HourlyRates { get; } = new Dictionary<string, decimal>
    {
        [RoleNames.OperationsManager] = 210m,
        [RoleNames.EventManager] = 195m,
        [RoleNames.CrewLead] = 140m,
        [RoleNames.CasualCrew] = 95m,
    };

    public HttpClient ClientFor(string role) => _clients[role];

    public Guid UserIdFor(string role) => _userIds[role];

    //----------------------------------------------------------\\
    //                              SET UP
    //----------------------------------------------------------\\

    public async Task InitializeAsync()
    {
        var connectionString = await SharedSqlServer.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<CemDbContext>().UseSqlServer(connectionString).Options;
        await using (var db = new CemDbContext(options))
        {
            await db.Database.MigrateAsync();
            await new PlatformSeeder(db).SeedAsync();
        }

        Factory = new DbApiFactory(connectionString);

        await CreateUsersAsync();
        foreach (var role in RoleNames.All)
        {
            _clients[role] = await SignInAsync(role);
        }

        await ArrangeMoneyAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        await Factory.DisposeAsync();
    }

    private async Task CreateUsersAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CemDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name);

        foreach (var (role, index) in RoleNames.All.Select((role, index) => (role, index)))
        {
            var user = new AppUser
            {
                EmployeeNumber = $"MASK{index + 1:00}",
                Email = $"{role.ToLowerInvariant()}@masking.test",
                FullName = $"Masking {role}",
                EmploymentType = role == RoleNames.CasualCrew ? EmploymentType.Casual : EmploymentType.Permanent,
            };
            user.PasswordHash = passwords.Hash(user, Password);

            if (RoleNames.MfaRequired.Contains(role))
            {
                user.MfaEnabled = true;
                user.MfaSecretEncrypted = secrets.Protect(TotpSecret);
            }

            user.UserRoles.Add(new UserRole { User = user, Role = roles[role], GrantedAt = DateTime.UtcNow });
            db.Users.Add(user);
            _userIds[role] = user.UserId;
        }

        await db.SaveChangesAsync();
    }

    //the same steps the login screen takes, so the permissions come from the seeded roles
    //rather than from a token the test made up
    private async Task<HttpClient> SignInAsync(string role)
    {
        var client = Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = $"{role.ToLowerInvariant()}@masking.test", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();

        if (session.GetProperty("mfaRequired").GetBoolean())
        {
            var code = new Totp(Base32Encoding.ToBytes(TotpSecret)).ComputeTotp();
            var verify = await client.PostAsJsonAsync("/api/auth/mfa/verify",
                new { mfaToken = session.GetProperty("mfaToken").GetString(), code });
            Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
            session = await verify.Content.ReadFromJsonAsync<JsonElement>();
        }

        client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("accessToken").GetString());
        return client;
    }

    //----------------------------------------------------------\\
    //                              THE MONEY
    //----------------------------------------------------------\\

    //everything goes through the api as the Director, who holds every permission, so each value is
    //stored the way a real user would store it
    private async Task ArrangeMoneyAsync()
    {
        var director = ClientFor(RoleNames.Director);
        var (clientId, venueId, divisionId, categoryId) = await ReferenceDataAsync();

        //an earlier event for the same client, so cost history has a costing to compare against (FR-09)
        var earlier = await PostAsync(director, "/api/events", EventBody(clientId, venueId, divisionId, "MASK-EARLY", 10));
        await PostAsync(director, $"/api/events/{Id(earlier, "eventId")}/quotes", new { lines = QuoteLines });

        var main = await PostAsync(director, "/api/events", EventBody(clientId, venueId, divisionId, "MASK-MAIN", 30));
        EventId = Id(main, "eventId");
        var startsAt = main.GetProperty("startsAt").GetDateTime();

        foreach (var (role, rate) in HourlyRates)
        {
            await PostAsync(director, $"/api/events/{EventId}/crew", new
            {
                userId = UserIdFor(role),
                crewRole = role == RoleNames.CasualCrew ? "Bartender" : "Manager",
                shiftStart = startsAt.AddHours(-4),
                shiftEnd = startsAt.AddHours(7),
                hourlyRate = rate,
            });
        }

        QuoteId = Id(await PostAsync(director, $"/api/events/{EventId}/quotes", new { lines = QuoteLines }), "quoteId");

        //recording the PO also confirms the event, which the order list needs (FR-12, FR-28)
        var confirmation = await PostAsync(director, $"/api/events/{EventId}/confirmation", new
        {
            confirmationType = "PurchaseOrder",
            clientPoNumber = "PO-MASK-1",
            poReceivedDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            poAmount = 25000m,
        });
        await PostAsync(director, $"/api/events/{EventId}/invoices", new
        {
            confirmationId = Id(confirmation, "confirmationId"),
            issuedDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            amountIncVat = 2070m,
        });

        await ArrangeStockAsync(director, categoryId, DateOnly.FromDateTime(startsAt));
    }

    private async Task ArrangeStockAsync(HttpClient director, Guid categoryId, DateOnly eventDate)
    {
        var supplier = await PostAsync(director, "/api/suppliers",
            new { name = "Masking Ice Co.", leadTimeDays = 3, isLiquorSupplier = false, isActive = true });
        var ice = await PostAsync(director, "/api/stock/items", new
        {
            categoryId,
            defaultSupplierId = Id(supplier, "supplierId"),
            sku = "MASK-ICE",
            name = "Ice, bulk",
            unit = "kg",
            isConsumable = true,
            isAsset = false,
            consumptionPerHundredGuests = 50m,
            standardUnitCost = 4.20m,
            isActive = true,
        });
        var machine = await PostAsync(director, "/api/stock/items", new
        {
            categoryId,
            sku = "MASK-MCH",
            name = "Ice machine",
            unit = "units",
            isConsumable = false,
            isAsset = true,
            standardUnitCost = 18000m,
            isActive = true,
        });
        AssetId = Id(await PostAsync(director, "/api/equipment", new
        {
            stockItemId = Id(machine, "stockItemId"),
            serialNumber = "MASK-0001",
            condition = "Good",
            status = "Available",
        }), "assetId");

        var saved = await director.PutAsJsonAsync($"/api/events/{EventId}/stock-requirements", new
        {
            items = new[]
            {
                new
                {
                    stockItemId = Id(ice, "stockItemId"),
                    quantityRequired = 60m,
                    requiredByDate = eventDate.ToString("yyyy-MM-dd"),
                    sourceMode = "Order",
                },
            },
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        OrderFrom = DateOnly.FromDateTime(DateTime.UtcNow);
        OrderTo = eventDate.AddDays(7);
        var generated = await PostAsync(director, "/api/order-lists/generate",
            new { from = OrderFrom.ToString("yyyy-MM-dd"), to = OrderTo.ToString("yyyy-MM-dd") });
        OrderListId = Id(generated.GetProperty("orderLists")[0], "orderListId");

        //the crew lead reports it, the way it happens at an event, then the office prices it
        var report = new MultipartFormDataContent
        {
            { new StringContent("EquipmentFailure"), "IncidentType" },
            { new StringContent(AssetId.ToString()), "AssetId" },
            { new StringContent("1"), "Quantity" },
            { new StringContent("Stopped making ice halfway through the evening."), "Description" },
        };
        var reported = await ClientFor(RoleNames.CrewLead).PostAsync($"/api/events/{EventId}/incidents", report);
        Assert.Equal(HttpStatusCode.Created, reported.StatusCode);
        var incident = await reported.Content.ReadFromJsonAsync<JsonElement>();
        var priced = await director.PatchAsync($"/api/incidents/{Id(incident, "incidentId")}",
            JsonContent.Create(new { replacementCost = 4500m }));
        Assert.Equal(HttpStatusCode.OK, priced.StatusCode);
    }

    //----------------------------------------------------------\\
    //                              HELPERS
    //----------------------------------------------------------\\

    //10 x R100 at a cost of R40, and 2 x R400 at a cost of R250
    private static readonly object[] QuoteLines =
    [
        new { description = "Bar hire", quantity = 10m, category = "Infrastructure", unitCostToUs = 40m, unitPriceToClient = 100m },
        new { description = "Bartenders", quantity = 2m, category = "Crew", unitCostToUs = 250m, unitPriceToClient = 400m },
    ];

    //clients and stock categories have no endpoint to create them, so these go straight in
    private async Task<(Guid ClientId, Guid VenueId, Guid DivisionId, Guid CategoryId)> ReferenceDataAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CemDbContext>();

        var client = new Client { Name = "Masking Client", PaymentTermsDays = 30 };
        var venue = new Venue { Name = "Masking Venue", Address = "1 Test Street, Cape Town" };
        var category = new StockCategory { Name = "Masking Consumables" };
        db.AddRange(client, venue, category);
        await db.SaveChangesAsync();

        var division = await db.Divisions.FirstAsync(d => d.Code == "CE");
        return (client.ClientId, venue.VenueId, division.DivisionId, category.CategoryId);
    }

    private static object EventBody(Guid clientId, Guid venueId, Guid divisionId, string code, int daysAhead)
    {
        var starts = DateTime.UtcNow.AddDays(daysAhead).Date.AddHours(16);
        return new
        {
            eventCode = code,
            clientId,
            venueId,
            divisionId,
            name = $"Masking event {code}",
            eventType = "Corporate",
            eventDate = DateOnly.FromDateTime(starts).ToString("yyyy-MM-dd"),
            startsAt = starts,
            endsAt = starts.AddHours(6),
            packSizeEstimated = 120,
            paymentMode = "PurchaseOrder",
            infrastructureMode = "Owned",
            staffRequired = 6,
            isConfidential = false,
            budgetAmount = 85000m,
        };
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"POST {path} gave {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static Guid Id(JsonElement element, string name) => element.GetProperty(name).GetGuid();
}
