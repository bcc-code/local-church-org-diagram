using Backend.CoreApi;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Backend.Features.PersonSearch;

/// <summary>Persons by name, for adding members (not limited to the user's church).</summary>
/// <param name="Q">At least <see cref="MinimumLength"/> characters, not counting surrounding whitespace.</param>
public record SearchPersons([FromQuery(Name = "q")] string? Q)
{
    public const int MinimumLength = 3;

    public class Validator : AbstractValidator<SearchPersons>
    {
        public Validator() =>
            RuleFor(x => x.Q).Cascade(CascadeMode.Stop)
                .Must(q => !string.IsNullOrWhiteSpace(q)).WithMessage("Query parameter 'q' is required")
                .Must(q => q!.Trim().Length >= MinimumLength)
                .WithMessage($"Search query must be at least {MinimumLength} characters")
                // Keyed like the query parameter, not the property
                .OverridePropertyName("q");
    }
}

public record PersonSearchResult(Guid PersonUid, string Name, string? ProfilePicture);

public static class PersonSearchEndpoint
{
    /// <summary>Core API matches ranked by <see cref="SearchRanking"/>.</summary>
    public const int Candidates = 50;

    public const int Results = 5;

    [WolverineGet("/persons/search", OperationId = "SearchPersons")]
    public static async Task<IReadOnlyList<PersonSearchResult>> Get(
        [FromQuery] SearchPersons query, IPersonDirectory persons, CancellationToken ct)
    {
        var text = query.Q!.Trim();
        var found = await persons.SearchAsync(text, Candidates, ct);
        return ToResults(found, text);
    }

    public static IReadOnlyList<PersonSearchResult> ToResults(IEnumerable<PersonSummary> found, string query) =>
    [
        .. SearchRanking.Rank(found, query, Results)
            .Select(p => new PersonSearchResult(p.Uid, p.DisplayName, p.ProfilePicture)),
    ];
}
