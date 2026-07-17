using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CafeteriaItemVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnedStockQuantity",
                table: "SessionCafeteriaLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StockDeductQuantity",
                table: "SessionCafeteriaLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                table: "SessionCafeteriaLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantName",
                table: "SessionCafeteriaLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnedStockQuantity",
                table: "CafeteriaSaleLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StockDeductQuantity",
                table: "CafeteriaSaleLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                table: "CafeteriaSaleLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantName",
                table: "CafeteriaSaleLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CafeteriaItemVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SellPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaItemVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CafeteriaItemVariants_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionCafeteriaLines_VariantId",
                table: "SessionCafeteriaLines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaSaleLines_VariantId",
                table: "CafeteriaSaleLines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaItemVariants_CafeteriaItemId",
                table: "CafeteriaItemVariants",
                column: "CafeteriaItemId");

            // One default variant per existing item (name = item name, price = SellPrice).
            migrationBuilder.Sql("""
                INSERT INTO CafeteriaItemVariants (Id, CafeteriaItemId, Name, SellPrice, IsActive, SortOrder, CreatedAt)
                SELECT NEWID(), ci.Id, ci.Name, ci.SellPrice, 1, 0, SYSUTCDATETIME()
                FROM cafeteria_items ci
                WHERE NOT EXISTS (
                    SELECT 1 FROM CafeteriaItemVariants v WHERE v.CafeteriaItemId = ci.Id
                );
                """);

            migrationBuilder.Sql("""
                UPDATE SessionCafeteriaLines SET StockDeductQuantity = Quantity WHERE StockDeductQuantity = 0;
                UPDATE CafeteriaSaleLines SET StockDeductQuantity = Quantity WHERE StockDeductQuantity = 0;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_CafeteriaSaleLines_CafeteriaItemVariants_VariantId",
                table: "CafeteriaSaleLines",
                column: "VariantId",
                principalTable: "CafeteriaItemVariants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionCafeteriaLines_CafeteriaItemVariants_VariantId",
                table: "SessionCafeteriaLines",
                column: "VariantId",
                principalTable: "CafeteriaItemVariants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CafeteriaSaleLines_CafeteriaItemVariants_VariantId",
                table: "CafeteriaSaleLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionCafeteriaLines_CafeteriaItemVariants_VariantId",
                table: "SessionCafeteriaLines");

            migrationBuilder.DropTable(
                name: "CafeteriaItemVariants");

            migrationBuilder.DropIndex(
                name: "IX_SessionCafeteriaLines_VariantId",
                table: "SessionCafeteriaLines");

            migrationBuilder.DropIndex(
                name: "IX_CafeteriaSaleLines_VariantId",
                table: "CafeteriaSaleLines");

            migrationBuilder.DropColumn(
                name: "ReturnedStockQuantity",
                table: "SessionCafeteriaLines");

            migrationBuilder.DropColumn(
                name: "StockDeductQuantity",
                table: "SessionCafeteriaLines");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "SessionCafeteriaLines");

            migrationBuilder.DropColumn(
                name: "VariantName",
                table: "SessionCafeteriaLines");

            migrationBuilder.DropColumn(
                name: "ReturnedStockQuantity",
                table: "CafeteriaSaleLines");

            migrationBuilder.DropColumn(
                name: "StockDeductQuantity",
                table: "CafeteriaSaleLines");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "CafeteriaSaleLines");

            migrationBuilder.DropColumn(
                name: "VariantName",
                table: "CafeteriaSaleLines");
        }
    }
}
