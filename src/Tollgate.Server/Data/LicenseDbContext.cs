using Microsoft.EntityFrameworkCore;

namespace Tollgate.Server.Data
{
    public class LicenseDbContext : DbContext
    {
        public LicenseDbContext(DbContextOptions<LicenseDbContext> options)
            : base(options) { }

        /// <summary>All issued license keys.</summary>
        public DbSet<LicenseKeyEntity> LicenseKeys => Set<LicenseKeyEntity>();

        /// <summary>Registered applications (multi-tenant support).</summary>
        public DbSet<AppEntity> Apps => Set<AppEntity>();

        /// <summary>Persistent trail of admin operations (queryable history).</summary>
        public DbSet<AdminAuditEntity> AdminAudit => Set<AdminAuditEntity>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<LicenseKeyEntity>(e =>
            {
                e.HasIndex(x => new { x.AppId, x.LicenseKey }).IsUnique();
                e.HasIndex(x => x.AppId);
                e.Property(x => x.Tier).HasConversion<string>();
                e.Property(x => x.Features).HasDefaultValue("");
            });

            mb.Entity<AppEntity>(e =>
            {
                e.HasKey(x => x.AppId);
                e.HasMany(x => x.LicenseKeys)
                 .WithOne()
                 .HasForeignKey(x => x.AppId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            mb.Entity<AdminAuditEntity>(e =>
            {
                e.HasIndex(x => x.Timestamp);
                e.HasIndex(x => x.AppId);
            });
        }
    }
}
