using System.Security.Cryptography;
using System.Text;

namespace Backend.Groups;

/// <summary>
/// Deterministic IDs (name-based UUIDv5, RFC 9562) for groups and memberships: the same input always gives the same
/// Guid, so the memberships importer finds a group's stream from the legacy <c>group_membership.group_id</c> alone.
/// </summary>
public static class GroupIds
{
    private static readonly Guid Namespace = new("6f1c2b0e-4d1a-4b8e-9a51-3c7e0d2f8a64");

    /// <summary>The stream ID of the group with this legacy <c>groups.id</c>.</summary>
    public static Guid FromLegacyId(long legacyGroupId) => Create($"group:{legacyGroupId}");

    /// <summary>The ID of a person's membership in a group (its <see cref="MembershipView"/>).</summary>
    public static Guid Membership(Guid groupId, Guid personUid) => Create($"membership:{groupId:N}:{personUid:N}");

    private static Guid Create(string name)
    {
        Span<byte> input = stackalloc byte[16 + Encoding.UTF8.GetByteCount(name)];
        Namespace.TryWriteBytes(input, bigEndian: true, out _);
        Encoding.UTF8.GetBytes(name, input[16..]);

        Span<byte> hash = stackalloc byte[SHA1.HashSizeInBytes];
        SHA1.HashData(input, hash);

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC variant
        return new Guid(bytes, bigEndian: true);
    }
}
