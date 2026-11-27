using System.Security.Claims;
using Backend.Auth;
using Backend.Groups;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Features.MembershipTree;

/// <summary>A group in the org chart; the client builds the tree from <see cref="ParentGroupId"/>.</summary>
/// <param name="Label">The group's name.</param>
public record GroupNode(Guid GroupId, string Label, Guid? ParentGroupId, int MemberCount, int? SortOrder);

public static class Endpoints
{
    public static Task<ProblemDetails> Before(ClaimsPrincipal user, IAuthorizationService authorization) =>
        authorization.AuthorizeTenantAsync(user, user.GetChurchId()!.Value, AuthPolicies.TenantReader);

    [WolverineGet("/tree", OperationId = "ListGroups")]
    public static async Task<IReadOnlyList<GroupNode>> Get(
        ClaimsPrincipal user, IQuerySession session, CancellationToken ct)
    {
        var tenantId = user.GetChurchId()!.Value;
        var groups = await session.Query<GroupView>().Where(g => g.TenantId == tenantId).ToListAsync(ct);
        return ToNodes(groups);
    }

    /// <summary>
    /// By sort order (unset last, like Postgres), then creation. The chart sorts siblings by sort order with a stable
    /// sort, so this order breaks its ties; the Flask backend broke them by legacy ID, which creation follows.
    /// </summary>
    public static IReadOnlyList<GroupNode> ToNodes(IEnumerable<GroupView> groups) =>
    [
        .. groups
            .OrderBy(g => g.SortOrder is null)
            .ThenBy(g => g.SortOrder)
            .ThenBy(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .Select(g => new GroupNode(g.Id, g.Name, g.ParentId, g.MemberCount, g.SortOrder)),
    ];
}
