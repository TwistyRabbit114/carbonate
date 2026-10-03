using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carbonate.Infrastructure.Persistence.Configurations;

internal class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> b)
    {
        b.HasKey(x => x.VenueId);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.AccessRoute).HasMaxLength(2000);
        b.Property(x => x.LoadingBayDetails).HasMaxLength(1000);
        b.Property(x => x.PpeRequirements).HasMaxLength(1000);
        b.HasIndex(x => x.Name);
    }
}

internal class SiteVisitConfiguration : IEntityTypeConfiguration<SiteVisit>
{
    public void Configure(EntityTypeBuilder<SiteVisit> b)
    {
        b.HasKey(x => x.SiteVisitId);
        b.Property(x => x.VehicleType).HasMaxLength(100);
        b.Property(x => x.SignInProcedure).HasMaxLength(2000);
        b.Property(x => x.SecurityCheckpoint).HasMaxLength(500);
        b.Property(x => x.RequiredDriverDetails).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.LicencePlate).HasMaxLength(20);
        b.Property(x => x.CrewNames).HasMaxLength(1000);
        b.Property(x => x.HealthSafetyFileRef).HasMaxLength(200);
        b.HasIndex(x => x.EventId);
        b.Ref<Event>(nameof(SiteVisit.EventId), DeleteBehavior.Cascade);
        b.Ref<AppUser>(nameof(SiteVisit.ConductedByUserId));
    }
}
