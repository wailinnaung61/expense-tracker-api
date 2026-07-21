using Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260721080000_AddEmailSentAndNotifyEmailEnabled")]
    public class AddEmailSentAndNotifyEmailEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "notify_email_enabled",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "email_sent_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    to_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: true),
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reference_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    milestone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_sent_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_sent_logs_status",
                table: "email_sent_logs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_email_sent_logs_user_id_created_at",
                table: "email_sent_logs",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_email_sent_logs_user_id_type_reference_id_milestone",
                table: "email_sent_logs",
                columns: new[] { "user_id", "type", "reference_id", "milestone" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "email_sent_logs");

            migrationBuilder.DropColumn(
                name: "notify_email_enabled",
                table: "member_profiles");
        }
    }
}
