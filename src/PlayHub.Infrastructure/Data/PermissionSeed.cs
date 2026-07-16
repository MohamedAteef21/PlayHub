using Microsoft.EntityFrameworkCore;
using PlayHub.Domain.Entities;

namespace PlayHub.Infrastructure.Data;

public static class PermissionSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        var permissions = new List<Permission>
        {
            Create("550e8400-e29b-41d4-a716-446655440001", "Sessions.Create", "Sessions", "Create", "Open a new gaming or watching session"),
            Create("550e8400-e29b-41d4-a716-446655440002", "Sessions.Pause", "Sessions", "Pause", "Pause or resume a session"),
            Create("550e8400-e29b-41d4-a716-446655440003", "Sessions.Close", "Sessions", "Close", "Close a session and generate invoice"),
            Create("550e8400-e29b-41d4-a716-446655440004", "Sessions.View", "Sessions", "View", "View live floor and active sessions"),
            Create("550e8400-e29b-41d4-a716-446655440005", "Cafeteria.Sell", "Cafeteria", "Sell", "Dispense cafeteria sales (walk-in or session), including unit choice"),
            Create("550e8400-e29b-41d4-a716-446655440006", "Cafeteria.Return", "Cafeteria", "Return", "Process cafeteria returns"),
            Create("550e8400-e29b-41d4-a716-446655440007", "Cafeteria.View", "Cafeteria", "View", "View cafeteria catalog and sales"),
            Create("550e8400-e29b-41d4-a716-446655440008", "Inventory.View", "Inventory", "View", "View inventory levels, units, and vouchers"),
            Create("550e8400-e29b-41d4-a716-446655440009", "Inventory.Adjust", "Inventory", "Adjust", "Create stock-in / count / settlement vouchers and manual stock adjust"),
            Create("550e8400-e29b-41d4-a716-446655440019", "Inventory.ManageItems", "Inventory", "ManageItems", "Create/edit items, units & conversion, activate/deactivate, soft-delete"),
            Create("550e8400-e29b-41d4-a716-446655440020", "Sessions.History", "Sessions", "History", "View full session open/close history (who and when)"),
            Create("550e8400-e29b-41d4-a716-446655440010", "PurchaseOrders.Create", "PurchaseOrders", "Create", "Create purchase orders"),
            Create("550e8400-e29b-41d4-a716-446655440011", "PurchaseOrders.Receive", "PurchaseOrders", "Receive", "Receive purchase orders"),
            Create("550e8400-e29b-41d4-a716-446655440012", "Expenses.Add", "Expenses", "Add", "Record expenses"),
            Create("550e8400-e29b-41d4-a716-446655440013", "Expenses.View", "Expenses", "View", "View expenses"),
            Create("550e8400-e29b-41d4-a716-446655440014", "Reports.View", "Reports", "View", "View financial and usage reports"),
            Create("550e8400-e29b-41d4-a716-446655440015", "Assets.Manage", "Assets", "Manage", "Manage rooms, devices, and controllers"),
            Create("550e8400-e29b-41d4-a716-446655440016", "Settings.Manage", "Settings", "Manage", "Manage pricing plans and tenant settings"),
            Create("550e8400-e29b-41d4-a716-446655440017", "CanEditClosedRecords", "Security", "EditClosed", "Edit or delete closed financial records"),
            Create("550e8400-e29b-41d4-a716-446655440018", "Users.Manage", "Users", "Manage", "Create and manage sub-users"),
            Create("550e8400-e29b-41d4-a716-446655440021", "Customers.View", "Customers", "View", "View and search customers"),
            Create("550e8400-e29b-41d4-a716-446655440022", "Customers.Manage", "Customers", "Manage", "Create, update, and delete customers; send WhatsApp messages"),
            Create("550e8400-e29b-41d4-a716-446655440023", "Offers.Manage", "Offers", "Manage", "Create and manage customer offers"),
        };

        modelBuilder.Entity<Permission>().HasData(permissions);
    }

    private static Permission Create(string id, string code, string module, string action, string description) =>
        new()
        {
            Id = Guid.Parse(id),
            Code = code,
            Module = module,
            Action = action,
            Description = description,
            IsSystem = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
}
