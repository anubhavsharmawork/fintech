using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace AccountService.Migrations;

public partial class AddCorporateFieldsToAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClientType",
            table: "LedgerAccounts",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Individual");

        migrationBuilder.AddColumn<Guid>(
            name: "OrganisationId",
            table: "LedgerAccounts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_LedgerAccounts_OrganisationId",
            table: "LedgerAccounts",
            column: "OrganisationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_LedgerAccounts_OrganisationId", table: "LedgerAccounts");
        migrationBuilder.DropColumn(name: "OrganisationId", table: "LedgerAccounts");
        migrationBuilder.DropColumn(name: "ClientType", table: "LedgerAccounts");
    }
}
