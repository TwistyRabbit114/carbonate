using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Calendar;
using Carbonate.Domain.Features.Commercial;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Stock;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Persistence;

public class CemDbContext(DbContextOptions<CemDbContext> options) : DbContext(options)
{
    // Platform
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Events
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventMilestone> EventMilestones => Set<EventMilestone>();
    public DbSet<MilestoneDependency> MilestoneDependencies => Set<MilestoneDependency>();
    public DbSet<CrewAssignment> CrewAssignments => Set<CrewAssignment>();

    // Venues
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<SiteVisit> SiteVisits => Set<SiteVisit>();

    // Commercial
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<EventConfirmation> EventConfirmations => Set<EventConfirmation>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<ReconciliationLine> ReconciliationLines => Set<ReconciliationLine>();

    // Boards
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<TaskCard> TaskCards => Set<TaskCard>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<CardAttachment> CardAttachments => Set<CardAttachment>();

    // Stock, equipment and incidents
    public DbSet<StockCategory> StockCategories => Set<StockCategory>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockLocation> StockLocations => Set<StockLocation>();
    public DbSet<InventoryLevel> InventoryLevels => Set<InventoryLevel>();
    public DbSet<EventStockRequirement> EventStockRequirements => Set<EventStockRequirement>();
    public DbSet<OrderList> OrderLists => Set<OrderList>();
    public DbSet<OrderListLine> OrderListLines => Set<OrderListLine>();
    public DbSet<EquipmentAsset> EquipmentAssets => Set<EquipmentAsset>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();

    // Calendar
    public DbSet<CalendarAccount> CalendarAccounts => Set<CalendarAccount>();
    public DbSet<CalendarLink> CalendarLinks => Set<CalendarLink>();
    public DbSet<CalendarOutbox> CalendarOutbox => Set<CalendarOutbox>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as strings, money as decimal(18,2), and no string is left unbounded
        // unless a configuration says so.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CemDbContext).Assembly);
    }
}
