using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ExportJobs",
                table: "ExportJobs");

            migrationBuilder.RenameTable(
                name: "ExportJobs",
                newName: "export_jobs");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "export_jobs",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "export_jobs",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "export_jobs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "export_jobs",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "StartMonth",
                table: "export_jobs",
                newName: "start_month");

            migrationBuilder.RenameColumn(
                name: "S3Key",
                table: "export_jobs",
                newName: "s3_key");

            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "export_jobs",
                newName: "file_name");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                table: "export_jobs",
                newName: "error_message");

            migrationBuilder.RenameColumn(
                name: "EndMonth",
                table: "export_jobs",
                newName: "end_month");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "export_jobs",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "export_jobs",
                newName: "completed_at");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "export_jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "export_jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                table: "export_jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "start_month",
                table: "export_jobs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "s3_key",
                table: "export_jobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "file_name",
                table: "export_jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "error_message",
                table: "export_jobs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "end_month",
                table: "export_jobs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_export_jobs",
                table: "export_jobs",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_export_jobs_user_id",
                table: "export_jobs",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_export_jobs",
                table: "export_jobs");

            migrationBuilder.DropIndex(
                name: "IX_export_jobs_user_id",
                table: "export_jobs");

            migrationBuilder.RenameTable(
                name: "export_jobs",
                newName: "ExportJobs");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "ExportJobs",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "ExportJobs",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ExportJobs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "ExportJobs",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "start_month",
                table: "ExportJobs",
                newName: "StartMonth");

            migrationBuilder.RenameColumn(
                name: "s3_key",
                table: "ExportJobs",
                newName: "S3Key");

            migrationBuilder.RenameColumn(
                name: "file_name",
                table: "ExportJobs",
                newName: "FileName");

            migrationBuilder.RenameColumn(
                name: "error_message",
                table: "ExportJobs",
                newName: "ErrorMessage");

            migrationBuilder.RenameColumn(
                name: "end_month",
                table: "ExportJobs",
                newName: "EndMonth");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "ExportJobs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "completed_at",
                table: "ExportJobs",
                newName: "CompletedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "ExportJobs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ExportJobs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ExportJobs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "StartMonth",
                table: "ExportJobs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "S3Key",
                table: "ExportJobs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "ExportJobs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "ExportJobs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EndMonth",
                table: "ExportJobs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExportJobs",
                table: "ExportJobs",
                column: "Id");
        }
    }
}
