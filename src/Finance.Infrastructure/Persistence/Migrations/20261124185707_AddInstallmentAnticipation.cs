using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallmentAnticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "anticipated_at_utc",
                schema: "finance",
                table: "transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "installment_group_id",
                schema: "finance",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "original_amount",
                schema: "finance",
                table: "transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id_installment_group_id",
                schema: "finance",
                table: "transactions",
                columns: new[] { "user_id", "installment_group_id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_transactions_original_amount",
                schema: "finance",
                table: "transactions",
                sql: "original_amount IS NULL OR original_amount > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_user_id_installment_group_id",
                schema: "finance",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_transactions_original_amount",
                schema: "finance",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "anticipated_at_utc",
                schema: "finance",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "installment_group_id",
                schema: "finance",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "original_amount",
                schema: "finance",
                table: "transactions");
        }
    }
}
