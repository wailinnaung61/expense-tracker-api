using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInvestmentGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investment_goals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investment_goals",
                columns: table => new
                {
                    goal_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    goal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "ix_investment_goals_user_status",
                table: "investment_goals",
                columns: new[] { "user_id", "status" });
        }
    }
}
