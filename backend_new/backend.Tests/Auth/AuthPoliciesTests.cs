using Backend.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Backend.Tests.Auth;

public class AuthPoliciesTests
{
    private const long Copenhagen = TestUser.Copenhagen;
    private const long Oslo = 1;

    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddLogging()
        .AddAppAuthorization()
        .BuildServiceProvider();

    private static readonly IAuthorizationService Authorization = Services.GetRequiredService<IAuthorizationService>();

    private static async Task<AuthorizationPolicy> DefaultPolicy() =>
        await Services.GetRequiredService<IAuthorizationPolicyProvider>().GetDefaultPolicyAsync();

    /// <summary>The named policy, or the default one (bare [Authorize]) when null, against the tenant if given.</summary>
    private static async Task<bool> Passes(TestUser user, string? policy, long? tenantId = null)
    {
        var resource = tenantId is { } id ? new Tenant(id) : null;
        var result = policy is null
            ? await Authorization.AuthorizeAsync(user.Principal(), resource, await DefaultPolicy())
            : await Authorization.AuthorizeAsync(user.Principal(), resource, policy);
        return result.Succeeded;
    }

    [Fact]
    public async Task Default_admits_signed_in_members()
    {
        (await Passes(TestUser.Member(), null)).ShouldBeTrue();
    }

    [Fact]
    public async Task Default_requires_membership()
    {
        (await Passes(TestUser.NonMember(), null)).ShouldBeFalse();
        (await Passes(TestUser.NonMember() with { GlobalAdmin = true }, AuthPolicies.GlobalAdmin)).ShouldBeFalse();
    }

    [Fact]
    public async Task Default_requires_a_person_uid()
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            TestUser.Member().Claims().Where(c => c.Type != BccClaims.PersonUid), "Test"));

        (await Authorization.AuthorizeAsync(principal, null, await DefaultPolicy())).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task GlobalAdmin_admits_only_global_admins()
    {
        (await Passes(TestUser.Global(), AuthPolicies.GlobalAdmin)).ShouldBeTrue();
        (await Passes(TestUser.LocalAdmin(Copenhagen), AuthPolicies.GlobalAdmin)).ShouldBeFalse();
        (await Passes(TestUser.Member(), AuthPolicies.GlobalAdmin)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(Copenhagen, true)]
    [InlineData(Oslo, true)]
    public async Task Global_admins_are_admins_and_readers_of_every_tenant(long tenantId, bool expected)
    {
        var user = TestUser.Global(churchId: Copenhagen);

        (await Passes(user, AuthPolicies.TenantAdmin, tenantId)).ShouldBe(expected);
        (await Passes(user, AuthPolicies.TenantReader, tenantId)).ShouldBe(expected);
    }

    [Fact]
    public async Task Local_admins_administer_their_tenant_whatever_their_church()
    {
        var user = TestUser.LocalAdmin(Oslo, churchId: Copenhagen);

        (await Passes(user, AuthPolicies.TenantAdmin, Oslo)).ShouldBeTrue();
        (await Passes(user, AuthPolicies.TenantReader, Oslo)).ShouldBeTrue();
        // Their own church: a reader, not an admin
        (await Passes(user, AuthPolicies.TenantAdmin, Copenhagen)).ShouldBeFalse();
        (await Passes(user, AuthPolicies.TenantReader, Copenhagen)).ShouldBeTrue();
    }

    [Fact]
    public async Task Members_read_only_their_own_church()
    {
        var user = TestUser.Member(Copenhagen);

        (await Passes(user, AuthPolicies.TenantReader, Copenhagen)).ShouldBeTrue();
        (await Passes(user, AuthPolicies.TenantReader, Oslo)).ShouldBeFalse();
        (await Passes(user, AuthPolicies.TenantAdmin, Copenhagen)).ShouldBeFalse();
    }

    [Fact]
    public async Task Members_without_a_church_read_no_tenant()
    {
        (await Passes(TestUser.Member(churchId: null), AuthPolicies.TenantReader, Copenhagen)).ShouldBeFalse();
    }

    [Fact]
    public async Task Tenant_policies_fail_without_a_tenant()
    {
        (await Passes(TestUser.Global(), AuthPolicies.TenantAdmin)).ShouldBeFalse();
    }
}
