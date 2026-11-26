using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramBotIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telegram_link_tokens",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_link_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_telegram_link_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telegram_links",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    telegram_chat_id = table.Column<long>(type: "bigint", nullable: false),
                    telegram_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telegram_first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    default_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_credit_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_bot_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_interaction_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_telegram_links_accounts_default_account_id",
                        column: x => x.default_account_id,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_telegram_links_credit_cards_default_credit_card_id",
                        column: x => x.default_credit_card_id,
                        principalSchema: "finance",
                        principalTable: "credit_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_telegram_links_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_link_tokens_token_hash",
                schema: "finance",
                table: "telegram_link_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_link_tokens_user_id_expires_at_utc",
                schema: "finance",
                table: "telegram_link_tokens",
                columns: new[] { "user_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_links_default_account_id",
                schema: "finance",
                table: "telegram_links",
                column: "default_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_links_default_credit_card_id",
                schema: "finance",
                table: "telegram_links",
                column: "default_credit_card_id");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_links_telegram_chat_id",
                schema: "finance",
                table: "telegram_links",
                column: "telegram_chat_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_links_user_id",
                schema: "finance",
                table: "telegram_links",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telegram_link_tokens",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "telegram_links",
                schema: "finance");
        }
    }
}
