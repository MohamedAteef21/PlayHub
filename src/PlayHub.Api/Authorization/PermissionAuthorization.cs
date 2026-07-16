using Microsoft.AspNetCore.Authorization;

namespace PlayHub.Api.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;
    public string Permission { get; }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var isMasterClaim = context.User.FindFirst("is_master")?.Value
            ?? context.User.FindFirst("IsMaster")?.Value;
        if (string.Equals(isMasterClaim, "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.HasClaim("permission", requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public static class PermissionPolicies
{
    public const string AssetsManage = "Assets.Manage";
    public const string SessionsView = "Sessions.View";
    public const string SessionsCreate = "Sessions.Create";
    public const string SessionsPause = "Sessions.Pause";
    public const string SessionsClose = "Sessions.Close";
    public const string SessionsHistory = "Sessions.History";
    public const string CafeteriaSell = "Cafeteria.Sell";
    public const string CafeteriaView = "Cafeteria.View";
    public const string CafeteriaReturn = "Cafeteria.Return";
    public const string InventoryView = "Inventory.View";
    public const string InventoryAdjust = "Inventory.Adjust";
    public const string InventoryManageItems = "Inventory.ManageItems";
    public const string PurchaseOrdersCreate = "PurchaseOrders.Create";
    public const string PurchaseOrdersReceive = "PurchaseOrders.Receive";
    public const string ExpensesAdd = "Expenses.Add";
    public const string ExpensesView = "Expenses.View";
    public const string ReportsView = "Reports.View";
    public const string SettingsManage = "Settings.Manage";
    public const string UsersManage = "Users.Manage";
    public const string CustomersView = "Customers.View";
    public const string CustomersManage = "Customers.Manage";
    public const string OffersManage = "Offers.Manage";
    public const string MasterOnly = "MasterOnly";

    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(AssetsManage, p => p.Requirements.Add(new PermissionRequirement("Assets.Manage")));
        options.AddPolicy(SessionsView, p => p.Requirements.Add(new PermissionRequirement("Sessions.View")));
        options.AddPolicy(SessionsCreate, p => p.Requirements.Add(new PermissionRequirement("Sessions.Create")));
        options.AddPolicy(SessionsPause, p => p.Requirements.Add(new PermissionRequirement("Sessions.Pause")));
        options.AddPolicy(SessionsClose, p => p.Requirements.Add(new PermissionRequirement("Sessions.Close")));
        options.AddPolicy(SessionsHistory, p => p.Requirements.Add(new PermissionRequirement("Sessions.History")));
        options.AddPolicy(CafeteriaSell, p => p.Requirements.Add(new PermissionRequirement("Cafeteria.Sell")));
        options.AddPolicy(CafeteriaView, p => p.Requirements.Add(new PermissionRequirement("Cafeteria.View")));
        options.AddPolicy(CafeteriaReturn, p => p.Requirements.Add(new PermissionRequirement("Cafeteria.Return")));
        options.AddPolicy(InventoryView, p => p.Requirements.Add(new PermissionRequirement("Inventory.View")));
        options.AddPolicy(InventoryAdjust, p => p.Requirements.Add(new PermissionRequirement("Inventory.Adjust")));
        options.AddPolicy(InventoryManageItems, p => p.Requirements.Add(new PermissionRequirement("Inventory.ManageItems")));
        options.AddPolicy(PurchaseOrdersCreate, p => p.Requirements.Add(new PermissionRequirement("PurchaseOrders.Create")));
        options.AddPolicy(PurchaseOrdersReceive, p => p.Requirements.Add(new PermissionRequirement("PurchaseOrders.Receive")));
        options.AddPolicy(ExpensesAdd, p => p.Requirements.Add(new PermissionRequirement("Expenses.Add")));
        options.AddPolicy(ExpensesView, p => p.Requirements.Add(new PermissionRequirement("Expenses.View")));
        options.AddPolicy(ReportsView, p => p.Requirements.Add(new PermissionRequirement("Reports.View")));
        options.AddPolicy(SettingsManage, p => p.Requirements.Add(new PermissionRequirement("Settings.Manage")));
        options.AddPolicy(UsersManage, p => p.Requirements.Add(new PermissionRequirement("Users.Manage")));
        options.AddPolicy(CustomersView, p => p.Requirements.Add(new PermissionRequirement("Customers.View")));
        options.AddPolicy(CustomersManage, p => p.Requirements.Add(new PermissionRequirement("Customers.Manage")));
        options.AddPolicy(OffersManage, p => p.Requirements.Add(new PermissionRequirement("Offers.Manage")));
        options.AddPolicy(MasterOnly, p => p.RequireClaim("is_master", "true"));
    }
}
