using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Auth;
using Shouldly;

namespace Carbonate.UnitTests.Masking;

public class FinancialMaskerTests
{
    private static readonly FinancialMasker Masker = new();

    public static TheoryData<string> AllRoles => [.. RoleNames.All];

    [Theory]
    [InlineData(RoleNames.Director, true)]
    [InlineData(RoleNames.EventManager, true)]
    [InlineData(RoleNames.Accounts, true)]
    [InlineData(RoleNames.OperationsManager, false)]
    [InlineData(RoleNames.CrewLead, false)]
    [InlineData(RoleNames.CasualCrew, false)]
    public void Price_cost_and_margin_are_kept_only_for_the_three_finance_roles(string role, bool canSee)
    {
        var dto = new QuoteDto { Total = 1150m, UnitCost = 400m, MarginPct = 22m, Name = "Naidoo Wedding" };

        Masker.Mask(dto, new TestUser(role));

        (dto.Total is not null).ShouldBe(canSee);
        (dto.UnitCost is not null).ShouldBe(canSee);
        (dto.MarginPct is not null).ShouldBe(canSee);
        dto.Name.ShouldBe("Naidoo Wedding");
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public void Masked_fields_are_absent_from_the_json_and_never_sent_as_null(string role)
    {
        var dto = new QuoteDto { Total = 1150m, UnitCost = 400m, MarginPct = 22m, Name = "x" };
        Masker.Mask(dto, new TestUser(role));

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var hidden = !RolePermissionMatrix.PermissionsFor(role).Contains(PermissionCodes.FinanceViewClientPrice);
        json.Contains("\"total\"").ShouldBe(!hidden);
        json.Contains("\"unitCost\"").ShouldBe(!hidden);
        json.Contains("\"marginPct\"").ShouldBe(!hidden);
        json.ShouldNotContain("null");
        json.ShouldContain("\"name\"");
    }

    [Theory]
    [InlineData(RoleNames.Director)]
    [InlineData(RoleNames.Accounts)]
    public void Director_and_Accounts_see_everyones_hourly_rate(string role)
    {
        var assignment = new CrewDto { UserId = Guid.NewGuid(), HourlyRate = 180m };

        Masker.Mask(assignment, new TestUser(role));

        assignment.HourlyRate.ShouldBe(180m);
    }

    [Theory]
    [InlineData(RoleNames.OperationsManager)]
    [InlineData(RoleNames.EventManager)]
    [InlineData(RoleNames.CrewLead)]
    [InlineData(RoleNames.CasualCrew)]
    public void Everyone_else_sees_only_their_own_hourly_rate(string role)
    {
        var me = new TestUser(role);
        var mine = new CrewDto { UserId = me.UserId, HourlyRate = 180m };
        var theirs = new CrewDto { UserId = Guid.NewGuid(), HourlyRate = 220m };

        Masker.Mask(new[] { mine, theirs }, me);

        mine.HourlyRate.ShouldBe(180m);
        theirs.HourlyRate.ShouldBeNull();
    }

    [Fact]
    public void A_staff_field_with_no_owner_is_hidden_from_anyone_who_cannot_see_all()
    {
        var dto = new OwnerlessStaffDto { ShiftCost = 900m };

        Masker.Mask(dto, new TestUser(RoleNames.EventManager));

        dto.ShiftCost.ShouldBeNull();
    }

    [Fact]
    public void Fields_inside_nested_objects_and_lists_are_masked()
    {
        var dto = new EventDetailDto
        {
            Budget = 90_000m,
            Quote = new QuoteDto
            {
                Total = 1150m,
                UnitCost = 400m,
                Lines = [new LineDto { Price = 10m, Cost = 4m }, new LineDto { Price = 20m, Cost = 8m }],
            },
        };

        Masker.Mask(dto, new TestUser(RoleNames.CrewLead));

        dto.Budget.ShouldBeNull();
        dto.Quote!.Total.ShouldBeNull();
        dto.Quote.Lines.ShouldAllBe(l => l.Price == null && l.Cost == null);
    }

    [Fact]
    public void Fields_inside_a_paged_result_are_masked()
    {
        var page = new PageDto<QuoteDto> { Items = [new QuoteDto { Total = 5m }, new QuoteDto { Total = 6m }] };

        Masker.Mask(page, new TestUser(RoleNames.OperationsManager));

        page.Items.ShouldAllBe(q => q.Total == null);
    }

    [Fact]
    public void Fields_inside_a_dictionary_are_masked()
    {
        var map = new Dictionary<string, QuoteDto> { ["a"] = new() { Total = 5m } };

        Masker.Mask(map, new TestUser(RoleNames.CasualCrew));

        map["a"].Total.ShouldBeNull();
    }

    [Fact]
    public void An_anonymous_caller_sees_no_financial_fields()
    {
        var dto = new QuoteDto { Total = 5m, UnitCost = 1m, MarginPct = 2m };

        Masker.Mask(dto, new TestUser());

        dto.Total.ShouldBeNull();
        dto.UnitCost.ShouldBeNull();
        dto.MarginPct.ShouldBeNull();
    }

    [Fact]
    public void Masking_null_and_plain_values_is_harmless()
    {
        Should.NotThrow(() => Masker.Mask(null, new TestUser(RoleNames.CasualCrew)));
        Should.NotThrow(() => Masker.Mask("text", new TestUser(RoleNames.CasualCrew)));
        Should.NotThrow(() => Masker.Mask(42, new TestUser(RoleNames.CasualCrew)));
    }

    [Fact]
    public void A_self_referencing_object_does_not_loop_forever()
    {
        var node = new LoopDto { Total = 1m };
        node.Next = node;

        Should.NotThrow(() => Masker.Mask(node, new TestUser(RoleNames.CasualCrew)));

        node.Total.ShouldBeNull();
    }

    [Fact]
    public void A_tagged_field_that_cannot_be_nulled_fails_loudly_instead_of_leaking()
    {
        var error = Should.Throw<InvalidOperationException>(
            () => Masker.Mask(new NonNullableDto { Total = 5m }, new TestUser(RoleNames.CasualCrew)));

        error.Message.ShouldContain("must be nullable");
    }

    /// <summary>
    /// Rule for every DTO in the code base: a [FinancialField] property is nullable, settable and
    /// omitted from the JSON when null, so masking removes the key rather than sending null.
    /// </summary>
    [Fact]
    public void Every_tagged_property_in_the_application_follows_the_rules()
    {
        var problems = new List<string>();

        var tagged = typeof(FinancialMasker).Assembly.GetTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.GetCustomAttribute<FinancialFieldAttribute>() is not null);

        foreach (var property in tagged)
        {
            var name = $"{property.DeclaringType!.Name}.{property.Name}";

            if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
            {
                problems.Add($"{name} is not nullable");
            }

            if (property.SetMethod is null)
            {
                problems.Add($"{name} has no setter");
            }

            var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
            if (ignore?.Condition != JsonIgnoreCondition.WhenWritingNull)
            {
                problems.Add($"{name} is missing [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]");
            }
        }

        problems.ShouldBeEmpty();
    }

    // Test DTOs, written the way real ones must be.

    public class QuoteDto
    {
        public string Name { get; set; } = "";

        [FinancialField(FinancialTier.Price)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Total { get; set; }

        [FinancialField(FinancialTier.Cost)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? UnitCost { get; set; }

        [FinancialField(FinancialTier.Margin)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? MarginPct { get; set; }

        public List<LineDto> Lines { get; set; } = [];
    }

    public class LineDto
    {
        [FinancialField(FinancialTier.Price)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Price { get; set; }

        [FinancialField(FinancialTier.Cost)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Cost { get; set; }
    }

    public class EventDetailDto
    {
        [FinancialField(FinancialTier.Price)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Budget { get; set; }

        public QuoteDto? Quote { get; set; }
    }

    public class CrewDto
    {
        public Guid UserId { get; set; }

        [FinancialField(FinancialTier.Staff)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? HourlyRate { get; set; }
    }

    public class OwnerlessStaffDto
    {
        [FinancialField(FinancialTier.Staff)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? ShiftCost { get; set; }
    }

    public class PageDto<T>
    {
        public List<T> Items { get; set; } = [];
    }

    public class LoopDto
    {
        [FinancialField(FinancialTier.Price)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Total { get; set; }

        public LoopDto? Next { get; set; }
    }

    public class NonNullableDto
    {
        [FinancialField(FinancialTier.Price)]
        public decimal Total { get; set; }
    }

    private sealed class TestUser : ICurrentUser
    {
        private readonly string[] _roles;

        public TestUser(params string[] roles)
        {
            _roles = roles;
            UserId = Guid.NewGuid();
        }

        public bool IsAuthenticated => _roles.Length > 0;
        public Guid UserId { get; }
        public IReadOnlyList<string> Roles => _roles;
        public string? IpAddress => null;

        public bool HasPermission(string code) =>
            _roles.Any(r => RolePermissionMatrix.PermissionsFor(r).Contains(code));

        public PermissionScope ScopeOf(string code) =>
            _roles.Select(r => RolePermissionMatrix.ScopeFor(r, code)).DefaultIfEmpty(PermissionScope.None).Max();
    }
}
