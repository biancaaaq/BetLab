using BetLab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;


namespace BetLab.Infrastructure.Persistence
{
    public class BetLabDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
    {
        public BetLabDbContext(DbContextOptions<BetLabDbContext> options)
            : base(options)
        {
        }

        public DbSet<CasinoGame> CasinoGames => Set<CasinoGame>();
        public DbSet<CasinoSession> CasinoSessions => Set<CasinoSession>();
        public DbSet<CasinoRound> CasinoRounds => Set<CasinoRound>();
        public DbSet<Sport> Sports => Set<Sport>();
        public DbSet<Competition> Competitions => Set<Competition>();
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<Market> Markets => Set<Market>();
        public DbSet<Outcome> Outcomes => Set<Outcome>();
        public DbSet<Wallet> Wallets => Set<Wallet>();
        public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
        public DbSet<BetSlip> BetSlips => Set<BetSlip>();
        public DbSet<BetSelection> BetSelections => Set<BetSelection>();
        public DbSet<ExperimentSession> ExperimentSessions => Set<ExperimentSession>();
        public DbSet<InteractionLog> InteractionLogs => Set<InteractionLog>();
        public DbSet<UserLimit> UserLimits => Set<UserLimit>();
        public DbSet<RouletteRound> RouletteRounds => Set<RouletteRound>();
        public DbSet<BlackjackSession> BlackjackSessions => Set<BlackjackSession>();
        public DbSet<UserExclusion> UserExclusions => Set<UserExclusion>();
        /// <summary>Registrul de Autoexcludere prin CNP (simulare ONJN).</summary>
        public DbSet<CnpExclusionEntry> CnpExclusions => Set<CnpExclusionEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ExperimentSession>().HasKey(x => x.Id);

            modelBuilder.Entity<AppUser>()
                .HasOne(x => x.Wallet)
                .WithOne(x => x.User)
                .HasForeignKey<Wallet>(x => x.UserId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.HomeTeam)
                .WithMany()
                .HasForeignKey(x => x.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.AwayTeam)
                .WithMany()
                .HasForeignKey(x => x.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BetSelection>()
                .Property(x => x.SelectedOdds)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Outcome>()
                .Property(x => x.Odds)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Wallet>()
                .Property(x => x.Balance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<WalletTransaction>()
                .Property(x => x.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<WalletTransaction>()
                .Property(x => x.BalanceBefore)
                .HasPrecision(18, 2);

            modelBuilder.Entity<WalletTransaction>()
                .Property(x => x.BalanceAfter)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BetSlip>()
                .Property(x => x.Stake)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BetSlip>()
                .Property(x => x.TotalOdds)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BetSlip>()
                .Property(x => x.PotentialPayout)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BetSlip>()
                .Property(x => x.ActualPayout)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CasinoSession>().HasKey(x => x.Id);

            modelBuilder.Entity<CasinoRound>()
                .Property(x => x.Stake)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CasinoRound>()
                .Property(x => x.Multiplier)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CasinoRound>()
                .Property(x => x.Payout)
                .HasPrecision(18, 2);

            // CasinoGame.RtpPercent — precision pentru afișarea corectă (ex: 96.50).
            modelBuilder.Entity<CasinoGame>()
                .Property(x => x.RtpPercent)
                .HasPrecision(5, 2);

            
            modelBuilder.Entity<UserLimit>()
                .HasOne(x => x.User)
                .WithMany(x => x.Limits)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserLimit>()
                .Property(x => x.DailyDepositLimit)
                .HasPrecision(18, 2);

            modelBuilder.Entity<UserLimit>()
                .Property(x => x.DailyLossLimit)
                .HasPrecision(18, 2);

            // Index pe (UserId, CreatedAt desc) ca să găsim rapid limita curentă a unui user.
            modelBuilder.Entity<UserLimit>()
                .HasIndex(x => new { x.UserId, x.CreatedAt });

            modelBuilder.Entity<RouletteRound>()
                .Property(x => x.Stake).HasPrecision(18, 2);
            modelBuilder.Entity<RouletteRound>()
                .Property(x => x.Payout).HasPrecision(18, 2);
            modelBuilder.Entity<RouletteRound>()
                .Property(x => x.NewBalance).HasPrecision(18, 2);

            modelBuilder.Entity<BlackjackSession>()
                .Property(x => x.Stake).HasPrecision(18, 2);
            modelBuilder.Entity<BlackjackSession>()
                .Property(x => x.Payout).HasPrecision(18, 2);
            modelBuilder.Entity<BlackjackSession>()
                .Property(x => x.NewBalance).HasPrecision(18, 2);

            
            modelBuilder.Entity<UserExclusion>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserExclusion>()
                .HasIndex(x => new { x.UserId, x.IsActive });

            // CnpExclusionEntry — Registru Autoexcludere CNP (simulare ONJN)
            modelBuilder.Entity<CnpExclusionEntry>()
                .HasIndex(x => x.Cnp)
                .IsUnique();  // un CNP poate apărea o singură dată în registru
        }
    }
}