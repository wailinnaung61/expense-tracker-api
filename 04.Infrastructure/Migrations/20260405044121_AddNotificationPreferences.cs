using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "notify_auto_payments",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_budget_alerts",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_exports",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_large_transactions",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_payment_failures",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_recurring_payments",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_saving_goals",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "notify_auto_payments",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "notify_budget_alerts",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "notify_exports",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "notify_large_transactions",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "notify_payment_failures",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "notify_recurring_payments",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "notify_saving_goals",
                table: "member_profiles");
        }
    }
}
