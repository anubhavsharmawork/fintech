using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace TransactionService.Migrations;

public partial class AddCorporateFieldsToTransaction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClientType",
            table: "LedgerTransactions",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Individual");

        migrationBuilder.AddColumn<Guid>(
            name: "OrganisationId",
            table: "LedgerTransactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PaymentBatchId",
            table: "LedgerTransactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_LedgerTransactions_OrganisationId",
            table: "LedgerTransactions",
            column: "OrganisationId");

        migrationBuilder.CreateIndex(
            name: "IX_LedgerTransactions_PaymentBatchId",
            table: "LedgerTransactions",
            column: "PaymentBatchId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_LedgerTransactions_PaymentBatchId", table: "LedgerTransactions");
        migrationBuilder.DropIndex(name: "IX_LedgerTransactions_OrganisationId", table: "LedgerTransactions");
        migrationBuilder.DropColumn(name: "PaymentBatchId", table: "LedgerTransactions");
        migrationBuilder.DropColumn(name: "OrganisationId", table: "LedgerTransactions");
        migrationBuilder.DropColumn(name: "ClientType", table: "LedgerTransactions");
    }
}
