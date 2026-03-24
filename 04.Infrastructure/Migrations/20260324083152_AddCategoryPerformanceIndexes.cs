using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categories_user_id",
                table: "categories");

            migrationBuilder.CreateIndex(
                name: "ix_categories_user_created",
                table: "categories",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_categories_user_displayname",
                table: "categories",
                columns: new[] { "user_id", "display_name" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_user_type_active_created",
                table: "categories",
                columns: new[] { "user_id", "type", "is_active", "created_at" },
                descending: new[] { false, false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_categories_user_created",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ix_categories_user_displayname",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ix_categories_user_type_active_created",
                table: "categories");

            migrationBuilder.CreateIndex(
                name: "IX_categories_user_id",
                table: "categories",
                column: "user_id");
        }
    }
}
