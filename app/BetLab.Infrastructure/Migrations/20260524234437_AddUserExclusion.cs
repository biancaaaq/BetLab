using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BetLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserExclusions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExcludedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExcludedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ImposedBy = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LiftedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LiftedByAdminEmail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserExclusions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserExclusions_UserId_IsActive",
                table: "UserExclusions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserExclusions");
        }
    }
}
