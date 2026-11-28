using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Auth;

public static class TenantAuthorization
{
    /// <summary>Whether the user passes the tenant policy for the tenant-scoped data (e.g. a group).</summary>
    public static async Task<bool> IsAllowedAsync(
        this IAuthorizationService authorization, ClaimsPrincipal user, ITenantScoped resource, string policy) =>
        (await authorization.AuthorizeAsync(user, resource, policy)).Succeeded;

    /// <summary>
    /// For a Wolverine.Http endpoint's Before/Validate method: <see cref="WolverineContinue.NoProblems"/> when the user
    /// passes the tenant policy (<see cref="AuthPolicies.TenantReader"/> or <see cref="AuthPolicies.TenantAdmin"/>)
    /// for the tenant, otherwise a 403 problem, which stops the request.
    /// </summary>
    public static async Task<ProblemDetails> AuthorizeTenantAsync(
        this IAuthorizationService authorization, ClaimsPrincipal user, long tenantId, string policy)
    {
        var result = await authorization.AuthorizeAsync(user, new Tenant(tenantId), policy);
        return result.Succeeded
            ? WolverineContinue.NoProblems
            : new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"Not allowed for tenant {tenantId}",
            };
    }
}
