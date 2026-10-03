using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carbonate.Infrastructure.Persistence.Configurations;

internal class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.HasKey(x => x.ClientId);
        b.Property(x => x.VatNumber).HasMaxLength(30);
        b.HasIndex(x => x.Name);
        b.HasMany(x => x.Contacts).WithOne()
            .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class ClientContactConfiguration : IEntityTypeConfiguration<ClientContact>
{
    public void Configure(EntityTypeBuilder<ClientContact> b)
    {
        b.HasKey(x => x.ContactId);
        b.Property(x => x.Position).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Phone).HasMaxLength(50);
    }
}

internal class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> b)
    {
        b.HasKey(x => x.DivisionId);
        b.Property(x => x.Code).HasMaxLength(10);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> b)
    {
        b.ClusterOn(x => x.CreatedAt, x => x.EventId);
        b.Property(x => x.EventCode).HasMaxLength(20);
        b.Property(x => x.ServiceScheduleJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.EventCode).IsUnique();
        b.HasIndex(x => new { x.EventDate, x.Status });
        b.HasIndex(x => x.ClientId);

        b.ToTable(t =>
        {
            // Same allowlist the API validates: 4 to 20 characters of letters, digits and hyphens.
            t.HasCheckConstraint("CK_Event_EventCode",
                "LEN([EventCode]) BETWEEN 4 AND 20 AND [EventCode] NOT LIKE '%[^A-Za-z0-9-]%'");
            t.HasCheckConstraint("CK_Event_Window", "[EndsAt] > [StartsAt]");
            t.HasCheckConstraint("CK_Event_PackSize", "[PackSizeEstimated] >= 0");
            t.HasCheckConstraint("CK_Event_ServiceSchedule",
                "[ServiceScheduleJson] IS NULL OR ISJSON([ServiceScheduleJson]) = 1");
        });

        b.Ref<Client>(nameof(Event.ClientId));
        b.Ref<Venue>(nameof(Event.VenueId));
        b.Ref<Division>(nameof(Event.DivisionId));
        b.Ref<AppUser>(nameof(Event.CreatedByUserId));

        b.HasMany(x => x.Milestones).WithOne()
            .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.CrewAssignments).WithOne()
            .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class EventMilestoneConfiguration : IEntityTypeConfiguration<EventMilestone>
{
    public void Configure(EntityTypeBuilder<EventMilestone> b)
    {
        b.HasKey(x => x.MilestoneId);
        b.HasIndex(x => new { x.EventId, x.ScheduledStart });
        b.ToTable(t => t.HasCheckConstraint("CK_EventMilestone_Window", "[ScheduledEnd] >= [ScheduledStart]"));
    }
}

internal class MilestoneDependencyConfiguration : IEntityTypeConfiguration<MilestoneDependency>
{
    public void Configure(EntityTypeBuilder<MilestoneDependency> b)
    {
        b.HasKey(x => x.DependencyId);
        b.HasIndex(x => new { x.PredecessorMilestoneId, x.SuccessorMilestoneId }).IsUnique();
        b.HasIndex(x => x.SuccessorMilestoneId);
        b.ToTable(t => t.HasCheckConstraint("CK_MilestoneDependency_NotSelf",
            "[PredecessorMilestoneId] <> [SuccessorMilestoneId]"));

        // Two cascading keys into the same table are not allowed, so only the predecessor cascades.
        b.Ref<EventMilestone>(nameof(MilestoneDependency.PredecessorMilestoneId), DeleteBehavior.Cascade);
        b.Ref<EventMilestone>(nameof(MilestoneDependency.SuccessorMilestoneId));
    }
}

internal class CrewAssignmentConfiguration : IEntityTypeConfiguration<CrewAssignment>
{
    public void Configure(EntityTypeBuilder<CrewAssignment> b)
    {
        b.HasKey(x => x.AssignmentId);
        b.Property(x => x.CrewRole).HasMaxLength(50);
        b.HasIndex(x => new { x.UserId, x.EventId });
        b.ToTable(t => t.HasCheckConstraint("CK_CrewAssignment_Shift", "[ShiftEnd] > [ShiftStart]"));
        b.Ref<AppUser>(nameof(CrewAssignment.UserId));
    }
}
