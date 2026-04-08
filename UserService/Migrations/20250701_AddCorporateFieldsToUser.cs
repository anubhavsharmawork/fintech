using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace UserService.Migrations;

public partial class AddCorporateFieldsToUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClientType",
            table: "users_usvc",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Individual");

        migrationBuilder.AddColumn<Guid>(
            name: "OrganisationId",
            table: "users_usvc",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OrganisationRole",
            table: "users_usvc",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CompanyName",
            table: "users_usvc",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RegistrationNumber",
            table: "users_usvc",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_usvc_OrganisationId",
            table: "users_usvc",
            column: "OrganisationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_users_usvc_OrganisationId", table: "users_usvc");
        migrationBuilder.DropColumn(name: "RegistrationNumber", table: "users_usvc");
        migrationBuilder.DropColumn(name: "CompanyName", table: "users_usvc");
        migrationBuilder.DropColumn(name: "OrganisationRole", table: "users_usvc");
        migrationBuilder.DropColumn(name: "OrganisationId", table: "users_usvc");
        migrationBuilder.DropColumn(name: "ClientType", table: "users_usvc");
    }
}
