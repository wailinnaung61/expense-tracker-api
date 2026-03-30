using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExcludeIncomeFromCategoryAggregations : Migration
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
                    TO_CHAR(transaction_date, 'YYYY-MM') AS period,
                    DATE_TRUNC('month', transaction_date)::date AS period_start,
                    (DATE_TRUNC('month', transaction_date) + INTERVAL '1 month' - INTERVAL '1 day')::date AS period_end,
                    SUM(amount) AS total_amount,
                    COUNT(*)::int AS transaction_count
                FROM transactions
                WHERE payment_status = 'COMPLETED'
                  AND type != 'INCOME'
                GROUP BY user_id, category_id, TO_CHAR(transaction_date, 'YYYY-MM'), DATE_TRUNC('month', transaction_date);
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_category_monthly_unique ON mv_category_monthly_aggregations(user_id, category_id, period);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_category_monthly_aggregations;");

            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_category_monthly_aggregations AS
                SELECT
                    user_id,
                    category_id,
                    TO_CHAR(transaction_date, 'YYYY-MM') AS period,
                    DATE_TRUNC('month', transaction_date)::date AS period_start,
                    (DATE_TRUNC('month', transaction_date) + INTERVAL '1 month' - INTERVAL '1 day')::date AS period_end,
                    SUM(amount) AS total_amount,
                    COUNT(*)::int AS transaction_count
                FROM transactions
                WHERE payment_status = 'COMPLETED'
                GROUP BY user_id, category_id, TO_CHAR(transaction_date, 'YYYY-MM'), DATE_TRUNC('month', transaction_date);
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_category_monthly_unique ON mv_category_monthly_aggregations(user_id, category_id, period);");
        }
    }
}
