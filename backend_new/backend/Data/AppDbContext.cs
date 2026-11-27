using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>
/// Relational data in the "public" schema, managed by EF Core migrations (<c>dotnet ef migrations add</c>).
/// Wolverine maps its envelope tables into the model (excluded from migrations), so changes made through this
/// context and outgoing messages commit in one transaction.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Admin> Admins => Set<Admin>();

    /// <summary>The Data Protection key ring (session cookies, sign-in state), so sessions survive restarts and deploys.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Admin>(entity =>
        {
            // One global row (TenantId null) and one row per tenant per person
            entity.HasIndex(a => new { a.PersonUid, a.TenantId }).IsUnique().AreNullsDistinct(false);
            entity.Property(a => a.Name).HasMaxLength(200);
            entity.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Admins_TenantId", "\"TenantId\" > 0");
                t.HasCheckConstraint("CK_Admins_Name", "length(btrim(\"Name\")) > 0");
            });
        });
    }
}
