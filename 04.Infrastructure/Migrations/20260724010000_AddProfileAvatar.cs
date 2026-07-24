using Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _04.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260724010000_AddProfileAvatar")]
    public class AddProfileAvatar : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_source",
                table: "member_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "preset");

            migrationBuilder.AddColumn<string>(
                name: "avatar_preset_id",
                table: "member_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "avatar-01");

            migrationBuilder.AddColumn<string>(
                name: "avatar_storage_key",
                table: "member_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "avatar_source", table: "member_profiles");
            migrationBuilder.DropColumn(name: "avatar_preset_id", table: "member_profiles");
            migrationBuilder.DropColumn(name: "avatar_storage_key", table: "member_profiles");
        }
    }
}
