using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BetLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCasinoModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CasinoGames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasinoGames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CasinoSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CasinoGameId = table.Column<int>(type: "integer", nullable: false),
                    Variant = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasinoSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasinoSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CasinoSessions_CasinoGames_CasinoGameId",
                        column: x => x.CasinoGameId,
                        principalTable: "CasinoGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CasinoRounds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CasinoGameId = table.Column<int>(type: "integer", nullable: false),
                    CasinoSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Stake = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Multiplier = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Payout = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ResultType = table.Column<string>(type: "text", nullable: false),
                    SymbolsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasinoRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CasinoRounds_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CasinoRounds_CasinoGames_CasinoGameId",
                        column: x => x.CasinoGameId,
                        principalTable: "CasinoGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CasinoRounds_CasinoSessions_CasinoSessionId",
                        column: x => x.CasinoSessionId,
                        principalTable: "CasinoSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CasinoRounds_CasinoGameId",
                table: "CasinoRounds",
                column: "CasinoGameId");

            migrationBuilder.CreateIndex(
                name: "IX_CasinoRounds_CasinoSessionId",
                table: "CasinoRounds",
                column: "CasinoSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CasinoRounds_UserId",
                table: "CasinoRounds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CasinoSessions_CasinoGameId",
                table: "CasinoSessions",
                column: "CasinoGameId");

            migrationBuilder.CreateIndex(
                name: "IX_CasinoSessions_UserId",
                table: "CasinoSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CasinoRounds");

            migrationBuilder.DropTable(
                name: "CasinoSessions");

            migrationBuilder.DropTable(
                name: "CasinoGames");
        }
    }
}
