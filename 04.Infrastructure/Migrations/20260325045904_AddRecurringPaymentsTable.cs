using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringPaymentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurring_payments",
                columns: table => new
                {
                    recurring_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(15,2)", nullable: false),
                    category_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    next_due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_paid_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    missed_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    auto_pay = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_payments", x => x.recurring_id);
                    table.ForeignKey(
                        name: "FK_recurring_payments_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_payments_member_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "member_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_payments_category_id",
                table: "recurring_payments",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_user_nextdue",
                table: "recurring_payments",
                columns: new[] { "user_id", "next_due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_recurring_user_status_nextdue",
                table: "recurring_payments",
                columns: new[] { "user_id", "status", "next_due_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurring_payments");
        }
    }
}
