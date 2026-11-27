using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Auth;

/// <summary>Data that belongs to one tenant (a BCC church ID): the resource the tenant policies authorize against.</summary>
public interface ITenantScoped
{
    long TenantId { get; }
}

/// <summary>A tenant on its own, to authorize against a tenant ID (e.g. from the route) rather than loaded data.</summary>
public sealed record Tenant(long TenantId) : ITenantScoped;

/// <summary>See <see cref="AuthPolicies.TenantAdmin"/>.</summary>
public sealed class TenantAdminRequirement : IAuthorizationRequirement;

/// <summary>See <see cref="AuthPolicies.TenantReader"/>.</summary>
public sealed class TenantReaderRequirement : IAuthorizationRequirement;

public sealed class IsTenantAdmin : AuthorizationHandler<TenantAdminRequirement, ITenantScoped>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TenantAdminRequirement requirement, ITenantScoped resource)
    {
        if (TenantAccess.IsAdmin(context.User, resource.TenantId))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public sealed class IsTenantReader : AuthorizationHandler<TenantReaderRequirement, ITenantScoped>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TenantReaderRequirement requirement, ITenantScoped resource)
    {
        if (TenantAccess.IsReader(context.User, resource.TenantId))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class TenantAccess
{
    /// <summary>Global Admins, and Local Admins of the tenant (whatever their own church).</summary>
    public static bool IsAdmin(ClaimsPrincipal user, long tenantId) =>
        user.IsGlobalAdmin() || user.GetAdminTenantIds().Contains(tenantId);

    /// <summary>The tenant's admins (<see cref="IsAdmin"/>) and the members of its church.</summary>
    public static bool IsReader(ClaimsPrincipal user, long tenantId) =>
        IsAdmin(user, tenantId) || user.GetChurchId() == tenantId;
}
