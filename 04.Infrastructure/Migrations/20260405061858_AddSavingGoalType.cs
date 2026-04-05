using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingGoalType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "saving_goal_type",
                table: "saving_goals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "OTHER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "saving_goal_type",
                table: "saving_goals");
        }
    }
}
