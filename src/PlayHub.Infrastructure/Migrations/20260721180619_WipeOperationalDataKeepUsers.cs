using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations;

/// <inheritdoc />
public partial class WipeOperationalDataKeepUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // One-time clean slate for production testing after isolation fixes.
        // Keeps: tenants, users, branches, user_branches, user_permissions, permissions,
        //        branch_payment_accounts, master_alert_settings.
        migrationBuilder.Sql("""
            DELETE FROM session_cafeteria_line_add_ons;
            DELETE FROM session_cafeteria_line_ingredient_deducts;
            DELETE FROM session_cafeteria_returns;
            DELETE FROM SessionCafeteriaLines;
            DELETE FROM SessionPauses;

            DELETE FROM cafeteria_sale_line_add_ons;
            DELETE FROM cafeteria_sale_line_ingredient_deducts;
            DELETE FROM CafeteriaReturns;
            DELETE FROM CafeteriaSaleLines;

            DELETE FROM PaymentProofs;
            DELETE FROM InvoicePayments;
            DELETE FROM revenue_entries;
            DELETE FROM invoices;
            DELETE FROM cash_collections;
            DELETE FROM expenses;

            DELETE FROM sessions;
            DELETE FROM CafeteriaSales;

            DELETE FROM cafeteria_variant_recipe_lines;
            DELETE FROM CafeteriaItemVariants;
            DELETE FROM cafeteria_add_ons;
            DELETE FROM stock_voucher_lines;
            DELETE FROM stock_vouchers;
            DELETE FROM PurchaseOrderLines;
            DELETE FROM PurchaseOrders;
            DELETE FROM InventoryMovements;
            DELETE FROM item_unit_conversion_logs;
            DELETE FROM cafeteria_items;
            DELETE FROM inventory_units;

            DELETE FROM room_assets;
            DELETE FROM DeviceControllers;
            DELETE FROM Screens;
            DELETE FROM device_pricing_plans;
            DELETE FROM device_maintenances;
            DELETE FROM Devices;
            DELETE FROM Rooms;
            DELETE FROM venue_asset_types;
            DELETE FROM ControllerTypes;

            DELETE FROM GamingRates;
            DELETE FROM WatchingRates;
            DELETE FROM PricingPlans;

            DELETE FROM wallet_transactions;
            DELETE FROM customer_offers;
            DELETE FROM customers;

            DELETE FROM ExpenseCategories;
            DELETE FROM Notifications;
            DELETE FROM audit_logs;
            DELETE FROM refresh_tokens;

            -- Remove cross-master UserBranch leaks
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
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible data wipe — nothing to restore.
    }
}
