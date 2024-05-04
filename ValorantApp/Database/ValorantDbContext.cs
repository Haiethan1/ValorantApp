using Microsoft.EntityFrameworkCore;
using ValorantApp.Database.Tables;

namespace ValorantApp.Database
{
    public class ValorantDbContext : DbContext
    {
        public ValorantDbContext(DbContextOptions<ValorantDbContext> options) : base(options)
        {
        }

        public DbSet<MatchStats> MatchStats { get; set; }

        public DbSet<Matches> Matches { get; set; }

        public DbSet<ValorantUsers> ValorantUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MatchStats>()
                .HasKey(m => new { m.Match_id, m.Val_puuid});
            modelBuilder.Entity<Matches>()
                .HasKey(m => new { m.Match_Id });
            modelBuilder.Entity<ValorantUsers>()
                .HasKey(m => new { m.Val_puuid });

            // Add other configurations as needed...

            base.OnModelCreating(modelBuilder);
        }
    }
}
