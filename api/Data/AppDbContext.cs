using BeneditaApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BeneditaApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Voter>  Voters   => Set<Voter>();
    public DbSet<Vote>   Votes    => Set<Vote>();
    public DbSet<Entity> Entities => Set<Entity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Um eleitor tem no máximo um voto
        modelBuilder.Entity<Voter>()
            .HasOne(v => v.Vote)
            .WithOne(v => v.Voter)
            .HasForeignKey<Vote>(v => v.VoterId);

        // Uma entidade pode ter muitos votos
        modelBuilder.Entity<Vote>()
            .HasOne(v => v.Entity)
            .WithMany(e => e.Votes)
            .HasForeignKey(v => v.EntityId);

        // FingerId único (permite múltiplos NULLs no MySQL)
        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.FingerId)
            .IsUnique();

        // BI único
        modelBuilder.Entity<Voter>()
            .HasIndex(v => v.BI)
            .IsUnique();

        // Sigla da entidade única
        modelBuilder.Entity<Entity>()
            .HasIndex(e => e.Acronym)
            .IsUnique();
    }
}
