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

// Events the admin endpoints (backend/Features/) append to a group's stream. Each carries the signed-in admin's person
// UID (ActorPersonUid) and when it happened (OccurredAt).

/// <summary>A person was added to the group. <paramref name="TenantId"/> is the group's.</summary>
public record MembershipAdded(
    Guid GroupId,
    Guid PersonUid,
    string? Title,
    long TenantId,
    Guid ActorPersonUid,
    DateTimeOffset OccurredAt);

/// <summary>A person was removed from the group.</summary>
public record MembershipRemoved(
    Guid GroupId,
    Guid PersonUid,
    Guid ActorPersonUid,
    DateTimeOffset OccurredAt);

/// <summary>A member's title in the group was set, as entered; an empty string clears it.</summary>
public record MembershipTitleChanged(
    Guid GroupId,
    Guid PersonUid,
    string Title,
    Guid ActorPersonUid,
    DateTimeOffset OccurredAt);

/// <summary>A member's link in the group was set, as entered; an empty string clears it.</summary>
public record MembershipLinkChanged(
    Guid GroupId,
    Guid PersonUid,
    string Link,
    Guid ActorPersonUid,
    DateTimeOffset OccurredAt);

/// <summary>The group's position among its siblings was set.</summary>
public record GroupSortOrderChanged(
    Guid GroupId,
    int SortOrder,
    Guid ActorPersonUid,
    DateTimeOffset OccurredAt);
