namespace Backend.Groups;

// Events on a group's stream (stream ID: GroupIds.FromLegacyId). The *Imported events come only from the one-off
// import of the Flask backend's tables (backend/LegacyImport/), never from the application itself, and carry the
// legacy IDs for reference.

/// <summary>A group imported from the legacy <c>groups</c> table. Starts the group's stream.</summary>
public record GroupImported(
    Guid GroupId,
    long LegacyId,
    string Name,
    Guid? ParentId,
    long? LegacyParentId,
    long TenantId,
    int? SortOrder,
    DateTimeOffset CreatedAt);

/// <summary>A membership imported from the legacy <c>group_membership</c> table, appended to its group's stream.</summary>
public record MembershipImported(
    Guid GroupId,
    long LegacyId,
    long LegacyGroupId,
    Guid PersonUid,
    string? Title,
    string? Link,
    long TenantId,
    DateTimeOffset CreatedAt);
