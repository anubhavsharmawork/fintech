using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace TransactionService.Migrations;

public partial class AddStatusToTransaction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "LedgerTransactions",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Completed");

        migrationBuilder.CreateIndex(
            name: "IX_LedgerTransactions_Status",
            table: "LedgerTransactions",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_LedgerTransactions_Status", table: "LedgerTransactions");
        migrationBuilder.DropColumn(name: "Status", table: "LedgerTransactions");
    }
}
