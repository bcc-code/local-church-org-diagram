using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Auth;

/// <summary>
/// Every policy, including the default one behind a bare [Authorize] (and Wolverine.Http's RequireAuthorizeOnAll),
/// requires a signed-in BCC member: a person UID, hasMembership and a church. Endpoints can rely on
/// <see cref="BccClaims.GetPersonUid"/> and <see cref="BccClaims.GetChurchId"/>.
/// </summary>
public static class AuthPolicies
{
    public const string GlobalAdmin = "GlobalAdmin";

    /// <summary>
    /// Resource-based (an <see cref="ITenantScoped"/>, e.g. <see cref="Tenant"/>): Global Admins and the tenant's Local
    /// Admins. For changes to a tenant's data.
    /// </summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>
    /// Resource-based (an <see cref="ITenantScoped"/>, e.g. <see cref="Tenant"/>): the tenant's admins and the members of
    /// its church (churchId claim). For reading a tenant's data.
    /// </summary>
    public const string TenantReader = "TenantReader";

    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IClaimsTransformation, RoleClaimsTransformation>();
        services.AddSingleton<IAuthorizationHandler, IsTenantAdmin>();
        services.AddSingleton<IAuthorizationHandler, IsTenantReader>();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireSignedInMember().Build())
            .AddPolicy(GlobalAdmin, policy => policy
                .RequireSignedInMember()
                .RequireRole(AppRoles.GlobalAdmin))
            .AddPolicy(TenantAdmin, policy => policy
                .RequireSignedInMember()
                .AddRequirements(new TenantAdminRequirement()))
            .AddPolicy(TenantReader, policy => policy
                .RequireSignedInMember()
                .AddRequirements(new TenantReaderRequirement()));

        return services;
    }

    private static AuthorizationPolicyBuilder RequireSignedInMember(this AuthorizationPolicyBuilder policy) =>
        policy
            .RequireAuthenticatedUser()
            // Without a church there's no tenant to read: treated like a person without membership
            .RequireAssertion(context => context.User.GetPersonUid() is not null && context.User.GetHasMembership() &&
                                         context.User.GetChurchId() is not null);
}
