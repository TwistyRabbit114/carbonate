using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Carbonate.Api.Platform.Auth;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Every endpoint declares the permission it needs, for example <c>[HasPermission("event.create")]</c>.
/// Services check again, because services are reused outside controllers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Permission { get; } = permission;

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(Permission);
    }
}

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // The MFA token proves only the password step. It never carries permissions, but refuse it explicitly.
        var isMfaToken = context.User.HasClaim(ClaimNames.Purpose, ClaimNames.MfaPurpose);

        if (!isMfaToken && context.User.HasClaim(ClaimNames.Permission, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class AuthPolicies
{
    /// <summary>For the enrol and confirm steps, which a caller holding only an MFA token may use.</summary>
    public const string MfaPending = "MfaPending";
}
