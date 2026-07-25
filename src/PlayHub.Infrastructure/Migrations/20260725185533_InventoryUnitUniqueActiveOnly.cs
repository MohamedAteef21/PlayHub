using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryUnitUniqueActiveOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_units_TenantId_Name",
                table: "inventory_units");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_units_TenantId_Name",
                table: "inventory_units",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_units_TenantId_Name",
                table: "inventory_units");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_units_TenantId_Name",
                table: "inventory_units",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
