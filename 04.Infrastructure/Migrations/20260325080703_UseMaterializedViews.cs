using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseMaterializedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Change transaction_date from varchar to date
            migrationBuilder.Sql("""
                ALTER TABLE transactions
                ALTER COLUMN transaction_date TYPE date
                USING transaction_date::date;
                """);

            // 2. Create materialized views

            // Daily
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_daily_aggregations AS
                SELECT
                    user_id,
                    transaction_date AS period,
                    SUM(CASE WHEN type = 'INCOME' THEN amount ELSE 0 END) AS income,
                    SUM(CASE WHEN type = 'EXPENSE' THEN amount ELSE 0 END) AS expense,
                    SUM(CASE WHEN type = 'SAVINGS' THEN amount ELSE 0 END) AS saving,
                    SUM(CASE WHEN type = 'INVESTMENT' THEN amount ELSE 0 END) AS investment,
                    COUNT(*)::int AS transaction_count
                FROM transactions
                WHERE payment_status = 'COMPLETED'
                GROUP BY user_id, transaction_date;
                """);

            // Weekly
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_weekly_aggregations AS
                SELECT
                    user_id,
                    TO_CHAR(transaction_date, 'IYYY-"W"IW') AS period,
                    SUM(CASE WHEN type = 'INCOME' THEN amount ELSE 0 END) AS income,
                    SUM(CASE WHEN type = 'EXPENSE' THEN amount ELSE 0 END) AS expense,
                    SUM(CASE WHEN type = 'SAVINGS' THEN amount ELSE 0 END) AS saving,
                    SUM(CASE WHEN type = 'INVESTMENT' THEN amount ELSE 0 END) AS investment,
                    COUNT(*)::int AS transaction_count
                FROM transactions
                WHERE payment_status = 'COMPLETED'
                GROUP BY user_id, TO_CHAR(transaction_date, 'IYYY-"W"IW');
                """);

            // Monthly
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_monthly_aggregations AS
                SELECT
                    user_id,
                    TO_CHAR(transaction_date, 'YYYY-MM') AS period,
                    DATE_TRUNC('month', transaction_date)::date AS period_start,
                    (DATE_TRUNC('month', transaction_date) + INTERVAL '1 month' - INTERVAL '1 day')::date AS period_end,
                    SUM(CASE WHEN type = 'INCOME' THEN amount ELSE 0 END) AS income,
                    SUM(CASE WHEN type = 'EXPENSE' THEN amount ELSE 0 END) AS expense,
                    SUM(CASE WHEN type = 'SAVINGS' THEN amount ELSE 0 END) AS saving,
                    SUM(CASE WHEN type = 'INVESTMENT' THEN amount ELSE 0 END) AS investment,
                    COUNT(*)::int AS transaction_count
                FROM transactions
                WHERE payment_status = 'COMPLETED'
                GROUP BY user_id, TO_CHAR(transaction_date, 'YYYY-MM'), DATE_TRUNC('month', transaction_date);
                """);

            // Yearly
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW mv_yearly_aggregations AS
                SELECT
                    user_id,
                    TO_CHAR(transaction_date, 'YYYY') AS period,
                    SUM(CASE WHEN type = 'INCOME' THEN amount ELSE 0 END) AS income,
                    SUM(CASE WHEN type = 'EXPENSE' THEN amount ELSE 0 END) AS expense,
                    SUM(CASE WHEN type = 'SAVINGS' THEN amount ELSE 0 END) AS saving,
                    SUM(CASE WHEN type = 'INVESTMENT' THEN amount ELSE 0 END) AS investment,
                    COUNT(*)::int AS transaction_count
                FROM transactions
                WHERE payment_status = 'COMPLETED'
                GROUP BY user_id, TO_CHAR(transaction_date, 'YYYY');
                """);

            // Category Monthly
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

            // 3. Unique indexes (required for REFRESH CONCURRENTLY)
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_daily_unique ON mv_daily_aggregations(user_id, period);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_weekly_unique ON mv_weekly_aggregations(user_id, period);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_monthly_unique ON mv_monthly_aggregations(user_id, period);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_yearly_unique ON mv_yearly_aggregations(user_id, period);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX idx_mv_category_monthly_unique ON mv_category_monthly_aggregations(user_id, category_id, period);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_category_monthly_aggregations;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_yearly_aggregations;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_monthly_aggregations;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_weekly_aggregations;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_daily_aggregations;");

            migrationBuilder.Sql("""
                ALTER TABLE transactions
                ALTER COLUMN transaction_date TYPE character varying(20)
                USING transaction_date::text;
                """);
        }
    }
}
