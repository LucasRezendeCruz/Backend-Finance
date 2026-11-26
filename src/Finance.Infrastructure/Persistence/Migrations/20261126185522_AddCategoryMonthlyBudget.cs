using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryMonthlyBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "monthly_budget",
                schema: "finance",
                table: "categories",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_categories_monthly_budget",
                schema: "finance",
                table: "categories",
                sql: "monthly_budget IS NULL OR monthly_budget > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_categories_monthly_budget",
                schema: "finance",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "monthly_budget",
                schema: "finance",
                table: "categories");
        }
    }
}
