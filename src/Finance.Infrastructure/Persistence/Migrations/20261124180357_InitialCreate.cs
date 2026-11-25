using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "users",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_secret = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    lockout_end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    initial_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    icon = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_categories_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    client_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    device_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_audit_logs",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_security_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "credit_cards",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    closing_day = table.Column<int>(type: "integer", nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_cards", x => x.id);
                    table.CheckConstraint("CK_credit_cards_closing_day", "closing_day >= 1 AND closing_day <= 31");
                    table.CheckConstraint("CK_credit_cards_due_day", "due_day >= 1 AND due_day <= 31");
                    table.CheckConstraint("CK_credit_cards_limit", "credit_limit > 0");
                    table.ForeignKey(
                        name: "FK_credit_cards_accounts_default_account_id",
                        column: x => x.default_account_id,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_cards_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_card_invoices",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_year = table.Column<int>(type: "integer", nullable: false),
                    reference_month = table.Column<int>(type: "integer", nullable: false),
                    closing_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_with_account_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_card_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_card_invoices_accounts_paid_with_account_id",
                        column: x => x.paid_with_account_id,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_card_invoices_credit_cards_credit_card_id",
                        column: x => x.credit_card_id,
                        principalSchema: "finance",
                        principalTable: "credit_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurrence_rules",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_installments = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurrence_rules", x => x.id);
                    table.CheckConstraint("CK_recurrence_rules_amount", "amount > 0");
                    table.ForeignKey(
                        name: "FK_recurrence_rules_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurrence_rules_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "finance",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurrence_rules_credit_cards_credit_card_id",
                        column: x => x.credit_card_id,
                        principalSchema: "finance",
                        principalTable: "credit_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurrence_rules_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_card_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transfer_pair_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recurrence_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: true),
                    total_installments = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.CheckConstraint("CK_transactions_amount", "amount > 0");
                    table.ForeignKey(
                        name: "FK_transactions_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "finance",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "finance",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_credit_card_invoices_credit_card_invoice_id",
                        column: x => x.credit_card_invoice_id,
                        principalSchema: "finance",
                        principalTable: "credit_card_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_recurrence_rules_recurrence_rule_id",
                        column: x => x.recurrence_rule_id,
                        principalSchema: "finance",
                        principalTable: "recurrence_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_transactions_transfer_pair_id",
                        column: x => x.transfer_pair_id,
                        principalSchema: "finance",
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "finance",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_user_id_is_archived",
                schema: "finance",
                table: "accounts",
                columns: new[] { "user_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_user_id_name_type",
                schema: "finance",
                table: "categories",
                columns: new[] { "user_id", "name", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_invoices_credit_card_id_reference_year_referenc~",
                schema: "finance",
                table: "credit_card_invoices",
                columns: new[] { "credit_card_id", "reference_year", "reference_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_invoices_paid_with_account_id",
                schema: "finance",
                table: "credit_card_invoices",
                column: "paid_with_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_cards_default_account_id",
                schema: "finance",
                table: "credit_cards",
                column: "default_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_cards_user_id",
                schema: "finance",
                table: "credit_cards",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_rules_account_id",
                schema: "finance",
                table: "recurrence_rules",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_rules_category_id",
                schema: "finance",
                table: "recurrence_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_rules_credit_card_id",
                schema: "finance",
                table: "recurrence_rules",
                column: "credit_card_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurrence_rules_user_id_is_active",
                schema: "finance",
                table: "recurrence_rules",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                schema: "finance",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_family_id",
                schema: "finance",
                table: "refresh_tokens",
                columns: new[] { "user_id", "family_id" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_logs_user_id_created_at_utc",
                schema: "finance",
                table: "security_audit_logs",
                columns: new[] { "user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_account_id",
                schema: "finance",
                table: "transactions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_category_id",
                schema: "finance",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_credit_card_invoice_id",
                schema: "finance",
                table: "transactions",
                column: "credit_card_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_recurrence_rule_id",
                schema: "finance",
                table: "transactions",
                column: "recurrence_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_transfer_pair_id",
                schema: "finance",
                table: "transactions",
                column: "transfer_pair_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id_transaction_date",
                schema: "finance",
                table: "transactions",
                columns: new[] { "user_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                schema: "finance",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.Sql("""
                REVOKE ALL ON SCHEMA finance FROM PUBLIC;
                REVOKE ALL ON ALL TABLES IN SCHEMA finance FROM PUBLIC;
                REVOKE ALL ON ALL SEQUENCES IN SCHEMA finance FROM PUBLIC;
                REVOKE ALL ON ALL FUNCTIONS IN SCHEMA finance FROM PUBLIC;

                CREATE OR REPLACE PROCEDURE pg_temp.finance_revoke_supabase_roles()
                LANGUAGE plpgsql
                AS $finance_hardening$
                DECLARE
                    role_name text;
                BEGIN
                    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                            EXECUTE format('REVOKE ALL ON SCHEMA finance FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA finance FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON ALL SEQUENCES IN SCHEMA finance FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON ALL FUNCTIONS IN SCHEMA finance FROM %I', role_name);
                            EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA finance REVOKE ALL ON TABLES FROM %I', role_name);
                            EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA finance REVOKE ALL ON SEQUENCES FROM %I', role_name);
                            EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA finance REVOKE ALL ON FUNCTIONS FROM %I', role_name);
                        END IF;
                    END LOOP;
                END
                $finance_hardening$;

                CALL pg_temp.finance_revoke_supabase_roles();

                ALTER TABLE finance.users ENABLE ROW LEVEL SECURITY;
                ALTER TABLE finance.accounts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE finance.categories ENABLE ROW LEVEL SECURITY;
                ALTER TABLE finance.refresh_tokens ENABLE ROW LEVEL SECURITY;
                ALTER TABLE finance.security_audit_logs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE finance.credit_cards ENABLE ROW LEVEL SECURITY;
                ALTER TABLE finance.credit_card_invoices ENABLE ROW LEVEL SECURITY;
                ALTER TABLE finance.recurrence_rules ENABLE ROW LEVEL SECURITY;
                ALTER TABLE finance.transactions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE IF EXISTS finance.__ef_migrations_history ENABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "security_audit_logs",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "transactions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "credit_card_invoices",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "recurrence_rules",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "credit_cards",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "users",
                schema: "finance");
        }
    }
}
