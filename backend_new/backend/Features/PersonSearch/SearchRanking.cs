using Backend.CoreApi;

namespace Backend.Features.PersonSearch;

/// <summary>
/// Ranks Core API search results for the autocomplete, as the Flask backend did: an exact (case-insensitive) name
/// match first, then names containing more of the query's words, earlier words and earlier positions counting more.
/// </summary>
public static class SearchRanking
{
    public const int ExactMatch = 1000;
    public const int PartialMatch = 500;

    /// <summary>The <paramref name="limit"/> best matches, best first; equal scores keep the Core API's order.</summary>
    public static IReadOnlyList<PersonSummary> Rank(IEnumerable<PersonSummary> persons, string query, int limit) =>
        [.. persons.OrderByDescending(p => Score(p.DisplayName, query)).Take(limit)];

    public static int Score(string name, string query)
    {
        var nameLower = name.ToLowerInvariant();
        var queryLower = query.ToLowerInvariant();
        if (nameLower == queryLower)
            return ExactMatch;

        var score = PartialMatch;
        // Split on single spaces like Python's str.split(" "): an empty word (two spaces) matches at position 0
        var terms = queryLower.Split(' ');
        for (var i = 0; i < terms.Length; i++)
        {
            var index = nameLower.IndexOf(terms[i], StringComparison.Ordinal);
            if (index < 0)
                continue;
            score += 100 / (i + 1);
            score += 50 / (index + 1);
        }

        return score;
    }
}
