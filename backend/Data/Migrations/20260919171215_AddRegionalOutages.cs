using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cia.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionalOutages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regional_outages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostalCodePrefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedResolutionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regional_outages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_regional_outages_PostalCodePrefix_ResolvedAt",
                table: "regional_outages",
                columns: new[] { "PostalCodePrefix", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_regional_outages_StartedAt",
                table: "regional_outages",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regional_outages");
        }
    }
}
