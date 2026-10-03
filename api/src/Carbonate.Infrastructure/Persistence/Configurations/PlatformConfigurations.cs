using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carbonate.Infrastructure.Persistence.Configurations;

internal class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.HasKey(x => x.UserId);
        b.Property(x => x.EmployeeNumber).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.PasswordHash).HasMaxLength(500);
        b.Property(x => x.SecurityStamp).HasMaxLength(64);
        b.Property(x => x.MfaSecretEncrypted).HasMaxLength(500);
        b.HasIndex(x => x.EmployeeNumber).IsUnique();
        b.HasIndex(x => x.Email).IsUnique();
        b.HasMany(x => x.UserRoles).WithOne(x => x.User)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> b)
    {
        b.HasKey(x => x.RoleId);
        b.Property(x => x.Name).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Name).IsUnique();
        b.HasMany(x => x.RolePermissions).WithOne(x => x.Role)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.HasKey(x => x.PermissionId);
        b.Property(x => x.Code).HasMaxLength(60);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasKey(x => x.TokenId);
        b.Property(x => x.TokenHash).HasMaxLength(128);
        b.Property(x => x.CreatedByIp).HasMaxLength(45);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.Ref<AppUser>(nameof(RefreshToken.UserId));
    }
}

internal class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ClusterOn(x => x.UploadedAt, x => x.DocumentId);
        b.Property(x => x.DocumentType).HasMaxLength(50);
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.BlobUri).HasMaxLength(1000);
        b.Property(x => x.Sha256Hash).HasMaxLength(64);
        b.HasIndex(x => x.EventId);
        b.Ref<Event>(nameof(Document.EventId), DeleteBehavior.Cascade);
        b.Ref<AppUser>(nameof(Document.UploadedByUserId));
    }
}

internal class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.HasKey(x => x.AuditId);
        b.Property(x => x.AuditId).ValueGeneratedOnAdd();
        b.Property(x => x.Action).HasMaxLength(100);
        b.Property(x => x.EntityName).HasMaxLength(100);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.BeforeJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.AfterJson).HasColumnType("nvarchar(max)");
        b.HasIndex(x => x.OccurredAt).IsDescending();
        b.HasIndex(x => new { x.EntityName, x.EntityId });
        b.Ref<AppUser>(nameof(AuditEntry.UserId));
    }
}
