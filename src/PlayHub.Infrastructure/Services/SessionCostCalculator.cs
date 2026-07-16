using System.Text.Json;
using PlayHub.Application.Sessions;
using PlayHub.Domain.Enums;

namespace PlayHub.Infrastructure.Services;

public class SessionCostCalculator : ISessionCostCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public int GetElapsedSeconds(
        SessionStatus status,
        DateTime startedAt,
        int totalPausedSeconds,
        DateTime? activePauseStartedAt,
        DateTime? closedAt = null)
    {
        var end = closedAt ?? DateTime.UtcNow;
        var totalSeconds = (int)Math.Max(0, (end - startedAt).TotalSeconds);

        if (status == SessionStatus.Paused && activePauseStartedAt.HasValue)
            totalSeconds = (int)Math.Max(0, (activePauseStartedAt.Value - startedAt).TotalSeconds);

        return Math.Max(0, totalSeconds - totalPausedSeconds);
    }

    public decimal CalculateTimeCost(
        string rateSnapshotJson,
        SessionMode mode,
        int elapsedSeconds,
        int? controllerCount,
        int? watcherCount,
        bool billingRoundUp)
    {
        var snapshot = JsonSerializer.Deserialize<RateSnapshot>(rateSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("Invalid rate snapshot.");

        if (mode == SessionMode.Gaming && snapshot.PackagePrice is > 0 && snapshot.PackageDurationMinutes is > 0)
            return CalculatePackageCost(snapshot, controllerCount, elapsedSeconds, billingRoundUp);

        var units = GetBillableUnits(snapshot.TimeUnit, elapsedSeconds, billingRoundUp);

        return mode switch
        {
            SessionMode.Gaming => CalculateGamingCost(snapshot, controllerCount, units),
            SessionMode.Watching => CalculateWatchingCost(snapshot, watcherCount, units),
            _ => throw new InvalidOperationException("Unsupported session mode.")
        };
    }

    private static decimal GetBillableUnits(TimeUnit timeUnit, int elapsedSeconds, bool billingRoundUp)
    {
        // Flat per game/match — ignore elapsed time
        if (timeUnit == TimeUnit.PerGame)
            return 1;

        var unitSeconds = timeUnit == TimeUnit.PerHour ? 3600 : 60;
        var units = billingRoundUp
            ? (decimal)Math.Max(1, (int)Math.Ceiling(elapsedSeconds / (double)unitSeconds))
            : elapsedSeconds / (decimal)unitSeconds;

        if (units <= 0) units = billingRoundUp ? 1 : 0;
        return units;
    }

    private static decimal CalculateGamingCost(RateSnapshot snapshot, int? controllerCount, decimal units)
    {
        if (controllerCount is null or <= 0)
            throw new InvalidOperationException("Controller count is required for gaming sessions.");

        var rate = snapshot.GamingRates
            .Where(r => r.ControllerCount == controllerCount.Value)
            .Select(r => r.Rate)
            .FirstOrDefault();

        if (rate <= 0)
            throw new InvalidOperationException($"No gaming rate configured for {controllerCount} controller(s).");

        return rate * units;
    }

    /// <summary>Package plans: flat price covers the package window; extra time billed at the normal rate.</summary>
    private decimal CalculatePackageCost(RateSnapshot snapshot, int? controllerCount, int elapsedSeconds, bool billingRoundUp)
    {
        var packageSeconds = snapshot.PackageDurationMinutes!.Value * 60;
        var overageSeconds = Math.Max(0, elapsedSeconds - packageSeconds);

        if (overageSeconds == 0)
            return snapshot.PackagePrice!.Value;

        var overageUnits = GetBillableUnits(snapshot.TimeUnit, overageSeconds, billingRoundUp);
        return snapshot.PackagePrice!.Value + CalculateGamingCost(snapshot, controllerCount, overageUnits);
    }

    private static decimal CalculateWatchingCost(RateSnapshot snapshot, int? watcherCount, decimal units)
    {
        var rate = snapshot.WatchingRates.FirstOrDefault()?.RatePerPerson ?? 0;
        if (rate <= 0)
            throw new InvalidOperationException("No watching rate configured for this plan.");

        // Default PerPerson for old snapshots that omit WatchingBilling
        var billing = snapshot.WatchingBilling == 0
            ? WatchingBilling.PerPerson
            : snapshot.WatchingBilling;

        if (watcherCount is null or <= 0)
            throw new InvalidOperationException("Watcher count is required for watching sessions.");

        // Per person = flat fee for each watcher for the whole watching session (no time).
        if (billing == WatchingBilling.PerPerson)
            return watcherCount.Value * rate;

        // Per screen = each person added into the room in use, billed by time.
        return watcherCount.Value * rate * units;
    }

    private sealed class RateSnapshot
    {
        public TimeUnit TimeUnit { get; set; }
        public WatchingBilling WatchingBilling { get; set; }
        public int? PackageDurationMinutes { get; set; }
        public decimal? PackagePrice { get; set; }
        public List<GamingRateSnapshot> GamingRates { get; set; } = [];
        public List<WatchingRateSnapshot> WatchingRates { get; set; } = [];
    }

    private sealed class GamingRateSnapshot
    {
        public int ControllerCount { get; set; }
        public decimal Rate { get; set; }
    }

    private sealed class WatchingRateSnapshot
    {
        public decimal RatePerPerson { get; set; }
    }
}
