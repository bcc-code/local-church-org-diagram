namespace Backend.Features.GroupMembershipListing;

/// <summary>
/// Leaders first: "Leder", then "Ansvarlig", then other titles alphabetically, then members without a title; by name
/// within each. Case-insensitive, by code point (as the Flask backend did, so æ, ø and å sort after z).
/// </summary>
public static class MemberOrdering
{
    public static IEnumerable<GroupMember> Sort(IEnumerable<GroupMember> members) =>
        members
            .Select(m => (Member: m, Key: SortKey(m)))
            .OrderBy(x => x.Key.Rank)
            .ThenBy(x => x.Key.Title, StringComparer.Ordinal)
            .ThenBy(x => x.Key.Name, StringComparer.Ordinal)
            .Select(x => x.Member);

    private static (int Rank, string Title, string Name) SortKey(GroupMember member)
    {
        var title = (member.Title ?? "").Trim().ToLowerInvariant();
        var name = member.Name.Trim().ToLowerInvariant();
        return title switch
        {
            "leder" => (0, "", name),
            "ansvarlig" => (1, "", name),
            "" => (3, "", name),
            _ => (2, title, name),
        };
    }
}
