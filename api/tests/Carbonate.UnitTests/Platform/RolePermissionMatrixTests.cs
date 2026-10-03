using Carbonate.Application.Platform.Auth;
using Shouldly;

namespace Carbonate.UnitTests.Platform;

public class RolePermissionMatrixTests
{
    private static readonly string[] FinanceTiers =
    [
        PermissionCodes.FinanceViewClientPrice,
        PermissionCodes.FinanceViewInternalCost,
        PermissionCodes.FinanceViewMargin,
    ];

    [Theory]
    [InlineData(RoleNames.Director, true)]
    [InlineData(RoleNames.EventManager, true)]
    [InlineData(RoleNames.Accounts, true)]
    [InlineData(RoleNames.OperationsManager, false)]
    [InlineData(RoleNames.CrewLead, false)]
    [InlineData(RoleNames.CasualCrew, false)]
    public void Only_Director_Event_Manager_and_Accounts_see_price_cost_and_margin(string role, bool expected)
    {
        foreach (var tier in FinanceTiers)
        {
            RolePermissionMatrix.PermissionsFor(role).Contains(tier).ShouldBe(expected, $"{role} / {tier}");
        }
    }

    [Fact]
    public void The_three_money_tiers_always_move_together()
    {
        foreach (var role in RoleNames.All)
        {
            var held = FinanceTiers.Select(t => RolePermissionMatrix.PermissionsFor(role).Contains(t)).Distinct();
            held.Count().ShouldBe(1, role);
        }
    }

    [Theory]
    [InlineData(RoleNames.Director, PermissionScope.All)]
    [InlineData(RoleNames.Accounts, PermissionScope.All)]
    [InlineData(RoleNames.OperationsManager, PermissionScope.Self)]
    [InlineData(RoleNames.EventManager, PermissionScope.Self)]
    [InlineData(RoleNames.CrewLead, PermissionScope.Self)]
    [InlineData(RoleNames.CasualCrew, PermissionScope.Self)]
    public void Staff_cost_is_visible_in_full_only_to_Director_and_Accounts(string role, PermissionScope expected)
    {
        RolePermissionMatrix.ScopeFor(role, PermissionCodes.FinanceViewStaffCost).ShouldBe(expected);
    }

    [Fact]
    public void Only_the_Director_approves_quotes_and_views_the_audit_trail()
    {
        foreach (var role in RoleNames.All.Where(r => r != RoleNames.Director))
        {
            RolePermissionMatrix.PermissionsFor(role).ShouldNotContain(PermissionCodes.QuoteApprove);
            RolePermissionMatrix.PermissionsFor(role).ShouldNotContain(PermissionCodes.AuditView);
        }
    }

    [Fact]
    public void Accounts_sees_the_events_board_but_cannot_move_anything()
    {
        var accounts = RolePermissionMatrix.PermissionsFor(RoleNames.Accounts);

        accounts.ShouldContain(PermissionCodes.EventViewAll);
        accounts.ShouldNotContain(PermissionCodes.TaskMove);
        accounts.ShouldNotContain(PermissionCodes.EventTransition);
    }

    [Fact]
    public void Crew_roles_cannot_see_every_event()
    {
        RolePermissionMatrix.PermissionsFor(RoleNames.CrewLead).ShouldNotContain(PermissionCodes.EventViewAll);
        RolePermissionMatrix.PermissionsFor(RoleNames.CasualCrew).ShouldNotContain(PermissionCodes.EventViewAll);
    }

    [Fact]
    public void The_Director_holds_every_permission()
    {
        RolePermissionMatrix.PermissionsFor(RoleNames.Director).Count.ShouldBe(RolePermissionMatrix.Permissions.Count);
    }

    [Fact]
    public void Permission_codes_are_unique()
    {
        var codes = RolePermissionMatrix.Permissions.Select(p => p.Code).ToList();
        codes.Distinct().Count().ShouldBe(codes.Count);
    }
}
