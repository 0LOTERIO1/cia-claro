using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cia.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentRoutingAndSharedContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentDepartment",
                table: "conversation_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Triage");

            migrationBuilder.AddColumn<string>(
                name: "PreviousDepartment",
                table: "conversation_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextSummary",
                table: "conversation_contexts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentRequest",
                table: "conversation_contexts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportantFacts",
                table: "conversation_contexts",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InternetStillDown",
                table: "conversation_contexts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OriginalProblem",
                table: "conversation_contexts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TroubleshootingPerformed",
                table: "conversation_contexts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "department_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDepartment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToDepartment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_department_transfers_conversation_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "conversation_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_department_transfers_SessionId",
                table: "department_transfers",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "department_transfers");

            migrationBuilder.DropColumn(
                name: "CurrentDepartment",
                table: "conversation_sessions");

            migrationBuilder.DropColumn(
                name: "PreviousDepartment",
                table: "conversation_sessions");

            migrationBuilder.DropColumn(
                name: "ContextSummary",
                table: "conversation_contexts");

            migrationBuilder.DropColumn(
                name: "CurrentRequest",
                table: "conversation_contexts");

            migrationBuilder.DropColumn(
                name: "ImportantFacts",
                table: "conversation_contexts");

            migrationBuilder.DropColumn(
                name: "InternetStillDown",
                table: "conversation_contexts");

            migrationBuilder.DropColumn(
                name: "OriginalProblem",
                table: "conversation_contexts");

            migrationBuilder.DropColumn(
                name: "TroubleshootingPerformed",
                table: "conversation_contexts");
        }
    }
}
