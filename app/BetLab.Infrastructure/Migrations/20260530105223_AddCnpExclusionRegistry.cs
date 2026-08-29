using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BetLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCnpExclusionRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cnp",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CnpExclusions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cnp = table.Column<string>(type: "text", nullable: false),
                    ExcludedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CnpExclusions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CnpExclusions_Cnp",
                table: "CnpExclusions",
                column: "Cnp",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CnpExclusions");

            migrationBuilder.DropColumn(
                name: "Cnp",
                table: "AspNetUsers");
        }
    }
}
