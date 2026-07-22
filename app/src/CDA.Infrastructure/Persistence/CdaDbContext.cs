using CDA.Domain.Users;
using CDA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Persistence;

/// <summary>
/// The application's single EF Core context. Entity configurations are discovered from this
/// assembly, so each aggregate gets its own IEntityTypeConfiguration rather than
/// accumulating in OnModelCreating.
/// </summary>
public sealed class CdaDbContext(DbContextOptions<CdaDbContext> options)
    : IdentityDbContext<CdaUser, CdaRole, Guid>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The server default is utf8mb4_general_ci, a legacy byte-order collation that
        // compares non-Latin text poorly. This is a multilingual platform, so the model
        // pins the best Unicode collation this MariaDB build offers (11.4 predates the
        // uca1400 family). See Documentation/devplan.md section 2.1.
        modelBuilder.UseCollation(DatabaseCollation);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CdaDbContext).Assembly);
    }

    /// <summary>Collation applied to the model; see <see cref="OnModelCreating"/>.</summary>
    public const string DatabaseCollation = "utf8mb4_unicode_520_ci";
}
