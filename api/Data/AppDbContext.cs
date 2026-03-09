using BeneditaApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BeneditaApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Voter> Voters => Set<Voter>();
    public DbSet<Vote>  Votes  => Set<Vote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Um eleitor só pode ter um voto
        modelBuilder.Entity<Voter>()
            .HasOne(v => v.Vote)
            .WithOne(v => v.Voter)
            .HasForeignKey<Vote>(v => v.VoterId);

        // FingerId único
        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.FingerId)
            .IsUnique();
    }
}
