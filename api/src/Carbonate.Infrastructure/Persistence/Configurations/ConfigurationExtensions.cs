using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carbonate.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>
    /// Declares a foreign key to a principal that has no collection navigation back. Every foreign
    /// key states its delete behaviour: cascade only along parent-to-child chains, restrict for
    /// everything else, so SQL Server never sees multiple cascade paths.
    /// </summary>
    public static void Ref<TPrincipal>(
        this EntityTypeBuilder builder,
        string foreignKey,
        DeleteBehavior onDelete = DeleteBehavior.Restrict)
        where TPrincipal : class
    {
        builder.HasOne(typeof(TPrincipal), (string?)null)
            .WithMany()
            .HasForeignKey(foreignKey)
            .OnDelete(onDelete);
    }

    /// <summary>
    /// Marks the timestamp as the clustered index and leaves the GUID primary key non-clustered,
    /// so inserts append rather than split pages (project plan section 6).
    /// </summary>
    public static void ClusterOn<T>(
        this EntityTypeBuilder<T> builder,
        System.Linq.Expressions.Expression<Func<T, object?>> timestamp,
        System.Linq.Expressions.Expression<Func<T, object?>> key)
        where T : class
    {
        builder.HasKey(key).IsClustered(false);
        builder.HasIndex(timestamp).IsClustered();
    }
}
