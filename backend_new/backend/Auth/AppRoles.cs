namespace Backend.Auth;

/// <summary>App roles, from <see cref="Data.Admin"/> rows, added per request by <see cref="RoleClaimsTransformation"/>.</summary>
public static class AppRoles
{
    /// <summary>Admin of every tenant: an Admins row with TenantId null.</summary>
    public const string GlobalAdmin = "GlobalAdmin";

    /// <summary>
    /// Admin of one or more tenants: Admins rows with a TenantId, each also added as an
    /// <see cref="AppClaims.AdminTenant"/> claim. Applies whatever the user's own church is.
    /// </summary>
    public const string LocalAdmin = "LocalAdmin";
}
