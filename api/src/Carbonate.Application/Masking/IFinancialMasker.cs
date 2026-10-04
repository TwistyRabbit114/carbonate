using Carbonate.Application.Platform.Auth;

namespace Carbonate.Application.Masking;

public interface IFinancialMasker
{
    /// <summary>
    /// Sets every financial field the user may not see to null, recursively through nested objects and
    /// collections. Call it in the service before returning a DTO; a global filter calls it again as a
    /// safety net. It changes the object in place, so only pass DTOs, never entities.
    /// </summary>
    void Mask(object? dto, ICurrentUser user);
}
