using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cia.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndHumanAgentQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "human_agent_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_human_agent_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_human_agent_requests_conversation_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "conversation_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_human_agent_requests_users_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_human_agent_requests_AssignedAgentId",
                table: "human_agent_requests",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_human_agent_requests_SessionId",
                table: "human_agent_requests",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_human_agent_requests_Status",
                table: "human_agent_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_users_CustomerId",
                table: "users",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "human_agent_requests");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
