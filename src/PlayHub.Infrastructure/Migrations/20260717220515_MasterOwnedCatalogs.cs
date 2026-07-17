using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MasterOwnedCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "venue_asset_types",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "inventory_units",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "ExpenseCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "customer_offers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "ControllerTypes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_venue_asset_types_OwnerUserId",
                table: "venue_asset_types",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_units_OwnerUserId",
                table: "inventory_units",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_OwnerUserId",
                table: "ExpenseCategories",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_offers_OwnerUserId",
                table: "customer_offers",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ControllerTypes_OwnerUserId",
                table: "ControllerTypes",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ControllerTypes_users_OwnerUserId",
                table: "ControllerTypes",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_customer_offers_users_OwnerUserId",
                table: "customer_offers",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseCategories_users_OwnerUserId",
                table: "ExpenseCategories",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_units_users_OwnerUserId",
                table: "inventory_units",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_venue_asset_types_users_OwnerUserId",
                table: "venue_asset_types",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id");

            // Backfill: assign catalog rows to the branch owner that uses them most.
            migrationBuilder.Sql("""
                UPDATE vat
                SET OwnerUserId = x.OwnerUserId
                FROM venue_asset_types vat
                CROSS APPLY (
                    SELECT TOP 1 b.OwnerUserId
                    FROM room_assets ra
                    INNER JOIN Rooms r ON r.Id = ra.RoomId
                    INNER JOIN branches b ON b.Id = r.BranchId
                    WHERE ra.VenueAssetTypeId = vat.Id AND b.OwnerUserId IS NOT NULL
                    GROUP BY b.OwnerUserId
                    ORDER BY COUNT(*) DESC
                ) x
                WHERE vat.OwnerUserId IS NULL;

                UPDATE ct
                SET OwnerUserId = x.OwnerUserId
                FROM ControllerTypes ct
                CROSS APPLY (
                    SELECT TOP 1 b.OwnerUserId
                    FROM DeviceControllers dc
                    INNER JOIN Devices d ON d.Id = dc.DeviceId
                    INNER JOIN branches b ON b.Id = d.BranchId
                    WHERE dc.ControllerTypeId = ct.Id AND b.OwnerUserId IS NOT NULL
                    GROUP BY b.OwnerUserId
                    ORDER BY COUNT(*) DESC
                ) x
                WHERE ct.OwnerUserId IS NULL;

                UPDATE iu
                SET OwnerUserId = x.OwnerUserId
                FROM inventory_units iu
                CROSS APPLY (
                    SELECT TOP 1 b.OwnerUserId
                    FROM cafeteria_items ci
                    INNER JOIN branches b ON b.Id = ci.BranchId
                    WHERE b.OwnerUserId IS NOT NULL
                      AND (ci.BaseUnitName = iu.Name OR ci.LargeUnitName = iu.Name)
                    GROUP BY b.OwnerUserId
                    ORDER BY COUNT(*) DESC
                ) x
                WHERE iu.OwnerUserId IS NULL;

                UPDATE ec
                SET OwnerUserId = x.OwnerUserId
                FROM ExpenseCategories ec
                CROSS APPLY (
                    SELECT TOP 1 b.OwnerUserId
                    FROM expenses e
                    INNER JOIN branches b ON b.Id = e.BranchId
                    WHERE e.CategoryId = ec.Id AND b.OwnerUserId IS NOT NULL
                    GROUP BY b.OwnerUserId
                    ORDER BY COUNT(*) DESC
                ) x
                WHERE ec.OwnerUserId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ControllerTypes_users_OwnerUserId",
                table: "ControllerTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_customer_offers_users_OwnerUserId",
                table: "customer_offers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseCategories_users_OwnerUserId",
                table: "ExpenseCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_inventory_units_users_OwnerUserId",
                table: "inventory_units");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_asset_types_users_OwnerUserId",
                table: "venue_asset_types");

            migrationBuilder.DropIndex(
                name: "IX_venue_asset_types_OwnerUserId",
                table: "venue_asset_types");

            migrationBuilder.DropIndex(
                name: "IX_inventory_units_OwnerUserId",
                table: "inventory_units");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_OwnerUserId",
                table: "ExpenseCategories");

            migrationBuilder.DropIndex(
                name: "IX_customer_offers_OwnerUserId",
                table: "customer_offers");

            migrationBuilder.DropIndex(
                name: "IX_ControllerTypes_OwnerUserId",
                table: "ControllerTypes");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "venue_asset_types");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "inventory_units");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "customer_offers");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "ControllerTypes");
        }
    }
}
