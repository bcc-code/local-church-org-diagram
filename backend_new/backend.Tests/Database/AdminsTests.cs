using System.Security.Claims;
using Backend.Auth;
using Backend.Data;
using Backend.Tests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace Backend.Tests.Database;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class AdminsTests(DatabaseBackendFactory factory)
{
    private const long Copenhagen = TestUser.Copenhagen;
    private const long Oslo = 1;

    private async Task AddAdminsAsync(params Admin[] admins)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Admins.AddRange(admins);
        await db.SaveChangesAsync();
    }

    private async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await new RoleClaimsTransformation(scope.ServiceProvider.GetRequiredService<AppDbContext>())
            .TransformAsync(principal);
    }

    private static ClaimsPrincipal SignedIn(Guid personUid) =>
        new(new ClaimsIdentity((TestUser.Member() with { PersonUid = personUid }).Claims(), "Test", BccClaims.Name, AppClaims.Role));

    [DatabaseFact]
    public async Task Transformation_adds_no_roles_without_Admins_rows()
    {
        var principal = SignedIn(Guid.NewGuid());

        var transformed = await TransformAsync(principal);

        transformed.ShouldBeSameAs(principal);
        transformed.IsGlobalAdmin().ShouldBeFalse();
        transformed.GetAdminTenantIds().ShouldBeEmpty();
    }

    [DatabaseFact]
    public async Task Transformation_adds_global_and_local_admin_roles()
    {
        var personUid = Guid.NewGuid();
        await AddAdminsAsync(
            new Admin { PersonUid = personUid, Name = "Test Global", TenantId = null },
            new Admin { PersonUid = personUid, Name = "Test Global", TenantId = Copenhagen },
            new Admin { PersonUid = personUid, Name = "Test Global", TenantId = Oslo },
            new Admin { PersonUid = Guid.NewGuid(), Name = "Someone else", TenantId = 3 });

        var transformed = await TransformAsync(SignedIn(personUid));

        transformed.IsGlobalAdmin().ShouldBeTrue();
        transformed.IsInRole(AppRoles.LocalAdmin).ShouldBeTrue();
        transformed.GetAdminTenantIds().ShouldBe([Copenhagen, Oslo], ignoreOrder: true);
    }

    [DatabaseFact]
    public async Task Transformation_runs_once_per_principal()
    {
        var personUid = Guid.NewGuid();
        await AddAdminsAsync(new Admin { PersonUid = personUid, Name = "Test Local", TenantId = Copenhagen });

        var once = await TransformAsync(SignedIn(personUid));
        var twice = await TransformAsync(once);

        twice.ShouldBeSameAs(once);
        twice.FindAll(AppClaims.AdminTenant).Count().ShouldBe(1);
    }

    [DatabaseFact]
    public async Task Transformation_skips_anonymous_users()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        (await TransformAsync(anonymous)).ShouldBeSameAs(anonymous);
    }

    [DatabaseFact]
    public async Task A_person_has_one_global_row()
    {
        var personUid = Guid.NewGuid();
        await AddAdminsAsync(new Admin { PersonUid = personUid, Name = "Twice Global" });

        var duplicate = await Should.ThrowAsync<DbUpdateException>(() =>
            AddAdminsAsync(new Admin { PersonUid = personUid, Name = "Twice Global" }));

        duplicate.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [DatabaseFact]
    public async Task A_person_has_one_row_per_tenant()
    {
        var personUid = Guid.NewGuid();
        await AddAdminsAsync(new Admin { PersonUid = personUid, Name = "Twice Local", TenantId = Copenhagen });

        var duplicate = await Should.ThrowAsync<DbUpdateException>(() =>
            AddAdminsAsync(new Admin { PersonUid = personUid, Name = "Twice Local", TenantId = Copenhagen }));

        duplicate.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [DatabaseFact]
    public async Task Rows_need_a_name_and_a_positive_tenant()
    {
        (await Should.ThrowAsync<DbUpdateException>(() =>
                AddAdminsAsync(new Admin { PersonUid = Guid.NewGuid(), Name = "  ", TenantId = Copenhagen })))
            .InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("CK_Admins_Name");
        (await Should.ThrowAsync<DbUpdateException>(() =>
                AddAdminsAsync(new Admin { PersonUid = Guid.NewGuid(), Name = "Zero", TenantId = 0 })))
            .InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("CK_Admins_TenantId");
    }

    [DatabaseFact]
    public async Task CreatedAt_defaults_to_now()
    {
        var personUid = Guid.NewGuid();
        await AddAdminsAsync(new Admin { PersonUid = personUid, Name = "Timestamped", TenantId = Copenhagen });

        await using var scope = factory.Services.CreateAsyncScope();
        var admin = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Admins.SingleAsync(a => a.PersonUid == personUid);
        admin.CreatedAt.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }
}
