using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionService.Migrations
{
    public partial class AddSpendingTypeAndTxHashToTransaction : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpendingType",
                table: "LedgerTransactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TxHash",
                table: "LedgerTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpendingType",
                table: "LedgerTransactions");

            migrationBuilder.DropColumn(
                name: "TxHash",
                table: "LedgerTransactions");
        }
    }
}
