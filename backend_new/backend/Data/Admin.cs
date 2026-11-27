namespace Backend.Data;

/// <summary>
/// An admin assignment. <see cref="TenantId"/> set → Local Admin of that tenant (a BCC church ID, the groups'
/// tenant), whatever the person's own church. <see cref="TenantId"/> null → Global Admin of every tenant.
/// </summary>
public class Admin
{
    public int Id { get; set; }

    /// <summary>The person's BCC UID: the sign-on personUid claim, the Core API's person UID.</summary>
    public Guid PersonUid { get; set; }

    /// <summary>Who the row is for, so it can be read without looking the person up.</summary>
    public required string Name { get; set; }

    public long? TenantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
