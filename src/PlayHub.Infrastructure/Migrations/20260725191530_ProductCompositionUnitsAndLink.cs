using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductCompositionUnitsAndLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "Unit",
                table: "cafeteria_variant_recipe_lines",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseSellPrice",
                table: "cafeteria_items",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LargeSellPrice",
                table: "cafeteria_items",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedWarehouseItemId",
                table: "cafeteria_items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "DeductUnit",
                table: "cafeteria_add_ons",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_items_LinkedWarehouseItemId",
                table: "cafeteria_items",
                column: "LinkedWarehouseItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_cafeteria_items_cafeteria_items_LinkedWarehouseItemId",
                table: "cafeteria_items",
                column: "LinkedWarehouseItemId",
                principalTable: "cafeteria_items",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cafeteria_items_cafeteria_items_LinkedWarehouseItemId",
                table: "cafeteria_items");

            migrationBuilder.DropIndex(
                name: "IX_cafeteria_items_LinkedWarehouseItemId",
                table: "cafeteria_items");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "cafeteria_variant_recipe_lines");

            migrationBuilder.DropColumn(
                name: "BaseSellPrice",
                table: "cafeteria_items");

            migrationBuilder.DropColumn(
                name: "LargeSellPrice",
                table: "cafeteria_items");

            migrationBuilder.DropColumn(
                name: "LinkedWarehouseItemId",
                table: "cafeteria_items");

            migrationBuilder.DropColumn(
                name: "DeductUnit",
                table: "cafeteria_add_ons");
        }
    }
}
