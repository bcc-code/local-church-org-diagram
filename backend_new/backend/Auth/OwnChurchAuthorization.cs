using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Auth;

/// <summary>
/// For endpoints reading the user's own church as the tenant (until admins can pick another tenant, bean
/// org-diagram-bt6i).
/// </summary>
public static class OwnChurchAuthorization
{
    /// <summary>
    /// For a Before method: <see cref="AuthPolicies.TenantReader"/> against <see cref="BccClaims.GetChurchId"/>; a 403
    /// problem when denied or when the user has no church.
    /// </summary>
    public static Task<ProblemDetails> AuthorizeOwnChurchAsync(this IAuthorizationService authorization, ClaimsPrincipal user) =>
        user.GetChurchId() is { } churchId
            ? authorization.AuthorizeTenantAsync(user, churchId, AuthPolicies.TenantReader)
            : Task.FromResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "The user has no church to read",
            });

    /// <summary>The tenant <see cref="AuthorizeOwnChurchAsync"/> admitted, in the endpoint after it.</summary>
    public static long OwnChurchId(this ClaimsPrincipal user) =>
        user.GetChurchId() ?? throw new InvalidOperationException(
            $"No churchId claim: call {nameof(AuthorizeOwnChurchAsync)} in the endpoint's Before method");
}
