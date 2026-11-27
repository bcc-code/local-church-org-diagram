using System.Security.Claims;
using Backend.Auth;
using Backend.CoreApi;
using Backend.Groups;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Features.TitleSearch;

/// <summary>A person holding a title in at least one group.</summary>
public record TitleHolder(Guid PersonUid, string Name, string? ProfilePicture);

/// <summary>
/// The titles in use in the user's church, and who holds one. Titles are compared trimmed: memberships store them as
/// entered.
/// </summary>
public static class TitleSearchEndpoint
{
    // Runs before both endpoints
    public static Task<ProblemDetails> Before(ClaimsPrincipal user, IAuthorizationService authorization) =>
        authorization.AuthorizeOwnChurchAsync(user);

    [WolverineGet("/titles", OperationId = "ListTitles")]
    public static async Task<IReadOnlyList<string>> ListTitles(
        ClaimsPrincipal user, IQuerySession session, CancellationToken ct)
    {
        var tenantId = user.OwnChurchId();
        var titles = await session.Query<MembershipView>()
            .Where(m => m.TenantId == tenantId && m.Title != null)
            .Select(m => m.Title!)
            .Distinct()
            .ToListAsync(ct);
        return NormalizeTitles(titles);
    }

    [WolverineGet("/titles/{title}/persons", OperationId = "ListTitleHolders")]
    public static async Task<IReadOnlyList<TitleHolder>> ListTitleHolders(
        string title, ClaimsPrincipal user, IQuerySession session, IPersonDirectory persons, CancellationToken ct)
    {
        var tenantId = user.OwnChurchId();
        var trimmed = title.Trim();
        if (trimmed.Length == 0)
            return [];

        var personUids = await session.Query<MembershipView>()
            .Where(m => m.TenantId == tenantId && m.Title != null && m.Title.Trim() == trimmed)
            .Select(m => m.PersonUid)
            .Distinct()
            .ToListAsync(ct);
        if (personUids.Count == 0)
            return [];

        return ToHolders(await persons.GetByUidsAsync(personUids, ct));
    }

    /// <summary>Trimmed, without blanks and duplicates, by code point (as the Flask backend did).</summary>
    public static IReadOnlyList<string> NormalizeTitles(IEnumerable<string?> titles) =>
    [
        .. titles
            .Select(t => t?.Trim())
            .OfType<string>()
            .Where(t => t.Length > 0)
            .Distinct()
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// By name, case-insensitive. Persons the Core API doesn't know are left out: without a name there's nothing to
    /// show.
    /// </summary>
    public static IReadOnlyList<TitleHolder> ToHolders(IEnumerable<PersonSummary> persons) =>
    [
        .. persons
            .OrderBy(p => p.DisplayName.ToLowerInvariant(), StringComparer.Ordinal)
            .Select(p => new TitleHolder(p.Uid, p.DisplayName, p.ProfilePicture)),
    ];
}
