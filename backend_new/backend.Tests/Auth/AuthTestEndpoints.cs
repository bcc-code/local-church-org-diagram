using System.Security.Claims;
using Backend.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Tests.Auth;

public record WhoAmI(Guid PersonUid);

public record TenantProbe(long TenantId);

/// <summary>Test-only Wolverine.Http endpoints (under /api) exercising the app's authorization setup.</summary>
public static class AuthTestEndpoints
{
    /// <summary>No attribute: RequireAuthorizeOnAll applies the default policy.</summary>
    [WolverineGet("/test/auth/me")]
    public static WhoAmI Me(ClaimsPrincipal user) => new(user.GetPersonUid()!.Value);

    [AllowAnonymous]
    [WolverineGet("/test/auth/anonymous")]
    public static TenantProbe Anonymous() => new(0);

    [Authorize(Policy = AuthPolicies.GlobalAdmin)]
    [WolverineGet("/test/auth/global")]
    public static TenantProbe Global() => new(0);
}

public static class ReadTenantEndpoint
{
    public static Task<ProblemDetails> Before(long tenantId, ClaimsPrincipal user, IAuthorizationService authorization) =>
        authorization.AuthorizeTenantAsync(user, tenantId, AuthPolicies.TenantReader);

    [WolverineGet("/test/auth/tenants/{tenantId}")]
    public static TenantProbe Get(long tenantId) => new(tenantId);
}

public static class ChangeTenantEndpoint
{
    public static Task<ProblemDetails> Before(long tenantId, ClaimsPrincipal user, IAuthorizationService authorization) =>
        authorization.AuthorizeTenantAsync(user, tenantId, AuthPolicies.TenantAdmin);

    [WolverinePost("/test/auth/tenants/{tenantId}")]
    public static TenantProbe Post(long tenantId) => new(tenantId);
}
