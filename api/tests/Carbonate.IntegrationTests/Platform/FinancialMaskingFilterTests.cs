using Carbonate.Application.Platform.Auth;
using Carbonate.IntegrationTests.Support;

namespace Carbonate.IntegrationTests.Platform;

/// <summary>
/// The safety-net filter, tested on the raw response text. Keys must be absent, not null. The full
/// per-endpoint suite across all six roles is the masking acceptance suite (NFR-28).
/// </summary>
[Trait("Category", "Masking")]
public class FinancialMaskingFilterTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] MoneyKeys = ["totalIncVat", "unitCostToUs", "marginPercent", "hourlyRate"];

    public static TheoryData<string> NoFinanceRoles =>
        [RoleNames.OperationsManager, RoleNames.CrewLead, RoleNames.CasualCrew];

    public static TheoryData<string> FinanceRoles =>
        [RoleNames.Director, RoleNames.EventManager, RoleNames.Accounts];

    [Theory]
    [MemberData(nameof(NoFinanceRoles))]
    public async Task Roles_without_finance_access_get_a_response_with_no_money_keys(string role)
    {
        var raw = await GetRawAsync(role);

        foreach (var key in MoneyKeys)
        {
            Assert.DoesNotContain($"\"{key}\"", raw);
        }

        Assert.DoesNotContain("null", raw);
        Assert.Contains("\"name\"", raw);
    }

    [Theory]
    [MemberData(nameof(FinanceRoles))]
    public async Task Finance_roles_get_price_cost_and_margin(string role)
    {
        var raw = await GetRawAsync(role);

        Assert.Contains("\"totalIncVat\"", raw);
        Assert.Contains("\"unitCostToUs\"", raw);
        Assert.Contains("\"marginPercent\"", raw);
    }

    [Theory]
    [InlineData(RoleNames.Director, true)]
    [InlineData(RoleNames.Accounts, true)]
    [InlineData(RoleNames.EventManager, false)]
    public async Task Only_Director_and_Accounts_see_another_persons_hourly_rate(string role, bool visible)
    {
        var raw = await GetRawAsync(role);

        Assert.Equal(visible, raw.Contains("\"hourlyRate\""));
    }

    private async Task<string> GetRawAsync(string role)
    {
        var client = factory.CreateClientWith([role], RolePermissionMatrix.PermissionsFor(role));
        var response = await client.GetAsync("/api/test/finance");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
