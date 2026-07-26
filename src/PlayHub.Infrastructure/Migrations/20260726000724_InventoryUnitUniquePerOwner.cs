using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryUnitUniquePerOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Soft-delete duplicate active units within the same owner catalog.
            migrationBuilder.Sql("""
                ;WITH ranked AS (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [TenantId], [OwnerUserId], [Name]
                               ORDER BY [CreatedAt] DESC, [Id] DESC
                           ) AS rn
                    FROM [inventory_units]
                    WHERE [IsDeleted] = 0
                )
                UPDATE u
                SET u.[IsDeleted] = 1,
                    u.[IsActive] = 0,
                    u.[DeletedAt] = SYSUTCDATETIME()
                FROM [inventory_units] u
                INNER JOIN ranked r ON r.[Id] = u.[Id]
                WHERE r.rn > 1;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_inventory_units_TenantId_Name'
                      AND object_id = OBJECT_ID(N'[inventory_units]')
                )
                    DROP INDEX [IX_inventory_units_TenantId_Name] ON [inventory_units];
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_inventory_units_TenantId_OwnerUserId_Name'
                      AND object_id = OBJECT_ID(N'[inventory_units]')
                )
                    CREATE UNIQUE INDEX [IX_inventory_units_TenantId_OwnerUserId_Name]
                    ON [inventory_units] ([TenantId], [OwnerUserId], [Name])
                    WHERE [IsDeleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_inventory_units_TenantId_OwnerUserId_Name'
                      AND object_id = OBJECT_ID(N'[inventory_units]')
                )
                    DROP INDEX [IX_inventory_units_TenantId_OwnerUserId_Name] ON [inventory_units];
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_inventory_units_TenantId_Name'
                      AND object_id = OBJECT_ID(N'[inventory_units]')
                )
                    CREATE UNIQUE INDEX [IX_inventory_units_TenantId_Name]
                    ON [inventory_units] ([TenantId], [Name])
                    WHERE [IsDeleted] = 0;
                """);
        }
    }
}
