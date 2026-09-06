using Microsoft.EntityFrameworkCore;

namespace Tollgate.Server.Data
{
    public class LicenseDbContext : DbContext
    {
        public LicenseDbContext(DbContextOptions<LicenseDbContext> options)
            : base(options) { }

        public DbSet<LicenseKeyEntity> LicenseKeys => Set<LicenseKeyEntity>();
        public DbSet<AppEntity>        Apps        => Set<AppEntity>();

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
        }
    }
}
