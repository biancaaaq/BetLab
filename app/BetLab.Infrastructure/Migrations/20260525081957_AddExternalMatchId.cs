using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalMatchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalMatchId",
                table: "Events",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalMatchId",
                table: "Events");
        }
    }
}
