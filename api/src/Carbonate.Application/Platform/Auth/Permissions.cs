namespace Carbonate.Application.Platform.Auth;

public static class RoleNames
{
    public const string Director = "Director";
    public const string OperationsManager = "OperationsManager";
    public const string EventManager = "EventManager";
    public const string Accounts = "Accounts";
    public const string CrewLead = "CrewLead";
    public const string CasualCrew = "CasualCrew";

    public static readonly IReadOnlyList<string> All =
        [Director, OperationsManager, EventManager, Accounts, CrewLead, CasualCrew];

    /// <summary>Roles that must complete a TOTP step at sign-in (FR-34, NFR-18).</summary>
    public static readonly IReadOnlyList<string> MfaRequired = [Director, Accounts];
}

public static class PermissionCodes
{
    public const string EventViewAll = "event.view_all";
    public const string EventViewAssigned = "event.view_assigned";
    public const string EventCreate = "event.create";
    public const string EventEdit = "event.edit";
    public const string EventDelete = "event.delete";
    public const string EventTransition = "event.transition";
    public const string CrewAssign = "crew.assign";
    public const string FinanceViewClientPrice = "finance.view_client_price";
    public const string FinanceViewInternalCost = "finance.view_internal_cost";
    public const string FinanceViewMargin = "finance.view_margin";
    public const string FinanceViewStaffCost = "finance.view_staff_cost";
    public const string QuoteView = "quote.view";
    public const string QuoteEdit = "quote.edit";
    public const string QuoteApprove = "quote.approve";
    public const string ConfirmationRecord = "confirmation.record";
    public const string InvoiceView = "invoice.view";
    public const string InvoiceManage = "invoice.manage";
    public const string ReconciliationEdit = "reconciliation.edit";
    public const string ClientViewContacts = "client.view_contacts";
    public const string TaskEdit = "task.edit";
    public const string TaskMove = "task.move";
    public const string AdminTaskView = "admin_task.view";
    public const string AdminTaskEdit = "admin_task.edit";
    public const string AdminTaskAssign = "admin_task.assign";
    public const string AdminTaskReview = "admin_task.review";
    public const string VenueEdit = "venue.edit";
    public const string StockView = "stock.view";
    public const string StockManage = "stock.manage";
    public const string StockPlan = "stock.plan";
    public const string OrderGenerate = "order.generate";
    public const string OrderApprove = "order.approve";
    public const string OrderPlace = "order.place";
    public const string IncidentCreate = "incident.create";
    public const string IncidentView = "incident.view";
    public const string DocumentUpload = "document.upload";
    public const string DocumentViewConfidential = "document.view_confidential";
    public const string CalendarView = "calendar.view";
    public const string CalendarConnect = "calendar.connect";
    public const string UserManage = "user.manage";
    public const string AuditView = "audit.view";
}

/// <summary>
/// How far a role's permission reaches. Holding the permission is stored in the database; the scope
/// is a rule the services apply on top (plan section 7.4: "asg", "self", "own").
/// </summary>
public enum PermissionScope
{
    None = 0,
    Own = 1,
    Self = 2,
    Assigned = 3,
    All = 4,
}

/// <summary>The role-by-permission matrix from plan section 7.4. Seeding and the services read this.</summary>
public static class RolePermissionMatrix
{
    private const PermissionScope A = PermissionScope.All;
    private const PermissionScope G = PermissionScope.Assigned;
    private const PermissionScope S = PermissionScope.Self;
    private const PermissionScope O = PermissionScope.Own;
    private const PermissionScope N = PermissionScope.None;

    // Columns: Director, OperationsManager, EventManager, Accounts, CrewLead, CasualCrew.
    private static readonly (string Code, string Description, PermissionScope[] Scopes)[] Rows =
    [
        (PermissionCodes.EventViewAll, "See every event", [A, A, A, A, N, N]),
        (PermissionCodes.EventViewAssigned, "See events the user is assigned to", [A, A, A, A, A, A]),
        (PermissionCodes.EventCreate, "Create events", [A, A, A, N, N, N]),
        (PermissionCodes.EventEdit, "Edit events", [A, A, A, N, N, N]),
        (PermissionCodes.EventDelete, "Delete events", [A, N, N, N, N, N]),
        (PermissionCodes.EventTransition, "Move an event between stages or cancel it", [A, A, A, N, N, N]),
        (PermissionCodes.CrewAssign, "Assign crew to events", [A, A, A, N, N, N]),
        (PermissionCodes.FinanceViewClientPrice, "See client prices", [A, N, A, A, N, N]),
        (PermissionCodes.FinanceViewInternalCost, "See internal costs", [A, N, A, A, N, N]),
        (PermissionCodes.FinanceViewMargin, "See margins", [A, N, A, A, N, N]),
        (PermissionCodes.FinanceViewStaffCost, "See staff cost; everyone else sees their own", [A, S, S, A, S, S]),
        (PermissionCodes.QuoteView, "View costings", [A, N, A, A, N, N]),
        (PermissionCodes.QuoteEdit, "Create, edit and copy costings", [A, N, A, A, N, N]),
        (PermissionCodes.QuoteApprove, "Approve a costing above the threshold", [A, N, N, N, N, N]),
        (PermissionCodes.ConfirmationRecord, "Record a PO or deposit", [A, N, A, A, N, N]),
        (PermissionCodes.InvoiceView, "View invoices", [A, N, A, A, N, N]),
        (PermissionCodes.InvoiceManage, "Create invoices and mark them paid", [A, N, N, A, N, N]),
        (PermissionCodes.ReconciliationEdit, "Edit post-event reconciliation", [A, N, N, A, N, N]),
        (PermissionCodes.ClientViewContacts, "See client contact details", [A, A, A, A, N, N]),
        (PermissionCodes.TaskEdit, "Edit event task cards", [A, A, A, N, A, G]),
        (PermissionCodes.TaskMove, "Move event task cards", [A, A, A, N, A, G]),
        (PermissionCodes.AdminTaskView, "View admin tasks", [A, A, A, A, G, G]),
        (PermissionCodes.AdminTaskEdit, "Edit admin tasks", [A, A, A, A, G, G]),
        (PermissionCodes.AdminTaskAssign, "Assign admin tasks", [A, A, N, N, N, N]),
        (PermissionCodes.AdminTaskReview, "Complete or return an admin task", [A, A, N, N, N, N]),
        (PermissionCodes.VenueEdit, "Edit venues and site visits", [A, A, A, N, N, N]),
        (PermissionCodes.StockView, "View stock", [A, A, A, A, A, N]),
        (PermissionCodes.StockManage, "Manage the catalogue, suppliers, equipment and templates", [A, A, N, N, N, N]),
        (PermissionCodes.StockPlan, "Plan the stock for an event", [A, A, A, N, N, N]),
        (PermissionCodes.OrderGenerate, "Generate order lists", [A, A, A, N, N, N]),
        (PermissionCodes.OrderApprove, "Approve order lists", [A, N, N, A, N, N]),
        (PermissionCodes.OrderPlace, "Mark an order list as placed", [A, N, N, A, N, N]),
        (PermissionCodes.IncidentCreate, "Report an incident", [A, A, A, N, A, A]),
        (PermissionCodes.IncidentView, "View incidents", [A, A, A, N, G, O]),
        (PermissionCodes.DocumentUpload, "Upload event documents", [A, A, A, N, N, N]),
        (PermissionCodes.DocumentViewConfidential, "Open confidential documents", [A, A, A, A, N, N]),
        (PermissionCodes.CalendarView, "Use the calendar view", [A, A, A, A, G, G]),
        (PermissionCodes.CalendarConnect, "Connect the Google Calendar", [A, A, N, N, N, N]),
        (PermissionCodes.UserManage, "Create users and change roles", [A, A, N, N, N, N]),
        (PermissionCodes.AuditView, "View the audit trail", [A, N, N, N, N, N]),
    ];

    public static IReadOnlyList<(string Code, string Description)> Permissions =>
        [.. Rows.Select(r => (r.Code, r.Description))];

    public static IReadOnlyList<string> PermissionsFor(string role)
    {
        var column = ColumnOf(role);
        return column < 0 ? [] : [.. Rows.Where(r => r.Scopes[column] != N).Select(r => r.Code)];
    }

    public static PermissionScope ScopeFor(string role, string code)
    {
        var column = ColumnOf(role);
        var row = Rows.FirstOrDefault(r => r.Code == code);
        return column < 0 || row.Scopes is null ? N : row.Scopes[column];
    }

    private static int ColumnOf(string role)
    {
        for (var i = 0; i < RoleNames.All.Count; i++)
        {
            if (RoleNames.All[i] == role)
            {
                return i;
            }
        }

        return -1;
    }
}
