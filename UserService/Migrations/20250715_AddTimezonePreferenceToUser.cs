using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace UserService.Migrations;

public partial class AddTimezonePreferenceToUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TimeZoneId",
            table: "users_usvc",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "UtcOffsetMinutes",
            table: "users_usvc",
            type: "integer",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TimeZoneId",
            table: "users_usvc");

        migrationBuilder.DropColumn(
            name: "UtcOffsetMinutes",
            table: "users_usvc");
    }
}
