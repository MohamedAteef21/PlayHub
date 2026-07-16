using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BranchMultiPaymentAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_payment_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountType = table.Column<short>(type: "smallint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_payment_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branch_payment_accounts_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_branch_payment_accounts_BranchId_AccountType_SortOrder",
                table: "branch_payment_accounts",
                columns: new[] { "BranchId", "AccountType", "SortOrder" });

            // Migrate legacy single-account fields into the new multi-account table
            migrationBuilder.Sql("""
                INSERT INTO branch_payment_accounts (Id, TenantId, BranchId, AccountType, Label, AccountNumber, SortOrder, IsActive, CreatedAt)
                SELECT NEWID(), TenantId, Id, 1, N'Shared', SharedTransferAccount, 0, 1, SYSUTCDATETIME()
                FROM branches
                WHERE UseSharedTransferAccount = 1 AND SharedTransferAccount IS NOT NULL AND LTRIM(RTRIM(SharedTransferAccount)) <> '';

                INSERT INTO branch_payment_accounts (Id, TenantId, BranchId, AccountType, Label, AccountNumber, SortOrder, IsActive, CreatedAt)
                SELECT NEWID(), TenantId, Id, 2, N'Shared', SharedTransferAccount, 1, 1, SYSUTCDATETIME()
                FROM branches
                WHERE UseSharedTransferAccount = 1 AND SharedTransferAccount IS NOT NULL AND LTRIM(RTRIM(SharedTransferAccount)) <> '';

                INSERT INTO branch_payment_accounts (Id, TenantId, BranchId, AccountType, Label, AccountNumber, SortOrder, IsActive, CreatedAt)
                SELECT NEWID(), TenantId, Id, 1, NULL, BankTransferAccount, 0, 1, SYSUTCDATETIME()
                FROM branches
                WHERE (UseSharedTransferAccount = 0 OR UseSharedTransferAccount IS NULL)
                  AND BankTransferAccount IS NOT NULL AND LTRIM(RTRIM(BankTransferAccount)) <> '';

                INSERT INTO branch_payment_accounts (Id, TenantId, BranchId, AccountType, Label, AccountNumber, SortOrder, IsActive, CreatedAt)
                SELECT NEWID(), TenantId, Id, 2, NULL, DigitalWalletAccount, 1, 1, SYSUTCDATETIME()
                FROM branches
                WHERE (UseSharedTransferAccount = 0 OR UseSharedTransferAccount IS NULL)
                  AND DigitalWalletAccount IS NOT NULL AND LTRIM(RTRIM(DigitalWalletAccount)) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_payment_accounts");
        }
    }
}
