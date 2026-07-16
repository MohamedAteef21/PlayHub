using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "wallet_transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CollectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_collections_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cash_collections_users_CollectedByUserId",
                        column: x => x.CollectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_collections_BranchId",
                table: "cash_collections",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_collections_CollectedByUserId",
                table: "cash_collections",
                column: "CollectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_collections_TenantId_BranchId_CollectedAt",
                table: "cash_collections",
                columns: new[] { "TenantId", "BranchId", "CollectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_collections");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "wallet_transactions");
        }
    }
}
