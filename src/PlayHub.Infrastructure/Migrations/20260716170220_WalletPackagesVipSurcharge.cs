using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WalletPackagesVipSurcharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RoomSurchargeCost",
                table: "sessions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomSurchargePerHour",
                table: "sessions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VipSurchargePerHour",
                table: "Rooms",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PackageDurationMinutes",
                table: "PricingPlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagePrice",
                table: "PricingPlans",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WalletBalance",
                table: "customers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "wallet_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wallet_transactions_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_CreatedByUserId",
                table: "wallet_transactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_CustomerId",
                table: "wallet_transactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_InvoiceId",
                table: "wallet_transactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_TenantId_CustomerId_CreatedAt",
                table: "wallet_transactions",
                columns: new[] { "TenantId", "CustomerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "RoomSurchargeCost",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "RoomSurchargePerHour",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "VipSurchargePerHour",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "PackageDurationMinutes",
                table: "PricingPlans");

            migrationBuilder.DropColumn(
                name: "PackagePrice",
                table: "PricingPlans");

            migrationBuilder.DropColumn(
                name: "WalletBalance",
                table: "customers");
        }
    }
}
