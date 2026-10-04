using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Common;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// For endpoints that are in the published contract but not built yet. The module owner replaces
    /// the body and keeps the route, permission and DTOs; changing a published DTO needs the
    /// consumer's review (plan section 5).
    /// </summary>
    protected ObjectResult NotYetBuilt() => StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
    {
        Status = StatusCodes.Status501NotImplemented,
        Type = "/problems/not-implemented",
        Title = "Not built yet.",
        Detail = "This endpoint is part of the contract but has not been implemented.",
    });
}
