using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations;

/// <inheritdoc />
public partial class WipeOperationalDataKeepUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // One-time clean slate. Keeps tenants/users/branches/permissions/alerts/payment accounts.
        // Each DELETE is guarded so missing tables never abort MigrateAsync.
        migrationBuilder.Sql("""
            DECLARE @sql nvarchar(max) = N'';

            DECLARE @tables TABLE (Name sysname);
            INSERT INTO @tables (Name) VALUES
            (N'session_cafeteria_line_add_ons'),
            (N'session_cafeteria_line_ingredient_deducts'),
            (N'session_cafeteria_returns'),
            (N'SessionCafeteriaLines'),
            (N'SessionPauses'),
            (N'cafeteria_sale_line_add_ons'),
            (N'cafeteria_sale_line_ingredient_deducts'),
            (N'CafeteriaReturns'),
            (N'CafeteriaSaleLines'),
            (N'PaymentProofs'),
            (N'InvoicePayments'),
            (N'revenue_entries'),
            (N'invoices'),
            (N'cash_collections'),
            (N'expenses'),
            (N'sessions'),
            (N'CafeteriaSales'),
            (N'cafeteria_variant_recipe_lines'),
            (N'CafeteriaItemVariants'),
            (N'cafeteria_add_ons'),
            (N'stock_voucher_lines'),
            (N'stock_vouchers'),
            (N'PurchaseOrderLines'),
            (N'PurchaseOrders'),
            (N'InventoryMovements'),
            (N'item_unit_conversion_logs'),
            (N'cafeteria_items'),
            (N'inventory_units'),
            (N'room_assets'),
            (N'DeviceControllers'),
            (N'Screens'),
            (N'device_pricing_plans'),
            (N'device_maintenances'),
            (N'Devices'),
            (N'Rooms'),
            (N'venue_asset_types'),
            (N'ControllerTypes'),
            (N'GamingRates'),
            (N'WatchingRates'),
            (N'PricingPlans'),
            (N'wallet_transactions'),
            (N'customer_offers'),
            (N'customers'),
            (N'ExpenseCategories'),
            (N'Notifications'),
            (N'audit_logs'),
            (N'refresh_tokens');

            DECLARE @t sysname;
            DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @tables;
            OPEN c;
            FETCH NEXT FROM c INTO @t;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                IF OBJECT_ID(QUOTENAME(@t), 'U') IS NOT NULL
                BEGIN
                    SET @sql = N'DELETE FROM ' + QUOTENAME(@t) + N';';
                    EXEC sp_executesql @sql;
                END
                FETCH NEXT FROM c INTO @t;
            END
            CLOSE c;
            DEALLOCATE c;

            -- Remove cross-master UserBranch leaks
            IF OBJECT_ID(N'[user_branches]', 'U') IS NOT NULL
               AND OBJECT_ID(N'[users]', 'U') IS NOT NULL
               AND OBJECT_ID(N'[branches]', 'U') IS NOT NULL
            BEGIN
                DELETE ub FROM user_branches ub
                INNER JOIN users u ON u.Id = ub.UserId
                INNER JOIN branches b ON b.Id = ub.BranchId
                WHERE u.IsMaster = 1
                  AND b.OwnerUserId IS NOT NULL
                  AND b.OwnerUserId <> u.Id;

                DELETE ub FROM user_branches ub
                INNER JOIN users u ON u.Id = ub.UserId
                INNER JOIN branches b ON b.Id = ub.BranchId
                WHERE u.IsMaster = 0
                  AND u.ParentUserId IS NOT NULL
                  AND b.OwnerUserId IS NOT NULL
                  AND b.OwnerUserId <> u.ParentUserId;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible data wipe — nothing to restore.
    }
}
