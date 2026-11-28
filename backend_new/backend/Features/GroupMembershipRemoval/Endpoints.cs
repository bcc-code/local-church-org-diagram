using System.Security.Claims;
using Backend.Api;
using Backend.Auth;
using Backend.Groups;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Features.GroupMembershipRemoval;

public record RemoveGroupMember(string? GroupId, string? PersonUid) : IGroupMembershipRequest
{
    public class Validator : AbstractValidator<RemoveGroupMember>
    {
        public Validator() => this.AddMembershipKeyRules();
    }
}

/// <summary>Removes a person from a group, for the group's admins.</summary>
public static class Endpoints
{
    public static Task<(GroupStream?, MembershipView?)> LoadAsync(
        RemoveGroupMember command, IQuerySession session, ClaimsPrincipal user, IAuthorizationService authorization,
        CancellationToken ct) =>
        GroupMembershipRequests.LoadAsync(command, session, user, authorization, ct);

    // An unknown group has no members either: one message for both, as the Flask backend's single DELETE
    public static ProblemDetails Validate(GroupStream? group, MembershipView? membership) =>
        group is null || membership is null
            ? new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = GroupMembershipRequests.MemberNotFound }
            : WolverineContinue.NoProblems;

    [ApiNotFound]
    [WolverineDelete("/group-membership", OperationId = "RemoveGroupMember")]
    public static (ApiSuccess, AppendGroupEvents) Delete(
        [FromBody] RemoveGroupMember command, GroupStream group, ClaimsPrincipal user)
    {
        var (_, personUid) = command.Key();
        var removed = new MembershipRemoved(group.Id, personUid, user.GetPersonUid()!.Value, DateTimeOffset.UtcNow);
        return (ApiSuccess.Instance, new AppendGroupEvents(group.Append(removed)));
    }
}
