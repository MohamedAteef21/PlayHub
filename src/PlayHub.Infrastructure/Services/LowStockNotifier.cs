using PlayHub.Application.Alerts;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class LowStockNotifier
{
    private readonly TenantContext _tenantContext;
    private readonly IAlertDispatcher _alerts;

    public LowStockNotifier(TenantContext tenantContext, IAlertDispatcher alerts)
    {
        _tenantContext = tenantContext;
        _alerts = alerts;
    }

    public async Task CheckAndNotifyAsync(Domain.Entities.CafeteriaItem item, CancellationToken ct = default)
    {
        if (item.CurrentQuantity > item.MinThreshold)
            return;

        await _alerts.DispatchToMastersAsync(
            _tenantContext.TenantId,
            NotificationType.LowStock,
            "Low stock alert",
            "تنبيه مخزون منخفض",
            $"{item.Name} is at {item.CurrentQuantity} units (minimum: {item.MinThreshold}). Reorder soon.",
            $"{item.NameAr ?? item.Name}: الكمية {item.CurrentQuantity} (الحد الأدنى: {item.MinThreshold}). جيب تاني قريب.",
            "CafeteriaItem",
            item.Id,
            ct);
    }
}
