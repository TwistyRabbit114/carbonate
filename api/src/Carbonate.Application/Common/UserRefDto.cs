namespace Carbonate.Application.Common;

/// <summary>A user as other records show them: the id plus a display name.</summary>
public record UserRefDto(Guid UserId, string FullName);
