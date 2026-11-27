using Backend.Admins;
using Shouldly;

namespace Backend.Tests.Admins;

public class AdminOutputTests
{
    private static readonly Guid Person = new("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private static (string Stdout, string Stderr) Write(AdminResult result, bool json)
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        AdminOutput.Write(result, json, stdout, stderr);
        return (stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Json_IsOneDocumentWithStatusAdminsAndWarnings()
    {
        var result = new AdminResult("added")
        {
            Admins = [new AdminRow(1, Person, "Kari's", AdminRow.Local, 5, CreatedAt)],
            Warnings = [new AdminWarning(AdminWarning.TenantHasNoGroups, "No group has tenant 5")],
        };

        var (stdout, stderr) = Write(result, json: true);

        stdout.ShouldBe("""
            {
              "status": "added",
              "admins": [
                {
                  "id": 1,
                  "personUid": "11111111-1111-1111-1111-111111111111",
                  "name": "Kari's",
                  "scope": "local",
                  "tenantId": 5,
                  "createdAt": "2026-11-27T12:00:00+00:00"
                }
              ],
              "warnings": [
                {
                  "code": "tenant_has_no_groups",
                  "message": "No group has tenant 5"
                }
              ]
            }

            """, StringCompareShould.IgnoreLineEndings);
        stderr.ShouldBe("Warning (tenant_has_no_groups): No group has tenant 5\n");
    }

    [Fact]
    public void Json_Error()
    {
        var (stdout, stderr) = Write(AdminResult.Failed(AdminErrors.InvalidInput, "Bad"), json: true);

        stdout.ShouldBe("""
            {
              "status": "error",
              "warnings": [],
              "error": {
                "code": "invalid_input",
                "message": "Bad"
              }
            }

            """, StringCompareShould.IgnoreLineEndings);
        stderr.ShouldBe("Error (invalid_input): Bad\n");
    }

    [Fact]
    public void Json_GlobalAdminHasNullTenant() =>
        Write(new AdminResult("would_add") { Admins = [new AdminRow(null, Person, "Kari", AdminRow.Global, null, null)] },
                json: true)
            .Stdout.ShouldContain("\"tenantId\": null");

    [Fact]
    public void Text_List()
    {
        var result = new AdminResult("ok")
        {
            Admins =
            [
                new AdminRow(1, Person, "Kari", AdminRow.Global, null, CreatedAt),
                new AdminRow(12, Person, "Kari", AdminRow.Local, 5, CreatedAt),
            ],
        };

        Write(result, json: false).Stdout.ShouldBe("""
            ID  SCOPE   TENANT  PERSON UID                            NAME  CREATED
            1   global          11111111-1111-1111-1111-111111111111  Kari  2026-11-27
            12  local   5       11111111-1111-1111-1111-111111111111  Kari  2026-11-27

            """, StringCompareShould.IgnoreLineEndings);
    }

    [Fact]
    public void Text_Search()
    {
        var result = new AdminResult("ok")
        {
            Persons = [new PersonRow(Person, "Kari", [new AdminRow(1, Person, "Kari", AdminRow.Local, 5, CreatedAt)])],
        };

        Write(result, json: false).Stdout.ShouldBe("""
            PERSON UID                            NAME  ADMIN
            11111111-1111-1111-1111-111111111111  Kari  tenant 5

            """, StringCompareShould.IgnoreLineEndings);
    }

    [Fact]
    public void Text_ConfirmationRequired_ListsWhatWouldBeRemoved()
    {
        var result = new AdminResult("error")
        {
            Admins = [new AdminRow(1, Person, "Kari", AdminRow.Global, null, CreatedAt)],
            Error = new AdminError(AdminErrors.ConfirmationRequired, "Run again with --yes"),
        };

        var (stdout, stderr) = Write(result, json: false);

        stdout.ShouldBe($"Would remove: Global Admin: Kari ({Person})\n");
        stderr.ShouldBe("Error (confirmation_required): Run again with --yes\n");
    }

    [Fact]
    public void Text_Added() =>
        Write(new AdminResult("added") { Admins = [new AdminRow(1, Person, "Kari", AdminRow.Local, 5, CreatedAt)] },
                json: false)
            .Stdout.ShouldBe($"Added: Local Admin of tenant 5: Kari ({Person})\n");
}
