using Backend.Groups;
using Backend.LegacyImport;
using Shouldly;

namespace Backend.Tests.LegacyImport;

public class GroupIdsTests
{
    // Expected values from Python: uuid.uuid5(uuid.UUID("6f1c2b0e-4d1a-4b8e-9a51-3c7e0d2f8a64"), "group:1") etc.
    [Fact]
    public void FromLegacyId_IsUuidV5()
    {
        GroupIds.FromLegacyId(1).ShouldBe(new Guid("e535c43d-32b6-50a7-b950-9f1d6bfba1d0"));
        GroupIds.FromLegacyId(2).ShouldNotBe(GroupIds.FromLegacyId(1));
    }

    [Fact]
    public void Membership_IsUuidV5OfGroupAndPerson()
    {
        var person = new Guid("11111111-1111-1111-1111-111111111111");

        GroupIds.Membership(GroupIds.FromLegacyId(1), person)
            .ShouldBe(new Guid("65211d40-d249-5a06-a06f-8e4f3cb440ab"));
    }
}

public class LegacyMappingTests
{
    private static readonly DateTimeOffset CreatedAt = new(2024, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Person = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Groups_MapParentToDerivedId()
    {
        var events = LegacyMapping.ToEvents([
            new LegacyGroupRow(1, CreatedAt, "Root", null, 7, 0),
            new LegacyGroupRow(2, CreatedAt, "Child", 1, 7, null),
        ]);

        events.ShouldBe([
            new GroupImported(GroupIds.FromLegacyId(1), 1, "Root", null, null, 7, 0, CreatedAt),
            new GroupImported(GroupIds.FromLegacyId(2), 2, "Child", GroupIds.FromLegacyId(1), 1, 7, null, CreatedAt),
        ]);
    }

    [Fact]
    public void Memberships_MapToGroupStream()
    {
        var events = LegacyMapping.ToEvents([new LegacyMembershipRow(5, CreatedAt, 2, Person, "Leder", "https://x", 7)]);

        events.ShouldBe([new MembershipImported(GroupIds.FromLegacyId(2), 5, 2, Person, "Leder", "https://x", 7, CreatedAt)]);
    }

    [Fact]
    public void NullTenants_FailWithTheirIds()
    {
        var groups = Should.Throw<LegacyImportException>(() => LegacyMapping.ToEvents([
            new LegacyGroupRow(1, CreatedAt, "Root", null, null, null),
            new LegacyGroupRow(2, CreatedAt, "Child", 1, 7, null),
            new LegacyGroupRow(3, CreatedAt, "Child", 1, null, null),
        ]));
        groups.Message.ShouldBe("2 groups row(s) have no tenant_id (id 1, 3)");

        var memberships = Should.Throw<LegacyImportException>(() =>
            LegacyMapping.ToEvents([new LegacyMembershipRow(9, CreatedAt, 1, Person, null, null, null)]));
        memberships.Message.ShouldBe("1 group_membership row(s) have no tenant_id (id 9)");
    }
}
