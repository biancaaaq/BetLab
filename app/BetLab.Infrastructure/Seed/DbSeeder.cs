using BetLab.Domain.Entities;
using BetLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BetLab.Infrastructure.Seed
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(BetLabDbContext context)
        {
            await context.Database.MigrateAsync();

            if (!await context.Sports.AnyAsync())
            {
                var football = new Sport
                {
                    Name = "Football",
                    IsActive = true
                };

                context.Sports.Add(football);
                await context.SaveChangesAsync();

                var premierLeague = new Competition
                {
                    Name = "Premier League",
                    Country = "England",
                    SportId = football.Id
                };

                context.Competitions.Add(premierLeague);
                await context.SaveChangesAsync();

                var liverpool = new Team { Name = "Liverpool", Country = "England" };
                var arsenal = new Team { Name = "Arsenal", Country = "England" };
                var chelsea = new Team { Name = "Chelsea", Country = "England" };
                var manCity = new Team { Name = "Manchester City", Country = "England" };

                context.Teams.AddRange(liverpool, arsenal, chelsea, manCity);
                await context.SaveChangesAsync();

                var event1 = new Event
                {
                    SportId = football.Id,
                    CompetitionId = premierLeague.Id,
                    HomeTeamId = liverpool.Id,
                    AwayTeamId = arsenal.Id,
                    StartTimeUtc = DateTime.UtcNow.AddDays(1),
                    Status = "Open",
                    IsLive = false
                };

                var event2 = new Event
                {
                    SportId = football.Id,
                    CompetitionId = premierLeague.Id,
                    HomeTeamId = chelsea.Id,
                    AwayTeamId = manCity.Id,
                    StartTimeUtc = DateTime.UtcNow.AddDays(2),
                    Status = "Open",
                    IsLive = false
                };

                context.Events.AddRange(event1, event2);
                await context.SaveChangesAsync();

                var market1 = new Market
                {
                    EventId = event1.Id,
                    Name = "Full Time Result",
                    MarketType = "1X2",
                    Status = "Open",
                    IsMain = true
                };

                var market2 = new Market
                {
                    EventId = event2.Id,
                    Name = "Full Time Result",
                    MarketType = "1X2",
                    Status = "Open",
                    IsMain = true
                };

                context.Markets.AddRange(market1, market2);
                await context.SaveChangesAsync();

                context.Outcomes.AddRange(
                    new Outcome
                    {
                        MarketId = market1.Id,
                        Name = "Liverpool",
                        Code = "1",
                        Odds = 2.10m,
                        Status = "Open"
                    },
                    new Outcome
                    {
                        MarketId = market1.Id,
                        Name = "Draw",
                        Code = "X",
                        Odds = 3.30m,
                        Status = "Open"
                    },
                    new Outcome
                    {
                        MarketId = market1.Id,
                        Name = "Arsenal",
                        Code = "2",
                        Odds = 3.00m,
                        Status = "Open"
                    },
                    new Outcome
                    {
                        MarketId = market2.Id,
                        Name = "Chelsea",
                        Code = "1",
                        Odds = 3.40m,
                        Status = "Open"
                    },
                    new Outcome
                    {
                        MarketId = market2.Id,
                        Name = "Draw",
                        Code = "X",
                        Odds = 3.50m,
                        Status = "Open"
                    },
                    new Outcome
                    {
                        MarketId = market2.Id,
                        Name = "Manchester City",
                        Code = "2",
                        Odds = 1.95m,
                        Status = "Open"
                    }
                );
            }
            if (!await context.CasinoGames.AnyAsync())
            {
                context.CasinoGames.AddRange(
                    new CasinoGame
                    {
                        Name = "Lucky Sevens 5x",
                        Type = "Slot",
                        IsActive = true,
                        RtpPercent = 96.20m,
                        Volatility = "Medium"
                    },
                    new CasinoGame
                    {
                        Name = "Diamond Reels",
                        Type = "Slot",
                        IsActive = true,
                        RtpPercent = 95.50m,
                        Volatility = "High"
                    },
                    new CasinoGame
                    {
                        Name = "Blackjack",
                        Type = "Blackjack",
                        IsActive = true,
                        RtpPercent = 99.50m,
                        Volatility = "Low"
                    },
                    new CasinoGame
                    {
                        Name = "Roulette",
                        Type = "Roulette",
                        IsActive = true,
                        RtpPercent = 97.30m,
                        Volatility = "Medium"
                    }
                );

                await context.SaveChangesAsync();
            }
        }

        public static async Task SeedDemoUserAsync(
            UserManager<AppUser> userManager,
            BetLabDbContext context)
        {
            const string demoEmail = "demo@betlab.local";
            const string demoPassword = "demo123";

            var existingUser = await userManager.FindByEmailAsync(demoEmail);
            if (existingUser != null)
                return;

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = demoEmail,
                Email = demoEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, demoPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Demo user creation failed: {errors}");
            }

            var wallet = new Wallet
            {
                UserId = user.Id,
                Balance = 100m,
                Currency = "RON"
            };

            context.Wallets.Add(wallet);
            await context.SaveChangesAsync();

            context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Type = "InitialCredit",
                Amount = 100m,
                BalanceBefore = 0m,
                BalanceAfter = 100m,
                Description = "Initial demo credit"
            });

            await context.SaveChangesAsync();
        }
        public static async Task SeedAdminAsync(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<AppUser> userManager,
    BetLabDbContext context)
        {
            const string adminRoleName = "Admin";
            const string adminEmail = "admin@betlab.local";
            const string adminPassword = "admin123";

            var roleExists = await roleManager.RoleExistsAsync(adminRoleName);
            if (!roleExists)
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Name = adminRoleName
                });

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    throw new Exception($"Admin role creation failed: {errors}");
                }
            }

            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null)
            {
                var adminUser = new AppUser
                {
                    Id = Guid.NewGuid(),
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new Exception($"Admin user creation failed: {errors}");
                }

                var addToRoleResult = await userManager.AddToRoleAsync(adminUser, adminRoleName);
                if (!addToRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addToRoleResult.Errors.Select(e => e.Description));
                    throw new Exception($"Admin user role assignment failed: {errors}");
                }

                var wallet = new Wallet
                {
                    UserId = adminUser.Id,
                    Balance = 5000m,
                    Currency = "RON"
                };

                context.Wallets.Add(wallet);
                await context.SaveChangesAsync();

                context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = wallet.Id,
                    Type = "InitialCredit",
                    Amount = 5000m,
                    BalanceBefore = 0m,
                    BalanceAfter = 5000m,
                    Description = "Initial admin demo credit"
                });

                await context.SaveChangesAsync();
            }
            else
            {
                var isInRole = await userManager.IsInRoleAsync(existingAdmin, adminRoleName);
                if (!isInRole)
                {
                    var addToRoleResult = await userManager.AddToRoleAsync(existingAdmin, adminRoleName);
                    if (!addToRoleResult.Succeeded)
                    {
                        var errors = string.Join(", ", addToRoleResult.Errors.Select(e => e.Description));
                        throw new Exception($"Existing admin role assignment failed: {errors}");
                    }
                }
            }
        }
    }
}