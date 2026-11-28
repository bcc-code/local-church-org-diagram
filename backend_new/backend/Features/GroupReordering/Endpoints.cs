using System.Security.Claims;
using Backend.Api;
using Backend.Auth;
using Backend.Groups;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Features.GroupReordering;

/// <param name="GroupId">Required, a group ID.</param>
/// <param name="SortOrder">Required, a 32-bit integer (wider in JSON, so an out-of-range one gets its message).</param>
public record GroupSortOrderUpdate(string? GroupId, long? SortOrder);

/// <param name="Updates">Sibling groups (the same parent and tenant) with their new sort orders.</param>
public record ReorderGroups(IReadOnlyList<GroupSortOrderUpdate?>? Updates)
{
    public const string NoUpdates = "No updates provided";
    public const string EachRequiresBoth = "Each update requires group_id and sort_order";
    public const string InvalidGroupId = "Invalid group_id";
    public const string InvalidSortOrder = "Invalid sort_order";
    public const string DuplicateGroupId = "Duplicate group_id in updates";

    /// <summary>The updates' group IDs and sort orders; only after validation.</summary>
    public IReadOnlyList<(Guid GroupId, int SortOrder)> Parsed() =>
        [.. Updates!.Select(u => (Guid.Parse(u!.GroupId!), (int)u.SortOrder!.Value))];

    public class Validator : AbstractValidator<ReorderGroups>
    {
        public Validator()
        {
            RuleFor(x => x.Updates).Cascade(CascadeMode.Stop)
                .Must(u => u is { Count: > 0 }).WithMessage(NoUpdates)
                .Must(u => u!.All(x => x is { GroupId.Length: > 0, SortOrder: not null })).WithMessage(EachRequiresBoth)
                .Must(u => u!.All(x => Guid.TryParse(x!.GroupId, out _))).WithMessage(InvalidGroupId)
                .Must(u => u!.All(x => x!.SortOrder is >= int.MinValue and <= int.MaxValue)).WithMessage(InvalidSortOrder)
                .Must(u => u!.Select(x => Guid.Parse(x!.GroupId!)).Distinct().Count() == u!.Count)
                .WithMessage(DuplicateGroupId)
                .OverridePropertyName("updates");
        }
    }
}

/// <summary>
/// Sets the sort order of sibling groups (the admin UI's move left/right, which renumbers all siblings), for their
/// admins. All or nothing: one transaction, each group's stream checked for concurrent changes.
/// </summary>
public static class Endpoints
{
    public const string MissingGroupIds = "missing_group_ids";
    public const string NotSiblings = "The groups must be siblings";

    /// <summary>The groups the user may administer, of those in the request; the others are left out (unknown).</summary>
    public static async Task<IReadOnlyList<GroupStream>> LoadAsync(
        ReorderGroups command, IQuerySession session, ClaimsPrincipal user, IAuthorizationService authorization,
        CancellationToken ct)
    {
        var groups = new List<GroupStream>();
        foreach (var (groupId, _) in command.Parsed())
        {
            if (await GroupStream.LoadAsync(session, groupId, ct) is { } group &&
                await authorization.IsAllowedAsync(user, group.Group, AuthPolicies.TenantAdmin))
                groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// 404 with the IDs of the groups that are unknown or not the user's to change (as the Flask backend's
    /// tenant-filtered update); 400 when the groups don't share one parent in one tenant.
    /// </summary>
    public static ProblemDetails Validate(ReorderGroups command, IReadOnlyList<GroupStream> groups)
    {
        var found = groups.Select(g => g.Id).ToHashSet();
        var missing = command.Parsed().Select(u => u.GroupId).Where(id => !found.Contains(id)).Order().ToList();
        if (missing.Count > 0)
        {
            var notFound = new ApiErrorProblem
            {
                Status = StatusCodes.Status404NotFound,
                Detail = $"Group(s) {string.Join(", ", missing)} not found or not updatable",
            };
            notFound.Fields[MissingGroupIds] = missing;
            return notFound;
        }

        return groups.Select(g => (g.Group.ParentId, g.Group.TenantId)).Distinct().Count() > 1
            ? new ProblemDetails { Status = StatusCodes.Status400BadRequest, Detail = NotSiblings }
            : WolverineContinue.NoProblems;
    }

    /// <summary>A <see cref="GroupSortOrderChanged"/> for each group whose sort order changes.</summary>
    [ApiNotFound]
    [WolverinePut("/groups/sort-order", OperationId = "ReorderGroups")]
    public static (ApiSuccess, AppendGroupEvents) Put(
        ReorderGroups command, IReadOnlyList<GroupStream> groups, ClaimsPrincipal user)
    {
        var actor = user.GetPersonUid()!.Value;
        var now = DateTimeOffset.UtcNow;
        var groupsById = groups.ToDictionary(g => g.Id);
        var changes = command.Parsed()
            .Select(u => (Group: groupsById[u.GroupId], u.SortOrder))
            .Where(u => u.Group.Group.SortOrder != u.SortOrder)
            .Select(u => u.Group.Append(new GroupSortOrderChanged(u.Group.Id, u.SortOrder, actor, now)));

        return (ApiSuccess.Instance, new AppendGroupEvents([.. changes]));
    }
}
