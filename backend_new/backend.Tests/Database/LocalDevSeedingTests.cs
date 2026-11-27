using Backend.Auth;
using Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Backend.Tests.Database;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.Name)]
public class LocalDevSeedingTests(DatabaseBackendFactory factory)
{
    [DatabaseFact]
    public async Task Seeding_makes_the_dev_user_a_Global_Admin_through_the_Admins_table()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seeding runs on every migration: idempotent
        await LocalDev.SeedAsync(db);
        await LocalDev.SeedAsync(db);

        (await db.Admins.CountAsync(a => a.PersonUid == LocalDev.User.PersonUid)).ShouldBe(1);
        var transformed = await new RoleClaimsTransformation(db).TransformAsync(LocalDev.Principal());
        transformed.IsGlobalAdmin().ShouldBeTrue();

        db.Admins.RemoveRange(db.Admins.Where(a => a.PersonUid == LocalDev.User.PersonUid));
        await db.SaveChangesAsync();
    }
}
