using System.Security.Claims;

namespace Backend.Auth;

/// <summary>
/// Claim types this app adds (<see cref="RoleClaimsTransformation"/>), as opposed to BCC sign-on's
/// (<see cref="BccClaims"/>). Never stored in the session cookie: they are added on every request.
/// </summary>
public static class AppClaims
{
    /// <summary>Holds <see cref="AppRoles"/>; BCC sign-on doesn't issue roles.</summary>
    public const string Role = "role";

    /// <summary>A tenant the user is a Local Admin of, one claim per tenant.</summary>
    public const string AdminTenant = "admin_tenant";

    public static bool IsGlobalAdmin(this ClaimsPrincipal user) => user.IsInRole(AppRoles.GlobalAdmin);

    /// <summary>The tenants the user is a Local Admin of (not every tenant for a Global Admin).</summary>
    public static IReadOnlySet<long> GetAdminTenantIds(this ClaimsPrincipal user) =>
        user.FindAll(AdminTenant).Select(claim => long.TryParse(claim.Value, out var id) ? id : (long?)null)
            .OfType<long>().ToHashSet();
}
