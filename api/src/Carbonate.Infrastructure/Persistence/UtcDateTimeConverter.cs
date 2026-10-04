using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Carbonate.Infrastructure.Persistence;

/// <summary>
/// Everything is stored as UTC in datetime2, which has no time zone. Without this, values come back
/// with an unspecified kind and are written to JSON without the Z that marks them as UTC.
/// </summary>
internal sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            write => write.Kind == DateTimeKind.Local ? write.ToUniversalTime() : write,
            read => DateTime.SpecifyKind(read, DateTimeKind.Utc))
    {
    }
}

internal sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            write => write.HasValue && write.Value.Kind == DateTimeKind.Local ? write.Value.ToUniversalTime() : write,
            read => read.HasValue ? DateTime.SpecifyKind(read.Value, DateTimeKind.Utc) : read)
    {
    }
}
