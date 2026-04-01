using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterCategoryMonthlyMVNullCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_category_monthly_aggregations;");

            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_category_monthly_aggregations AS
                SELECT
                    user_id,
                    category_id,
                    type,
                    TO_CHAR(transaction_date, 'YYYY-MM') AS period,
                    DATE_TRUNC('month', transaction_date)::date AS period_start,
                    (DATE_TRUNC('month', transaction_date) + INTERVAL '1 month' - INTERVAL '1 day')::date AS period_end,
                    SUM(amount) AS total_amount,
                    COUNT(*)::int AS transaction_count
                FROM transactions
                WHERE payment_status = 'COMPLETED'
                  AND category_id IS NOT NULL
                GROUP BY user_id, category_id, type, TO_CHAR(transaction_date, 'YYYY-MM'), DATE_TRUNC('month', transaction_date);
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_category_monthly_unique ON mv_category_monthly_aggregations(user_id, category_id, type, period);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_category_monthly_aggregations;");

            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_category_monthly_aggregations AS
                SELECT
                    user_id,
                    category_id,
                    type,
                    TO_CHAR(transaction_date, 'YYYY-MM') AS period,
                    DATE_TRUNC('month', transaction_date)::date AS period_start,
                    (DATE_TRUNC('month', transaction_date) + INTERVAL '1 month' - INTERVAL '1 day')::date AS period_end,
                    SUM(amount) AS total_amount,
                    COUNT(*)::int AS transaction_count
                FROM transactions
                WHERE payment_status = 'COMPLETED'
                GROUP BY user_id, category_id, type, TO_CHAR(transaction_date, 'YYYY-MM'), DATE_TRUNC('month', transaction_date);
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_category_monthly_unique ON mv_category_monthly_aggregations(user_id, COALESCE(category_id, ''), type, period);");
        }
    }
}
