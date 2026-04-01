using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RebuildSavingGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saving_goals",
                columns: table => new
                {
                    saving_goal_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    goal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    target_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    current_amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    target_date = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saving_goals", x => x.saving_goal_id);
                    table.ForeignKey(
                        name: "FK_saving_goals_member_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "member_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saving_goal_contributions",
                columns: table => new
                {
                    contribution_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    saving_goal_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    contribution_date = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    mirror_transaction_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saving_goal_contributions", x => x.contribution_id);
                    table.ForeignKey(
                        name: "FK_saving_goal_contributions_saving_goals_saving_goal_id",
                        column: x => x.saving_goal_id,
                        principalTable: "saving_goals",
                        principalColumn: "saving_goal_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_saving_goal_contributions_goal_date",
                table: "saving_goal_contributions",
                columns: new[] { "saving_goal_id", "contribution_date" });

            migrationBuilder.CreateIndex(
                name: "ix_saving_goal_contributions_user_goal",
                table: "saving_goal_contributions",
                columns: new[] { "user_id", "saving_goal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_saving_goals_user_created",
                table: "saving_goals",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_saving_goals_user_status",
                table: "saving_goals",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saving_goal_contributions");

            migrationBuilder.DropTable(
                name: "saving_goals");
        }
    }
}
