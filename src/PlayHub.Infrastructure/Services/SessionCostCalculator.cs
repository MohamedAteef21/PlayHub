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
        RateSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<RateSnapshot>(rateSnapshotJson, JsonOptions);
        }
        catch (JsonException)
        {
            snapshot = null;
        }

        // Corrupt/legacy snapshot: bill 0 instead of failing every session screen.
        if (snapshot is null)
            return 0;

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

    /// <summary>Rates are stored per pricing tier: 1 = single (1-2 controllers), 2 = couple (3-4 controllers).</summary>
    public static int GamingRateTier(int controllerCount) => controllerCount <= 2 ? 1 : 2;

    private static decimal CalculateGamingCost(RateSnapshot snapshot, int? controllerCount, decimal units)
    {
        // Misconfigured plan / legacy session: bill 0 for the time instead of breaking
        // the whole active-sessions list (rates are validated at session open now).
        if (controllerCount is null or <= 0)
            return 0;

        var tier = GamingRateTier(controllerCount.Value);
        var rate = snapshot.GamingRates
            .Where(r => r.ControllerCount == tier)
            .Select(r => r.Rate)
            .FirstOrDefault();

        if (rate <= 0)
            return 0;

        return rate * units;
    }

    /// <summary>Package plans: flat single/couple price covers the whole package — no hourly overage.</summary>
    private decimal CalculatePackageCost(RateSnapshot snapshot, int? controllerCount, int elapsedSeconds, bool billingRoundUp)
    {
        _ = elapsedSeconds;
        _ = billingRoundUp;

        var tier = GamingRateTier(controllerCount ?? 1);
        var packagePrice = snapshot.GamingRates
            .Where(r => r.ControllerCount == tier)
            .Select(r => r.Rate)
            .FirstOrDefault();

        if (packagePrice <= 0)
            packagePrice = snapshot.PackagePrice ?? 0;

        return packagePrice > 0 ? packagePrice : 0;
    }

    private static decimal CalculateWatchingCost(RateSnapshot snapshot, int? watcherCount, decimal units)
    {
        var rate = snapshot.WatchingRates.FirstOrDefault()?.RatePerPerson ?? 0;
        // Misconfigured plan / legacy session: bill 0 instead of breaking the active-sessions list.
        if (rate <= 0)
            return 0;

        // Default PerPerson for old snapshots that omit WatchingBilling
        var billing = snapshot.WatchingBilling == 0
            ? WatchingBilling.PerPerson
            : snapshot.WatchingBilling;

        if (watcherCount is null or <= 0)
            return 0;

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
