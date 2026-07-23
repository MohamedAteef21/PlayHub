using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlayHub.Application.Accounting;
using PlayHub.Application.Assets;
using PlayHub.Application.Audit;
using PlayHub.Application.Auth;
using PlayHub.Application.Alerts;
using PlayHub.Application.Branches;
using PlayHub.Application.Cafeteria;
using PlayHub.Application.Common;
using PlayHub.Application.Customers;
using PlayHub.Application.Inventory;
using PlayHub.Application.Notifications;
using PlayHub.Application.Offers;
using PlayHub.Application.Platform;
using PlayHub.Application.Pricing;
using PlayHub.Application.PurchaseOrders;
using PlayHub.Application.Receivables;
using PlayHub.Application.Reports;
using PlayHub.Application.Sessions;
using PlayHub.Application.Users;
using PlayHub.Application.WhatsApp;
using PlayHub.Infrastructure.Auth;
using PlayHub.Infrastructure.Data;
using PlayHub.Infrastructure.Services;

namespace PlayHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<IBranchProvider>(sp => sp.GetRequiredService<TenantContext>());

        services.AddDbContext<PlayHubDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("HrConnection")));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<ISessionCostCalculator, SessionCostCalculator>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<BillingService>();
        services.AddScoped<LowStockNotifier>();
        services.AddScoped<ICafeteriaService, CafeteriaService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IInventoryUnitService, InventoryUnitService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IReceivableService, ReceivableService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IReportsService, ReportsService>();
        services.AddScoped<DataWipeService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<IAlertDispatcher, AlertDispatcher>();
        services.AddScoped<IAlertSettingsService, AlertSettingsService>();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<IDeviceMaintenanceService, DeviceMaintenanceService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddHttpClient("WhatsApp", client =>
        {
            // Chromium / QR bootstrap can exceed 30s on first connect
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddScoped<IWhatsAppService, WhatsAppService>();
        services.AddScoped<Jobs.SubscriptionExpiryJob>();
        services.AddScoped<Jobs.DeviceMaintenanceReminderJob>();

        return services;
    }
}
