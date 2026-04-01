using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "budgets",
                columns: table => new
                {
                    budget_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    period_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budgets", x => x.budget_id);
                    table.ForeignKey(
                        name: "FK_budgets_member_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "member_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "budget_categories",
                columns: table => new
                {
                    budget_category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    budget_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    alert_threshold = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_categories", x => x.budget_category_id);
                    table.ForeignKey(
                        name: "FK_budget_categories_budgets_budget_id",
                        column: x => x.budget_id,
                        principalTable: "budgets",
                        principalColumn: "budget_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_budget_categories_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_snapshots",
                columns: table => new
                {
                    budget_category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    spent_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    transaction_count = table.Column<int>(type: "integer", nullable: false),
                    last_transaction_date = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_snapshots", x => x.budget_category_id);
                    table.ForeignKey(
                        name: "FK_budget_snapshots_budget_categories_budget_category_id",
                        column: x => x.budget_category_id,
                        principalTable: "budget_categories",
                        principalColumn: "budget_category_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_categories_budget_category",
                table: "budget_categories",
                columns: new[] { "budget_id", "category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_categories_category_id",
                table: "budget_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_budgets_user_start",
                table: "budgets",
                columns: new[] { "user_id", "start_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_snapshots");

            migrationBuilder.DropTable(
                name: "budget_categories");

            migrationBuilder.DropTable(
                name: "budgets");
        }
    }
}
