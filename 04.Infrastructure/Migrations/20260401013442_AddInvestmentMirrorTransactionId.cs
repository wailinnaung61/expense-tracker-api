using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentMirrorTransactionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mirror_transaction_id",
                table: "investments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mirror_transaction_id",
                table: "investments");
        }
    }
}
