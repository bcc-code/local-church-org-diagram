using System.Globalization;
using System.Security.Claims;
using Backend.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Backend.Auth;

/// <summary>
/// Adds the signed-in user's <see cref="AppRoles"/> and <see cref="AppClaims.AdminTenant"/> claims from the Admins table
/// on every request. They aren't stored in the session cookie, so adding or removing an Admins row takes effect on the
/// next request.
/// </summary>
public sealed class RoleClaimsTransformation(AppDbContext db) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // May run more than once per request
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true }
            || principal.HasClaim(claim => claim.Type is AppClaims.Role or AppClaims.AdminTenant))
            return principal;

        if (principal.GetPersonUid() is not { } personUid)
            return principal;

        var tenantIds = await db.Admins.AsNoTracking()
            .Where(a => a.PersonUid == personUid)
            .Select(a => a.TenantId)
            .ToListAsync();

        if (tenantIds.Count == 0)
            return principal;

        List<Claim> claims = [];
        if (tenantIds.Contains(null))
            claims.Add(new Claim(AppClaims.Role, AppRoles.GlobalAdmin));
        var localTenants = tenantIds.OfType<long>().ToList();
        if (localTenants.Count > 0)
        {
            claims.Add(new Claim(AppClaims.Role, AppRoles.LocalAdmin));
            claims.AddRange(localTenants.Select(tenantId =>
                new Claim(AppClaims.AdminTenant, tenantId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)));
        }

        // Clone rather than mutate: the principal may be shared (e.g. cached by the cookie handler)
        var clone = principal.Clone();
        ((ClaimsIdentity)clone.Identity!).AddClaims(claims);
        return clone;
    }
}
