using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Alerts;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Jobs;

/// <summary>Weekly: remind masters about devices still in maintenance for 7+ days.</summary>
public class DeviceMaintenanceReminderJob
{
    private readonly PlayHubDbContext _db;
    private readonly IAlertDispatcher _alerts;

    public DeviceMaintenanceReminderJob(PlayHubDbContext db, IAlertDispatcher alerts)
    {
        _db = db;
        _alerts = alerts;
    }

    public async Task RunAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var open = await _db.DeviceMaintenances
            .IgnoreQueryFilters()
            .Include(m => m.Device)
            .Where(m => !m.IsDeleted && m.CompletedAt == null && m.StartedAt <= cutoff)
            .ToListAsync();

        foreach (var group in open.GroupBy(m => m.TenantId))
        {
            var linesEn = string.Join("\n", group.Select(m =>
                $"- {m.Device.Name} ({m.Device.Identifier}): {m.Reason} since {m.StartedAt:yyyy-MM-dd}"));
            var linesAr = string.Join("\n", group.Select(m =>
                $"- {m.Device.Name} ({m.Device.Identifier}): {m.Reason} من {m.StartedAt:yyyy-MM-dd}"));

            await _alerts.DispatchToMastersAsync(
                group.Key,
                NotificationType.DeviceMaintenanceReminder,
                "Devices still in maintenance",
                "أجهزة لسه في الصيانة",
                $"These devices have been in maintenance for a week or more:\n{linesEn}",
                $"الأجهزة دي في الصيانة من أسبوع أو أكتر:\n{linesAr}");
        }
    }
}
