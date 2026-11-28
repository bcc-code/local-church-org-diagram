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

namespace Backend.Features.GroupMembershipEditing;

/// <param name="Title">The new title, as entered (an empty string clears it); null or missing leaves it.</param>
/// <param name="Link">The new link, as <paramref name="Title"/>.</param>
public record UpdateGroupMember(string? GroupId, string? PersonUid, string? Title, string? Link)
    : IGroupMembershipRequest
{
    public class Validator : AbstractValidator<UpdateGroupMember>
    {
        public Validator() => this.AddMembershipKeyRules();
    }
}

/// <summary>Sets a member's title and/or link in a group, for the group's admins.</summary>
public static class Endpoints
{
    public static Task<(GroupStream?, MembershipView?)> LoadAsync(
        UpdateGroupMember command, IQuerySession session, ClaimsPrincipal user, IAuthorizationService authorization,
        CancellationToken ct) =>
        GroupMembershipRequests.LoadAsync(command, session, user, authorization, ct);

    public static ProblemDetails Validate(GroupStream? group, MembershipView? membership) =>
        group is null || membership is null
            ? new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = GroupMembershipRequests.MemberNotFound }
            : WolverineContinue.NoProblems;

    /// <summary>
    /// 304 when the request sets neither field, as the Flask backend; otherwise 200, with an event for each field that
    /// changes (none when both already have the values sent).
    /// </summary>
    [ApiNotFound]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [WolverinePut("/group-membership", OperationId = "UpdateGroupMember")]
    public static (Results<Ok<ApiSuccess>, StatusCodeHttpResult>, AppendGroupEvents) Put(
        UpdateGroupMember command, GroupStream group, MembershipView membership, ClaimsPrincipal user)
    {
        if (command.Title is null && command.Link is null)
            return (TypedResults.StatusCode(StatusCodes.Status304NotModified), new AppendGroupEvents([]));

        var actor = user.GetPersonUid()!.Value;
        var now = DateTimeOffset.UtcNow;
        var events = new List<object>();
        if (command.Title is { } title && title != membership.Title)
            events.Add(new MembershipTitleChanged(group.Id, membership.PersonUid, title, actor, now));
        if (command.Link is { } link && link != membership.Link)
            events.Add(new MembershipLinkChanged(group.Id, membership.PersonUid, link, actor, now));

        return (TypedResults.Ok(ApiSuccess.Instance), new AppendGroupEvents(group.Append(events)));
    }
}
