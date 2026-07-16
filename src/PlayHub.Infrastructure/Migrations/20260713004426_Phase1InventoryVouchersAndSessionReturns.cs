using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1InventoryVouchersAndSessionReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "SessionCafeteriaLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnedQuantity",
                table: "SessionCafeteriaLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "CafeteriaSales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextStockVoucherNumber",
                table: "branches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "session_cafeteria_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionCafeteriaLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReturnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_cafeteria_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_cafeteria_returns_SessionCafeteriaLines_SessionCafeteriaLineId",
                        column: x => x.SessionCafeteriaLineId,
                        principalTable: "SessionCafeteriaLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_session_cafeteria_returns_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_session_cafeteria_returns_users_ReturnedByUserId",
                        column: x => x.ReturnedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "stock_vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VoucherType = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RelatedCountVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_vouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_vouchers_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_stock_vouchers_stock_vouchers_RelatedCountVoucherId",
                        column: x => x.RelatedCountVoucherId,
                        principalTable: "stock_vouchers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_stock_vouchers_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_stock_vouchers_users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "stock_voucher_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SystemQuantity = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_voucher_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_voucher_lines_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_stock_voucher_lines_stock_vouchers_StockVoucherId",
                        column: x => x.StockVoucherId,
                        principalTable: "stock_vouchers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_cafeteria_returns_ReturnedByUserId",
                table: "session_cafeteria_returns",
                column: "ReturnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_session_cafeteria_returns_SessionCafeteriaLineId",
                table: "session_cafeteria_returns",
                column: "SessionCafeteriaLineId");

            migrationBuilder.CreateIndex(
                name: "IX_session_cafeteria_returns_SessionId",
                table: "session_cafeteria_returns",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_voucher_lines_CafeteriaItemId",
                table: "stock_voucher_lines",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_voucher_lines_StockVoucherId",
                table: "stock_voucher_lines",
                column: "StockVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_vouchers_BranchId_VoucherNumber",
                table: "stock_vouchers",
                columns: new[] { "BranchId", "VoucherNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_vouchers_CreatedByUserId",
                table: "stock_vouchers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_vouchers_PostedByUserId",
                table: "stock_vouchers",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_vouchers_RelatedCountVoucherId",
                table: "stock_vouchers",
                column: "RelatedCountVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_vouchers_TenantId_BranchId_VoucherType_Status",
                table: "stock_vouchers",
                columns: new[] { "TenantId", "BranchId", "VoucherType", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_cafeteria_returns");

            migrationBuilder.DropTable(
                name: "stock_voucher_lines");

            migrationBuilder.DropTable(
                name: "stock_vouchers");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "SessionCafeteriaLines");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                table: "SessionCafeteriaLines");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "CafeteriaSales");

            migrationBuilder.DropColumn(
                name: "NextStockVoucherNumber",
                table: "branches");
        }
    }
}
