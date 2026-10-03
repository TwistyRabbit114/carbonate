using Carbonate.Domain.Features.Calendar;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carbonate.Infrastructure.Persistence.Configurations;

internal class CalendarAccountConfiguration : IEntityTypeConfiguration<CalendarAccount>
{
    public void Configure(EntityTypeBuilder<CalendarAccount> b)
    {
        b.HasKey(x => x.AccountId);
        b.Property(x => x.GoogleAccountEmail).HasMaxLength(256);
        b.Property(x => x.GoogleCalendarId).HasMaxLength(256);
        b.Property(x => x.GrantedScope).HasMaxLength(500);
        b.Property(x => x.TokenSecretName).HasMaxLength(200);
        b.Ref<AppUser>(nameof(CalendarAccount.ConnectedByUserId));
    }
}

internal class CalendarLinkConfiguration : IEntityTypeConfiguration<CalendarLink>
{
    public void Configure(EntityTypeBuilder<CalendarLink> b)
    {
        b.HasKey(x => x.LinkId);
        b.Property(x => x.GoogleEventId).HasMaxLength(256);
        b.Property(x => x.SyncStatus).HasMaxLength(30);
        b.Property(x => x.LastError).HasMaxLength(2000);
        // One Google entry per source entity per account, so a push can never duplicate (FR-41).
        b.HasIndex(x => new { x.AccountId, x.SourceEntityType, x.SourceEntityId }).IsUnique();
        b.Ref<CalendarAccount>(nameof(CalendarLink.AccountId), DeleteBehavior.Cascade);
    }
}

internal class CalendarOutboxConfiguration : IEntityTypeConfiguration<CalendarOutbox>
{
    public void Configure(EntityTypeBuilder<CalendarOutbox> b)
    {
        b.ClusterOn(x => x.EnqueuedAt, x => x.OutboxId);
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.HasIndex(x => x.NextAttemptAt);
        b.HasIndex(x => new { x.SourceEntityType, x.SourceEntityId });
    }
}
