using Microsoft.EntityFrameworkCore;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

/// <summary>
/// Hard-deletes operational data while keeping tenants, users, branches, and auth assignments.
/// SuperAdmin maintenance only.
/// </summary>
public class DataWipeService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenant;

    public DataWipeService(PlayHubDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<object> WipeOperationalDataAsync(CancellationToken ct = default)
    {
        if (!_tenant.IsSuperAdmin)
            throw new UnauthorizedAccessException("Only Super Admin can wipe operational data.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var deleted = new Dictionary<string, int>();

        async Task Track(string name, string sql)
        {
            deleted[name] = await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }

        // Session cafeteria children
        await Track("session_cafeteria_line_add_ons", "DELETE FROM session_cafeteria_line_add_ons");
        await Track("session_cafeteria_line_ingredient_deducts", "DELETE FROM session_cafeteria_line_ingredient_deducts");
        await Track("session_cafeteria_returns", "DELETE FROM session_cafeteria_returns");
        await Track("SessionCafeteriaLines", "DELETE FROM SessionCafeteriaLines");
        await Track("SessionPauses", "DELETE FROM SessionPauses");

        // Cafeteria sale children
        await Track("cafeteria_sale_line_add_ons", "DELETE FROM cafeteria_sale_line_add_ons");
        await Track("cafeteria_sale_line_ingredient_deducts", "DELETE FROM cafeteria_sale_line_ingredient_deducts");
        await Track("CafeteriaReturns", "DELETE FROM CafeteriaReturns");
        await Track("CafeteriaSaleLines", "DELETE FROM CafeteriaSaleLines");

        // Finance children — detach invoice FKs from sessions/sales before delete
        await Track("PaymentProofs", "DELETE FROM PaymentProofs");
        await Track("InvoicePayments", "DELETE FROM InvoicePayments");
        await Track("revenue_entries", "DELETE FROM revenue_entries");
        await Track("invoices", "DELETE FROM invoices");
        await Track("cash_collections", "DELETE FROM cash_collections");
        await Track("expenses", "DELETE FROM expenses");

        // Sessions / sales
        await Track("sessions", "DELETE FROM sessions");
        await Track("CafeteriaSales", "DELETE FROM CafeteriaSales");

        // Inventory / cafeteria catalog
        await Track("cafeteria_variant_recipe_lines", "DELETE FROM cafeteria_variant_recipe_lines");
        await Track("CafeteriaItemVariants", "DELETE FROM CafeteriaItemVariants");
        await Track("cafeteria_add_ons", "DELETE FROM cafeteria_add_ons");
        await Track("stock_voucher_lines", "DELETE FROM stock_voucher_lines");
        await Track("stock_vouchers", "DELETE FROM stock_vouchers");
        await Track("PurchaseOrderLines", "DELETE FROM PurchaseOrderLines");
        await Track("PurchaseOrders", "DELETE FROM PurchaseOrders");
        await Track("InventoryMovements", "DELETE FROM InventoryMovements");
        await Track("item_unit_conversion_logs", "DELETE FROM item_unit_conversion_logs");
        await Track("cafeteria_items", "DELETE FROM cafeteria_items");
        await Track("inventory_units", "DELETE FROM inventory_units");

        // Assets / devices
        await Track("room_assets", "DELETE FROM room_assets");
        await Track("DeviceControllers", "DELETE FROM DeviceControllers");
        await Track("Screens", "DELETE FROM Screens");
        await Track("device_pricing_plans", "DELETE FROM device_pricing_plans");
        await Track("device_maintenances", "DELETE FROM device_maintenances");
        await Track("Devices", "DELETE FROM Devices");
        await Track("Rooms", "DELETE FROM Rooms");
        await Track("venue_asset_types", "DELETE FROM venue_asset_types");
        await Track("ControllerTypes", "DELETE FROM ControllerTypes");

        // Pricing
        await Track("GamingRates", "DELETE FROM GamingRates");
        await Track("WatchingRates", "DELETE FROM WatchingRates");
        await Track("PricingPlans", "DELETE FROM PricingPlans");

        // Customers / offers / wallet
        await Track("wallet_transactions", "DELETE FROM wallet_transactions");
        await Track("customer_offers", "DELETE FROM customer_offers");
        await Track("customers", "DELETE FROM customers");

        // Misc
        await Track("ExpenseCategories", "DELETE FROM ExpenseCategories");
        await Track("Notifications", "DELETE FROM Notifications");
        await Track("audit_logs", "DELETE FROM audit_logs");
        await Track("refresh_tokens", "DELETE FROM refresh_tokens");

        // Clean stray UserBranch rows that point at another master's branch
        deleted["user_branches_cross_owner"] = await _db.Database.ExecuteSqlRawAsync("""
            DELETE ub FROM user_branches ub
            INNER JOIN users u ON u.Id = ub.UserId
            INNER JOIN branches b ON b.Id = ub.BranchId
            WHERE u.IsMaster = 1
              AND b.OwnerUserId IS NOT NULL
              AND b.OwnerUserId <> u.Id
            """, ct);

        deleted["user_branches_staff_cross_owner"] = await _db.Database.ExecuteSqlRawAsync("""
            DELETE ub FROM user_branches ub
            INNER JOIN users u ON u.Id = ub.UserId
            INNER JOIN branches b ON b.Id = ub.BranchId
            WHERE u.IsMaster = 0
              AND u.ParentUserId IS NOT NULL
              AND b.OwnerUserId IS NOT NULL
              AND b.OwnerUserId <> u.ParentUserId
            """, ct);

        await tx.CommitAsync(ct);

        return new
        {
            message = "Operational data wiped. Users, tenants, branches, and permissions kept.",
            deleted
        };
    }
}
