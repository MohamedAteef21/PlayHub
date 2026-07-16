using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryUnitsAndConversionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnteredQuantity",
                table: "stock_voucher_lines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "EnteredUnit",
                table: "stock_voucher_lines",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AlterColumn<string>(
                name: "LargeUnitName",
                table: "cafeteria_items",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BaseUnitName",
                table: "cafeteria_items",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "inventory_units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_units_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "item_unit_conversion_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldBaseUnitName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NewBaseUnitName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldLargeUnitName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NewLargeUnitName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldUnitsPerLarge = table.Column<int>(type: "int", nullable: false),
                    NewUnitsPerLarge = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_unit_conversion_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_unit_conversion_logs_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_item_unit_conversion_logs_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_item_unit_conversion_logs_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_units_TenantId_Name",
                table: "inventory_units",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_unit_conversion_logs_BranchId_CafeteriaItemId_CreatedAt",
                table: "item_unit_conversion_logs",
                columns: new[] { "BranchId", "CafeteriaItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_item_unit_conversion_logs_CafeteriaItemId",
                table: "item_unit_conversion_logs",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_item_unit_conversion_logs_ChangedByUserId",
                table: "item_unit_conversion_logs",
                column: "ChangedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_units");

            migrationBuilder.DropTable(
                name: "item_unit_conversion_logs");

            migrationBuilder.DropColumn(
                name: "EnteredQuantity",
                table: "stock_voucher_lines");

            migrationBuilder.DropColumn(
                name: "EnteredUnit",
                table: "stock_voucher_lines");

            migrationBuilder.AlterColumn<string>(
                name: "LargeUnitName",
                table: "cafeteria_items",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BaseUnitName",
                table: "cafeteria_items",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
