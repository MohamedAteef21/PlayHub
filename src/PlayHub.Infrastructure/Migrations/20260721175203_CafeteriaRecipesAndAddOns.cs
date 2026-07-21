using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CafeteriaRecipesAndAddOns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cafeteria_items_BranchId",
                table: "cafeteria_items");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CafeteriaItemVariants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<short>(
                name: "Kind",
                table: "cafeteria_items",
                type: "smallint",
                nullable: false,
                defaultValue: (short)3);

            migrationBuilder.Sql("UPDATE cafeteria_items SET Kind = 3 WHERE Kind = 0;");

            migrationBuilder.CreateTable(
                name: "cafeteria_add_ons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SellPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WarehouseItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeductQuantity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_add_ons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_add_ons_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_add_ons_cafeteria_items_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "cafeteria_sale_line_ingredient_deducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "int", nullable: false),
                    WasSkipped = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_sale_line_ingredient_deducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_sale_line_ingredient_deducts_CafeteriaSaleLines_SaleLineId",
                        column: x => x.SaleLineId,
                        principalTable: "CafeteriaSaleLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_sale_line_ingredient_deducts_cafeteria_items_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "cafeteria_variant_recipe_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_variant_recipe_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_variant_recipe_lines_CafeteriaItemVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "CafeteriaItemVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_variant_recipe_lines_cafeteria_items_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "session_cafeteria_line_ingredient_deducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionCafeteriaLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "int", nullable: false),
                    WasSkipped = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_cafeteria_line_ingredient_deducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_cafeteria_line_ingredient_deducts_SessionCafeteriaLines_SessionCafeteriaLineId",
                        column: x => x.SessionCafeteriaLineId,
                        principalTable: "SessionCafeteriaLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_session_cafeteria_line_ingredient_deducts_cafeteria_items_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "cafeteria_sale_line_add_ons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddOnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StockDeductQuantity = table.Column<int>(type: "int", nullable: false),
                    ReturnedStockQuantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_sale_line_add_ons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_sale_line_add_ons_CafeteriaSaleLines_SaleLineId",
                        column: x => x.SaleLineId,
                        principalTable: "CafeteriaSaleLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cafeteria_sale_line_add_ons_cafeteria_add_ons_AddOnId",
                        column: x => x.AddOnId,
                        principalTable: "cafeteria_add_ons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "session_cafeteria_line_add_ons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionCafeteriaLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddOnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StockDeductQuantity = table.Column<int>(type: "int", nullable: false),
                    ReturnedStockQuantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_cafeteria_line_add_ons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_cafeteria_line_add_ons_SessionCafeteriaLines_SessionCafeteriaLineId",
                        column: x => x.SessionCafeteriaLineId,
                        principalTable: "SessionCafeteriaLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_session_cafeteria_line_add_ons_cafeteria_add_ons_AddOnId",
                        column: x => x.AddOnId,
                        principalTable: "cafeteria_add_ons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_items_BranchId_Kind_IsActive",
                table: "cafeteria_items",
                columns: new[] { "BranchId", "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_add_ons_BranchId",
                table: "cafeteria_add_ons",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_add_ons_TenantId_BranchId_IsActive",
                table: "cafeteria_add_ons",
                columns: new[] { "TenantId", "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_add_ons_WarehouseItemId",
                table: "cafeteria_add_ons",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_sale_line_add_ons_AddOnId",
                table: "cafeteria_sale_line_add_ons",
                column: "AddOnId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_sale_line_add_ons_SaleLineId",
                table: "cafeteria_sale_line_add_ons",
                column: "SaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_sale_line_ingredient_deducts_SaleLineId",
                table: "cafeteria_sale_line_ingredient_deducts",
                column: "SaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_sale_line_ingredient_deducts_WarehouseItemId",
                table: "cafeteria_sale_line_ingredient_deducts",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_variant_recipe_lines_VariantId_WarehouseItemId",
                table: "cafeteria_variant_recipe_lines",
                columns: new[] { "VariantId", "WarehouseItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_variant_recipe_lines_WarehouseItemId",
                table: "cafeteria_variant_recipe_lines",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_session_cafeteria_line_add_ons_AddOnId",
                table: "session_cafeteria_line_add_ons",
                column: "AddOnId");

            migrationBuilder.CreateIndex(
                name: "IX_session_cafeteria_line_add_ons_SessionCafeteriaLineId",
                table: "session_cafeteria_line_add_ons",
                column: "SessionCafeteriaLineId");

            migrationBuilder.CreateIndex(
                name: "IX_session_cafeteria_line_ingredient_deducts_SessionCafeteriaLineId",
                table: "session_cafeteria_line_ingredient_deducts",
                column: "SessionCafeteriaLineId");

            migrationBuilder.CreateIndex(
                name: "IX_session_cafeteria_line_ingredient_deducts_WarehouseItemId",
                table: "session_cafeteria_line_ingredient_deducts",
                column: "WarehouseItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cafeteria_sale_line_add_ons");

            migrationBuilder.DropTable(
                name: "cafeteria_sale_line_ingredient_deducts");

            migrationBuilder.DropTable(
                name: "cafeteria_variant_recipe_lines");

            migrationBuilder.DropTable(
                name: "session_cafeteria_line_add_ons");

            migrationBuilder.DropTable(
                name: "session_cafeteria_line_ingredient_deducts");

            migrationBuilder.DropTable(
                name: "cafeteria_add_ons");

            migrationBuilder.DropIndex(
                name: "IX_cafeteria_items_BranchId_Kind_IsActive",
                table: "cafeteria_items");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "cafeteria_items");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CafeteriaItemVariants",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_items_BranchId",
                table: "cafeteria_items",
                column: "BranchId");
        }
    }
}
