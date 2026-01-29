using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiGateway.Migrations
{
    public partial class _20251101_SeedDemoLedgerData : Migration
    {
    protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert demo user if table exists (defensive) and not exists
            migrationBuilder.Sql(@"
DO $$
BEGIN
 IF EXISTS (
 SELECT 1 FROM information_schema.tables 
 WHERE table_schema = 'public' AND table_name = 'users_usvc'
 ) THEN
 INSERT INTO users_usvc (""Id"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""IsEmailVerified"", ""CreatedAt"", ""UpdatedAt"")
 SELECT 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'demo', 'YgpjgBLU+nFvWOyGaj+2+jvNAlib8hCImfFPowkuBHc=', 'Demo', 'User', true, NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM users_usvc WHERE ""Email"" = 'demo');
 END IF;
END$$;
");

 // Insert demo accounts idempotently (avoid unique AccountNumber conflicts)
 migrationBuilder.Sql(@"
INSERT INTO ""LedgerAccounts"" (""Id"", ""UserId"", ""AccountNumber"", ""AccountType"", ""Balance"", ""Currency"", ""CreatedAt"", ""UpdatedAt"")
VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, '123456789012', 'Checking',2500.50, 'NZD', NOW(), NOW())
ON CONFLICT DO NOTHING;

INSERT INTO ""LedgerAccounts"" (""Id"", ""UserId"", ""AccountNumber"", ""AccountType"", ""Balance"", ""Currency"", ""CreatedAt"", ""UpdatedAt"")
VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc'::uuid, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, '098765432109', 'Savings',10000.00, 'NZD', NOW(), NOW())
ON CONFLICT DO NOTHING;
");

 // Insert demo transactions idempotently
 migrationBuilder.Sql(@"
INSERT INTO ""LedgerTransactions"" (""Id"", ""AccountId"", ""UserId"", ""Amount"", ""Currency"", ""Type"", ""Description"", ""CreatedAt"", ""SpendingType"")
VALUES ('dddddddd-dddd-dddd-dddd-dddddddddddd'::uuid, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid,100.00, 'NZD', 'credit', 'Salary deposit', NOW() - INTERVAL '1 day', 'Future')
ON CONFLICT DO NOTHING;

INSERT INTO ""LedgerTransactions"" (""Id"", ""AccountId"", ""UserId"", ""Amount"", ""Currency"", ""Type"", ""Description"", ""CreatedAt"", ""SpendingType"")
VALUES ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee'::uuid, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid,50.00, 'NZD', 'debit', 'Grocery shopping', NOW() - INTERVAL '2 days', 'Fixed')
ON CONFLICT DO NOTHING;

INSERT INTO ""LedgerTransactions"" (""Id"", ""AccountId"", ""UserId"", ""Amount"", ""Currency"", ""Type"", ""Description"", ""CreatedAt"", ""SpendingType"")
VALUES ('ffffffff-ffff-ffff-ffff-ffffffffffff'::uuid, 'cccccccc-cccc-cccc-cccc-cccccccccccc'::uuid, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid,500.00, 'NZD', 'credit', 'Transfer from checking', NOW() - INTERVAL '3 days', 'Future')
ON CONFLICT DO NOTHING;
");
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.Sql(@"
DELETE FROM ""LedgerTransactions"" WHERE ""Id"" IN (
 'dddddddd-dddd-dddd-dddd-dddddddddddd'::uuid,
 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee'::uuid,
 'ffffffff-ffff-ffff-ffff-ffffffffffff'::uuid
);
DELETE FROM ""LedgerAccounts"" WHERE ""Id"" IN (
 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'::uuid,
 'cccccccc-cccc-cccc-cccc-cccccccccccc'::uuid
);
");
 }
 }
}
