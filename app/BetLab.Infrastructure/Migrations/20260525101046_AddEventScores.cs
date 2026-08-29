using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayScore",
                table: "Events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeScore",
                table: "Events",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayScore",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "HomeScore",
                table: "Events");
        }
    }
}
