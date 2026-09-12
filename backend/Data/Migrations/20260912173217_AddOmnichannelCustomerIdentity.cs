using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cia.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOmnichannelCustomerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channel_link_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_link_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_channel_link_codes_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_channel_identities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalChatId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_channel_identities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_channel_identities_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channel_link_codes_CodeHash",
                table: "channel_link_codes",
                column: "CodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_channel_link_codes_CustomerId_Channel",
                table: "channel_link_codes",
                columns: new[] { "CustomerId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_channel_identities_Channel_ExternalUserId",
                table: "customer_channel_identities",
                columns: new[] { "Channel", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_channel_identities_CustomerId",
                table: "customer_channel_identities",
                column: "CustomerId");

            migrationBuilder.Sql("""
                INSERT INTO customer_channel_identities ("Id", "CustomerId", "Channel", "ExternalUserId", "ExternalChatId", "DisplayName", "VerifiedAt", "CreatedAt")
                SELECT gen_random_uuid(), c."Id", 'Telegram', c."TelegramUserId"::text, c."TelegramChatId"::text, c."Name", NULL, c."CreatedAt"
                FROM customers c
                WHERE c."TelegramUserId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM customer_channel_identities i
                      WHERE i."Channel" = 'Telegram'
                        AND i."ExternalUserId" = c."TelegramUserId"::text
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_link_codes");

            migrationBuilder.DropTable(
                name: "customer_channel_identities");
        }
    }
}
