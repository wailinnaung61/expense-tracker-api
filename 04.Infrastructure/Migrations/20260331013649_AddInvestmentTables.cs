using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investment_goals",
                columns: table => new
                {
                    goal_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    goal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    current_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_goals", x => x.goal_id);
                    table.ForeignKey(
                        name: "FK_investment_goals_member_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "member_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "investment_portfolios",
                columns: table => new
                {
                    portfolio_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_portfolios", x => x.portfolio_id);
                    table.ForeignKey(
                        name: "FK_investment_portfolios_member_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "member_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "investments",
                columns: table => new
                {
                    investment_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    portfolio_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    asset_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    asset_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    purchase_price = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    current_price = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investments", x => x.investment_id);
                    table.ForeignKey(
                        name: "FK_investments_investment_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "investment_portfolios",
                        principalColumn: "portfolio_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_investments_member_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "member_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_investment_goals_user_status",
                table: "investment_goals",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_investment_portfolios_user_active",
                table: "investment_portfolios",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_investments_portfolio_id",
                table: "investments",
                column: "portfolio_id");

            migrationBuilder.CreateIndex(
                name: "ix_investments_user_asset_type",
                table: "investments",
                columns: new[] { "user_id", "asset_type" });

            migrationBuilder.CreateIndex(
                name: "ix_investments_user_portfolio",
                table: "investments",
                columns: new[] { "user_id", "portfolio_id" });

            migrationBuilder.CreateIndex(
                name: "ix_investments_user_purchase_date",
                table: "investments",
                columns: new[] { "user_id", "purchase_date" });

            migrationBuilder.CreateIndex(
                name: "ix_investments_user_status",
                table: "investments",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investment_goals");

            migrationBuilder.DropTable(
                name: "investments");

            migrationBuilder.DropTable(
                name: "investment_portfolios");
        }
    }
}
