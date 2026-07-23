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

        // Exact table names as mapped in this database (mixed Pascal / snake_case).
        // Each DELETE is guarded so missing tables never abort the wipe.
        string[] tables =
        [
            "session_cafeteria_line_add_ons",
            "session_cafeteria_line_ingredient_deducts",
            "session_cafeteria_returns",
            "SessionCafeteriaLines",
            "SessionPauses",
            "cafeteria_sale_line_add_ons",
            "cafeteria_sale_line_ingredient_deducts",
            "CafeteriaReturns",
            "CafeteriaSaleLines",
            "cafeteria_hold_line_add_ons",
            "cafeteria_hold_line_ingredient_deducts",
            "cafeteria_hold_lines",
            "cafeteria_holds",
            "PaymentProofs",
            "invoice_payments",
            "revenue_entries",
            "invoices",
            "cash_collections",
            "expenses",
            "sessions",
            "CafeteriaSales",
            "cafeteria_variant_recipe_lines",
            "CafeteriaItemVariants",
            "cafeteria_add_ons",
            "stock_voucher_lines",
            "stock_vouchers",
            "PurchaseOrderLines",
            "PurchaseOrders",
            "InventoryMovements",
            "item_unit_conversion_logs",
            "cafeteria_items",
            "inventory_units",
            "room_assets",
            "session_equipment_allocations",
            "branch_equipment",
            "DeviceControllers",
            "Screens",
            "device_pricing_plans",
            "device_maintenances",
            "Devices",
            "Rooms",
            "venue_asset_types",
            "ControllerTypes",
            "GamingRates",
            "WatchingRates",
            "PricingPlans",
            "wallet_transactions",
            "customer_offers",
            "customers",
            "ExpenseCategories",
            "Notifications",
            "audit_logs",
            "refresh_tokens",
        ];

        var deleted = new Dictionary<string, int>();
        foreach (var table in tables)
        {
            deleted[table] = await _db.Database.ExecuteSqlRawAsync($"""
                IF OBJECT_ID(N'{table}', N'U') IS NOT NULL
                    DELETE FROM [{table}];
                """, ct);
        }

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
