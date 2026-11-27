using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>
/// Relational data in the "public" schema, managed by EF Core migrations (<c>dotnet ef migrations add</c>).
/// Wolverine maps its envelope tables into the model (excluded from migrations), so changes made through this
/// context and outgoing messages commit in one transaction.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
