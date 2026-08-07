using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerDebtAndCafeteriaHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoicePayments_invoices_InvoiceId",
                table: "InvoicePayments");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoicePayments_users_CollectedByUserId",
                table: "InvoicePayments");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentProofs_InvoicePayments_InvoicePaymentId",
                table: "PaymentProofs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InvoicePayments",
                table: "InvoicePayments");

            migrationBuilder.RenameTable(
                name: "InvoicePayments",
                newName: "invoice_payments");

            migrationBuilder.RenameIndex(
                name: "IX_InvoicePayments_InvoiceId",
                table: "invoice_payments",
                newName: "IX_invoice_payments_InvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_InvoicePayments_CollectedByUserId",
                table: "invoice_payments",
                newName: "IX_invoice_payments_CollectedByUserId");

            migrationBuilder.AlterColumn<string>(
                name: "DebtorPhone",
                table: "invoice_payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DebtorName",
                table: "invoice_payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "invoice_payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_invoice_payments",
                table: "invoice_payments",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "cafeteria_holds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachedSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConvertedSaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_holds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_holds_CafeteriaSales_ConvertedSaleId",
                        column: x => x.ConvertedSaleId,
                        principalTable: "CafeteriaSales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_holds_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_holds_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_holds_sessions_AttachedSessionId",
                        column: x => x.AttachedSessionId,
                        principalTable: "sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_holds_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "cafeteria_hold_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HoldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VariantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    StockDeductQuantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_hold_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_hold_lines_CafeteriaItemVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "CafeteriaItemVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_hold_lines_cafeteria_holds_HoldId",
                        column: x => x.HoldId,
                        principalTable: "cafeteria_holds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_hold_lines_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "cafeteria_hold_line_add_ons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HoldLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddOnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StockDeductQuantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_hold_line_add_ons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_hold_line_add_ons_cafeteria_add_ons_AddOnId",
                        column: x => x.AddOnId,
                        principalTable: "cafeteria_add_ons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_hold_line_add_ons_cafeteria_hold_lines_HoldLineId",
                        column: x => x.HoldLineId,
                        principalTable: "cafeteria_hold_lines",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "cafeteria_hold_line_ingredient_deducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HoldLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    WasSkipped = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_hold_line_ingredient_deducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_hold_line_ingredient_deducts_cafeteria_hold_lines_HoldLineId",
                        column: x => x.HoldLineId,
                        principalTable: "cafeteria_hold_lines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_hold_line_ingredient_deducts_cafeteria_items_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_CustomerId",
                table: "invoice_payments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_hold_line_add_ons_AddOnId",
                table: "cafeteria_hold_line_add_ons",
                column: "AddOnId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_hold_line_add_ons_HoldLineId",
                table: "cafeteria_hold_line_add_ons",
                column: "HoldLineId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_hold_line_ingredient_deducts_HoldLineId",
                table: "cafeteria_hold_line_ingredient_deducts",
                column: "HoldLineId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_hold_line_ingredient_deducts_WarehouseItemId",
                table: "cafeteria_hold_line_ingredient_deducts",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_hold_lines_CafeteriaItemId",
                table: "cafeteria_hold_lines",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_hold_lines_HoldId",
                table: "cafeteria_hold_lines",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_hold_lines_VariantId",
                table: "cafeteria_hold_lines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_holds_AttachedSessionId",
                table: "cafeteria_holds",
                column: "AttachedSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_holds_BranchId",
                table: "cafeteria_holds",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_holds_ConvertedSaleId",
                table: "cafeteria_holds",
                column: "ConvertedSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_holds_CreatedByUserId",
                table: "cafeteria_holds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_holds_CustomerId",
                table: "cafeteria_holds",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_holds_TenantId_BranchId_Status",
                table: "cafeteria_holds",
                columns: new[] { "TenantId", "BranchId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_payments_customers_CustomerId",
                table: "invoice_payments",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_payments_invoices_InvoiceId",
                table: "invoice_payments",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_payments_users_CollectedByUserId",
                table: "invoice_payments",
                column: "CollectedByUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentProofs_invoice_payments_InvoicePaymentId",
                table: "PaymentProofs",
                column: "InvoicePaymentId",
                principalTable: "invoice_payments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoice_payments_customers_CustomerId",
                table: "invoice_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_payments_invoices_InvoiceId",
                table: "invoice_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_payments_users_CollectedByUserId",
                table: "invoice_payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentProofs_invoice_payments_InvoicePaymentId",
                table: "PaymentProofs");

            migrationBuilder.DropTable(
                name: "cafeteria_hold_line_add_ons");

            migrationBuilder.DropTable(
                name: "cafeteria_hold_line_ingredient_deducts");

            migrationBuilder.DropTable(
                name: "cafeteria_hold_lines");

            migrationBuilder.DropTable(
                name: "cafeteria_holds");

            migrationBuilder.DropPrimaryKey(
                name: "PK_invoice_payments",
                table: "invoice_payments");

            migrationBuilder.DropIndex(
                name: "IX_invoice_payments_CustomerId",
                table: "invoice_payments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "invoice_payments");

            migrationBuilder.RenameTable(
                name: "invoice_payments",
                newName: "InvoicePayments");

            migrationBuilder.RenameIndex(
                name: "IX_invoice_payments_InvoiceId",
                table: "InvoicePayments",
                newName: "IX_InvoicePayments_InvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_invoice_payments_CollectedByUserId",
                table: "InvoicePayments",
                newName: "IX_InvoicePayments_CollectedByUserId");

            migrationBuilder.AlterColumn<string>(
                name: "DebtorPhone",
                table: "InvoicePayments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DebtorName",
                table: "InvoicePayments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_InvoicePayments",
                table: "InvoicePayments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoicePayments_invoices_InvoiceId",
                table: "InvoicePayments",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoicePayments_users_CollectedByUserId",
                table: "InvoicePayments",
                column: "CollectedByUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentProofs_InvoicePayments_InvoicePaymentId",
                table: "PaymentProofs",
                column: "InvoicePaymentId",
                principalTable: "InvoicePayments",
                principalColumn: "Id");
        }
    }
}
