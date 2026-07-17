using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssetStockAndPlanVip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalQuantity",
                table: "venue_asset_types",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkingCount",
                table: "venue_asset_types",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "VipSurchargePerHour",
                table: "PricingPlans",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Backfill stock from existing room assignments.
            migrationBuilder.Sql("""
                UPDATE vat
                SET TotalQuantity = x.Qty,
                    WorkingCount = x.Qty
                FROM venue_asset_types vat
                INNER JOIN (
                    SELECT VenueAssetTypeId, SUM(Quantity) AS Qty
                    FROM room_assets
                    GROUP BY VenueAssetTypeId
                ) x ON x.VenueAssetTypeId = vat.Id
                WHERE vat.TotalQuantity = 0 AND x.Qty > 0;

                -- Move room VIP surcharge onto pricing plans for the same branch (max room VIP).
                UPDATE pp
                SET VipSurchargePerHour = x.Vip
                FROM PricingPlans pp
                INNER JOIN (
                    SELECT BranchId, MAX(VipSurchargePerHour) AS Vip
                    FROM Rooms
                    WHERE VipSurchargePerHour > 0 AND IsDeleted = 0
                    GROUP BY BranchId
                ) x ON x.BranchId = pp.BranchId
                WHERE pp.VipSurchargePerHour = 0 AND x.Vip > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalQuantity",
                table: "venue_asset_types");

            migrationBuilder.DropColumn(
                name: "WorkingCount",
                table: "venue_asset_types");

            migrationBuilder.DropColumn(
                name: "VipSurchargePerHour",
                table: "PricingPlans");
        }
    }
}
