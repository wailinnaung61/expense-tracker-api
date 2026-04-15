using Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260415143000_AddExportJobBudgetId")]
    public class AddExportJobBudgetId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: a prior migration may have been recorded without running DDL (empty Up).
            migrationBuilder.Sql(
                """
                ALTER TABLE export_jobs
                ADD COLUMN IF NOT EXISTS budget_id character varying(50) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE export_jobs
                DROP COLUMN IF EXISTS budget_id;
                """);
        }
    }
}
