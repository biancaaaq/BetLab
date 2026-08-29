using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BetLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLimitsAndCasinoRtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RtpPercent",
                table: "CasinoGames",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Volatility",
                table: "CasinoGames",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "UserLimits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyDepositLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DailyLossLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DailySessionMinutesLimit = table.Column<int>(type: "integer", nullable: true),
                    RealityCheckIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    SelfExcludedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLimits_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLimits_UserId_CreatedAt",
                table: "UserLimits",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLimits");

            migrationBuilder.DropColumn(
                name: "RtpPercent",
                table: "CasinoGames");

            migrationBuilder.DropColumn(
                name: "Volatility",
                table: "CasinoGames");
        }
    }
}
