using System.Net;
using System.Reflection;
using System.Text.Json;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Auth;

namespace Carbonate.IntegrationTests.Masking;

//the client's most important rule: money never reaches a role that isn't allowed it, and when it's
//not allowed the key is missing from the json, not sent as null (FR-35, NFR-17). this suite gates
//every production deploy (NFR-28). ci's masking job picks it up by this category, so keep the name
[Trait("Category", "Masking")]
public sealed class MaskingTests(MaskingFixture fixture) : IClassFixture<MaskingFixture>
{
    public static TheoryData<string, string> RestrictedRoleCases =>
        MaskingCases.For(RoleNames.OperationsManager, RoleNames.CrewLead, RoleNames.CasualCrew);

    public static TheoryData<string, string> FinanceRoleCases =>
        MaskingCases.For(RoleNames.Director, RoleNames.EventManager, RoleNames.Accounts);

    public static TheoryData<string, string> EveryRoleCases => MaskingCases.For([.. RoleNames.All]);

    public static TheoryData<string> EveryRole => [.. RoleNames.All];

    //----------------------------------------------------------\\
    //                              PRICE, COST AND MARGIN
    //----------------------------------------------------------\\

    [Theory]
    [MemberData(nameof(RestrictedRoleCases))]
    public async Task Money_never_reaches_a_role_without_finance_permissions(string role, string endpoint)
    {
        var item = MaskingCases.Named(endpoint);

        using var response = await item.SendAsync(fixture.ClientFor(role), fixture);

        if (!MaskingCases.CanReach(role, item))
        {
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
            return;
        }

        //roles that do get in must really get in, or this would pass without reading anything
        Assert.True(response.IsSuccessStatusCode, $"{role} got {(int)response.StatusCode} from {item.Name}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var leaked = JsonWalker.PropertyNames(json.RootElement).Where(MaskingFields.Finance.Contains).Distinct().ToList();

        Assert.Empty(leaked);
    }

    [Theory]
    [MemberData(nameof(FinanceRoleCases))]
    public async Task Finance_roles_get_the_money_on_every_endpoint_they_can_open(string role, string endpoint)
    {
        var item = MaskingCases.Named(endpoint);

        using var response = await item.SendAsync(fixture.ClientFor(role), fixture);

        //Accounts doesn't hold incident.view, for one
        if (!MaskingCases.CanReach(role, item))
        {
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
            return;
        }

        Assert.True(response.IsSuccessStatusCode, $"{role} got {(int)response.StatusCode} from {item.Name}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = JsonWalker.PropertyNames(json.RootElement).ToHashSet(StringComparer.Ordinal);

        Assert.All(item.Expect, name => Assert.Contains(name, names));
    }

    //----------------------------------------------------------\\
    //                              STAFF COST
    //----------------------------------------------------------\\

    //Director and Accounts see every rate. everyone else sees a rate only on their own row (FR-35)
    [Theory]
    [MemberData(nameof(EveryRole))]
    public async Task Hourly_rates_show_on_every_row_for_Director_and_Accounts_and_only_on_your_own_row_otherwise(string role)
    {
        var seesEveryone = role is RoleNames.Director or RoleNames.Accounts;
        var me = fixture.UserIdFor(role);
        var rateOf = fixture.HourlyRates.ToDictionary(pair => fixture.UserIdFor(pair.Key), pair => pair.Value);

        using var response = await fixture.ClientFor(role).GetAsync($"/api/events/{fixture.EventId}/crew");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(fixture.HourlyRates.Count, rows.Count);

        foreach (var row in rows)
        {
            var userId = row.GetProperty("userId").GetGuid();
            var shown = row.TryGetProperty("hourlyRate", out var rate);

            if (seesEveryone || userId == me)
            {
                Assert.True(shown, $"{role} should see the rate on {userId}'s row");
                Assert.Equal(rateOf[userId], rate.GetDecimal());
            }
            else
            {
                Assert.False(shown, $"{role} can see the rate on someone else's row");
            }
        }
    }

    //the rate lives on the crew rows only, so it mustn't turn up anywhere else for anyone
    [Theory]
    [MemberData(nameof(EveryRoleCases))]
    public async Task Hourly_rates_never_ride_along_on_other_endpoints(string role, string endpoint)
    {
        var item = MaskingCases.Named(endpoint);

        using var response = await item.SendAsync(fixture.ClientFor(role), fixture);
        if (!response.IsSuccessStatusCode) return;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(JsonWalker.PropertyNames(json.RootElement), MaskingFields.Staff.Contains);
    }

    //----------------------------------------------------------\\
    //                              THE FIELD LIST
    //----------------------------------------------------------\\

    //a money field the suite doesn't know about would never be checked, so any property tagged
    //[FinancialField] has to be on MaskingFields under the same tier
    [Fact]
    public void Every_money_field_in_the_api_is_on_the_suite_list()
    {
        var tagged = typeof(FinancialFieldAttribute).Assembly.GetTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(property => (property, tag: property.GetCustomAttribute<FinancialFieldAttribute>()))
            .Where(pair => pair.tag is not null)
            .ToList();

        Assert.NotEmpty(tagged);
        Assert.All(tagged, pair =>
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(pair.property.Name);
            var list = pair.tag!.Tier switch
            {
                FinancialTier.Price => MaskingFields.Price,
                FinancialTier.Cost => MaskingFields.Cost,
                FinancialTier.Margin => MaskingFields.Margin,
                _ => MaskingFields.Staff,
            };
            Assert.True(list.Contains(name),
                $"{pair.property.DeclaringType?.Name}.{pair.property.Name} is {pair.tag.Tier} money, add \"{name}\" to MaskingFields");
        });
    }
}
