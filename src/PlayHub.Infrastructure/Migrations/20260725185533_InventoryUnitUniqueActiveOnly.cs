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
            // Production DBs may already lack this index (or use a different name) — drop safely.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_inventory_units_TenantId_Name'
                      AND object_id = OBJECT_ID(N'[inventory_units]')
                )
                    DROP INDEX [IX_inventory_units_TenantId_Name] ON [inventory_units];
                """);

            // Production may have duplicate active unit names (re-adds). Soft-delete extras before unique index.
            migrationBuilder.Sql("""
                ;WITH ranked AS (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [TenantId], [Name]
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                    WHERE name = N'IX_inventory_units_TenantId_Name'
                      AND object_id = OBJECT_ID(N'[inventory_units]')
                )
                    CREATE UNIQUE INDEX [IX_inventory_units_TenantId_Name]
                    ON [inventory_units] ([TenantId], [Name]);
                """);
        }
    }
}
