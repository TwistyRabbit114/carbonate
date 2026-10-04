namespace Carbonate.Domain.Common;

public enum EventStatus { Enquired, ConfirmedInPlanning, InProgress, Finished, Cancelled }

public enum EventType { Activation, Corporate, Wedding, Festival, YearEnd, Private }

public enum MilestoneType
{
    SiteVisit, LoadIn, Rehearsal, Doors, Strike, LoadOut, Debrief, Invoice, Reconciliation
}

public enum MilestoneStatus { Planned, InProgress, Done }

public enum DependencyType { FS }

public enum CardPriority { Low, Normal, High, Critical }

public enum BoardType { Event, Admin }

public enum QuoteStatus { Draft, PendingApproval, Approved, Issued, Accepted, Superseded }

public enum QuoteLineCategory
{
    SetUpAndStrike, Infrastructure, Transportation, Crew, Ice, Stock, BarKit, Glassware, Other
}

public enum InvoiceStatus { Draft, Issued, Paid, Void }

public enum PaymentMode { PurchaseOrder, Deposit }

public enum ConfirmationType { PurchaseOrder, Deposit }

public enum InfrastructureMode { Owned, Rented }

public enum SourceMode { Stock, Order, Rent }

public enum OrderListStatus { Draft, PendingApproval, Approved, Placed }

public enum IncidentType { Breakage, EquipmentFailure, StockShortfall }

public enum SensitivityLevel { Normal, Confidential }

public enum EmploymentType { Permanent, Casual }

public enum CalendarSourceType { Event, Milestone, TaskCard }

public enum OutboxOperation { Upsert, Delete }
