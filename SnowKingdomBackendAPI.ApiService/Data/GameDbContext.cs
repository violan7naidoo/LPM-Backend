using Microsoft.EntityFrameworkCore;
using SnowKingdomBackendAPI.ApiService.Data.Entities;

namespace SnowKingdomBackendAPI.ApiService.Data;

/// <summary>
/// Entity Framework database context for slot game data.
/// Tracks all sessions and spin transactions for auditability and analysis.
/// </summary>
public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// All game sessions.
    /// </summary>
    public DbSet<GameSessionEntity> GameSessions { get; set; }

    /// <summary>
    /// All spin transactions (historical record of all spins).
    /// </summary>
    public DbSet<SpinTransactionEntity> SpinTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure GameSessionEntity
        modelBuilder.Entity<GameSessionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasIndex(e => e.PlayerId);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.PlayerId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.OperatorId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Balance).IsRequired();
            entity.Property(e => e.FreeSpinsRemaining).IsRequired();
            entity.Property(e => e.LastWin).IsRequired();
            entity.Property(e => e.FreeSpinsTotalWin).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        // Configure SpinTransactionEntity
        modelBuilder.Entity<SpinTransactionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.SpinAt);
            entity.HasIndex(e => new { e.SessionId, e.SpinAt });
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.GameId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.BetAmount).IsRequired();
            entity.Property(e => e.WinAmount).IsRequired();
            entity.Property(e => e.TotalWin).IsRequired();
            entity.Property(e => e.FreeSpinsAwarded).IsRequired();
            entity.Property(e => e.IsFreeSpin).IsRequired();
            entity.Property(e => e.GridJson).IsRequired();
            entity.Property(e => e.WinningLinesJson).IsRequired();
            entity.Property(e => e.ScatterWinJson).IsRequired();
            entity.Property(e => e.SpinAt).IsRequired();
            
            // Optional: Add foreign key relationship
            entity.HasOne<GameSessionEntity>()
                .WithMany()
                .HasForeignKey(t => t.SessionId)
                .HasPrincipalKey(s => s.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

