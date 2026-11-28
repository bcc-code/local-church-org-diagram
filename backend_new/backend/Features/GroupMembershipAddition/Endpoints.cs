using System.Security.Claims;
using Backend.Api;
using Backend.Auth;
using Backend.Groups;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Features.GroupMembershipAddition;

/// <param name="Title">Optional, stored as entered.</param>
public record AddGroupMember(string? GroupId, string? PersonUid, string? Title) : IGroupMembershipRequest
{
    public class Validator : AbstractValidator<AddGroupMember>
    {
        public Validator() => this.AddMembershipKeyRules();
    }
}

/// <summary>
/// Adds a person to a group, for the group's admins. Any person UID is accepted (persons of other churches too): the
/// admin UI only adds persons found by the person search.
/// </summary>
public static class Endpoints
{
    public const string GroupNotFound = "Group not found";
    public const string AlreadyMember = "Member already in group";

    public static Task<(GroupStream?, MembershipView?)> LoadAsync(
        AddGroupMember command, IQuerySession session, ClaimsPrincipal user, IAuthorizationService authorization,
        CancellationToken ct) =>
        GroupMembershipRequests.LoadAsync(command, session, user, authorization, ct);

    public static ProblemDetails Validate(GroupStream? group, MembershipView? membership) =>
        group is null
            ? new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = GroupNotFound }
            : membership is not null
                ? new ProblemDetails { Status = StatusCodes.Status409Conflict, Detail = AlreadyMember }
                : WolverineContinue.NoProblems;

    [ApiNotFound]
    [WolverinePost("/group-membership", OperationId = "AddGroupMember")]
    public static (Created<ApiSuccess>, AppendGroupEvents) Post(
        AddGroupMember command, GroupStream group, ClaimsPrincipal user)
    {
        var (_, personUid) = command.Key();
        var added = new MembershipAdded(group.Id, personUid, command.Title, group.Group.TenantId,
            user.GetPersonUid()!.Value, DateTimeOffset.UtcNow);
        return (TypedResults.Created((string?)null, ApiSuccess.Instance), new AppendGroupEvents(group.Append(added)));
    }
}
