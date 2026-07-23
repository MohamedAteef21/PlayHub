using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryUnitOwnerUniqueAndTransferTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Soft-delete duplicate active units so the new per-owner unique index can apply.
            migrationBuilder.Sql("""
                ;WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY TenantId, OwnerUserId, Name
                               ORDER BY CreatedAt, Id) AS rn
                    FROM inventory_units
                    WHERE IsDeleted = 0
                )
                UPDATE u
                SET IsDeleted = 1,
                    IsActive = 0,
                    DeletedAt = SYSUTCDATETIME()
                FROM inventory_units u
                INNER JOIN ranked r ON r.Id = u.Id
                WHERE r.rn > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_inventory_units_TenantId_Name",
                table: "inventory_units");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_units_TenantId_OwnerUserId_Name",
                table: "inventory_units",
                columns: new[] { "TenantId", "OwnerUserId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_units_TenantId_OwnerUserId_Name",
                table: "inventory_units");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_units_TenantId_Name",
                table: "inventory_units",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
