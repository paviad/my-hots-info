using Microsoft.EntityFrameworkCore;
using MyReplayLibrary.Data.Models;

namespace MyReplayLibrary.Data;

public class ReplayDbContext(DbContextOptions<ReplayDbContext> opts) : DbContext(opts) {
    public DbSet<ReplayEntry> Replays { get; init; } = null!;
    public DbSet<PlayerEntry> Players { get; init; } = null!;
    public DbSet<ReplayCharacter> ReplayCharacters { get; init; } = null!;
    public DbSet<ReplayTeamObjective> ReplayTeamObjectives { get; init; } = null!;
    public DbSet<ReplayCharacterDraftOrder> ReplayCharacterDraftOrders { get; init; } = null!;
    public DbSet<ReplayCharacterTalent> ReplayCharacterTalents { get; init; } = null!;
    public DbSet<ReplayCharacterScoreResult> ReplayCharacterScoreResults { get; init; } = null!;
    public DbSet<ReplayCharacterMatchAward> ReplayCharacterMatchAwards { get; init; } = null!;
    public DbSet<HeroTalentInformation> HeroTalentInformations { get; init; } = null!;
    public DbSet<BuildNumber> BuildNumbers { get; init; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.UseCollation("NOCASE");
        modelBuilder.Entity<ReplayEntry>(e => {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedOnAdd();
            e.Property(r => r.GameMode).HasConversion<string>();
        });

        modelBuilder.Entity<PlayerEntry>(e => {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ReplayCharacter>(e => {
            e.HasKey(r => new {
                r.ReplayId,
                r.PlayerId,
            });
            e.HasOne(r => r.Replay).WithMany(r => r.ReplayCharacters).HasForeignKey(r => r.ReplayId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Player).WithMany(r => r.ReplayCharacters).HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReplayCharacterScoreResult>(e => {
            e.HasKey(r => new {
                r.ReplayId,
                r.PlayerId,
            });
            e.HasOne(r => r.Replay).WithMany().HasForeignKey(r => r.ReplayId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Player).WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ReplayCharacter).WithOne(r => r.ReplayCharacterScoreResult)
                .HasForeignKey<ReplayCharacterScoreResult>(r => new {
                    r.ReplayId,
                    r.PlayerId,
                });
        });

        modelBuilder.Entity<ReplayCharacterMatchAward>(e => {
            e.HasKey(r => new {
                r.ReplayId,
                r.PlayerId,
            });
            e.HasOne(r => r.Replay).WithMany().HasForeignKey(r => r.ReplayId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Player).WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ReplayCharacter).WithMany(r => r.ReplayCharacterMatchAwards)
                .HasForeignKey(r => new {
                    r.ReplayId,
                    r.PlayerId,
                });
            e.Property(r => r.MatchAwardType).HasConversion<string>();
        });

        modelBuilder.Entity<ReplayTeamObjective>(e => {
            e.HasKey(r => new { r.ReplayId, r.IsWinner, r.TeamObjectiveType, r.TimeSpan });
            e.HasOne(r => r.Replay).WithMany(r => r.ReplayTeamObjectives).HasForeignKey(r => r.ReplayId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Player).WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.TeamObjectiveType).HasConversion<string>();
        });

        modelBuilder.Entity<ReplayCharacterDraftOrder>(e => {
            e.HasKey(r => new {
                r.ReplayId,
                r.PlayerId,
            });
            e.HasOne(r => r.Replay).WithMany().HasForeignKey(r => r.ReplayId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Player).WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ReplayCharacter).WithOne(r => r.ReplayCharacterDraftOrder)
                .HasForeignKey<ReplayCharacterDraftOrder>(r => new {
                    r.ReplayId,
                    r.PlayerId,
                });
        });

        modelBuilder.Entity<ReplayCharacterTalent>(e => {
            e.HasKey(r => new {
                r.ReplayId,
                r.PlayerId,
                r.TalentId,
            });

            e.HasOne(r => r.Replay).WithMany().HasForeignKey(r => r.ReplayId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Player).WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ReplayCharacter).WithMany(r => r.ReplayCharacterTalents)
                .HasForeignKey(r => new {
                    r.ReplayId,
                    r.PlayerId,
                });
        });

        modelBuilder.Entity<HeroTalentInformation>(e => {
            e.HasKey(e => new {
                e.Character,
                e.ReplayBuildFirst,
                e.TalentId,
            });
        });

        modelBuilder.Entity<BuildNumber>(entity => {
            entity.HasKey(e => e.Buildnumber1);
        });
    }
}
