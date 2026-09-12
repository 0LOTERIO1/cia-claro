using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cia.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessibilityPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accessibility_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FontScale = table.Column<double>(type: "double precision", precision: 4, scale: 2, nullable: false),
                    HighContrast = table.Column<bool>(type: "boolean", nullable: false),
                    Theme = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReducedMotion = table.Column<bool>(type: "boolean", nullable: false),
                    ReadingSpacing = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReadAloudEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accessibility_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accessibility_preferences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accessibility_preferences_UserId",
                table: "accessibility_preferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accessibility_preferences");
        }
    }
}
