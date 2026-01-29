using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountService.Migrations
{
    public partial class AddCryptoMetadataToAccount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCrypto",
                table: "LedgerAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Blockchain",
                table: "LedgerAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "LedgerAccounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenSymbol",
                table: "LedgerAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCrypto",
                table: "LedgerAccounts");

            migrationBuilder.DropColumn(
                name: "Blockchain",
                table: "LedgerAccounts");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "LedgerAccounts");

            migrationBuilder.DropColumn(
                name: "TokenSymbol",
                table: "LedgerAccounts");
        }
    }
}
