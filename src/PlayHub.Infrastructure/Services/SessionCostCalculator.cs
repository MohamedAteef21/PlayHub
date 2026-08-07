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

    public TimeUnit? GetTimeUnit(string rateSnapshotJson)
    {
        var snapshot = Deserialize(rateSnapshotJson);
        return snapshot?.TimeUnit;
    }

    public decimal GetGamingRate(string rateSnapshotJson, int? controllerCount)
    {
        var snapshot = Deserialize(rateSnapshotJson);
        if (snapshot is null || controllerCount is null or <= 0)
            return 0;

        var tier = GamingRateTier(controllerCount.Value);
        return snapshot.GamingRates
            .Where(r => r.ControllerCount == tier)
            .Select(r => r.Rate)
            .FirstOrDefault();
    }

    public decimal CalculateTimeCost(
        string rateSnapshotJson,
        SessionMode mode,
        int elapsedSeconds,
        int? controllerCount,
        int? watcherCount,
        bool billingRoundUp,
        decimal? billableUnitsOverride = null)
    {
        var snapshot = Deserialize(rateSnapshotJson);
        // Corrupt/legacy snapshot: bill 0 instead of failing every session screen.
        if (snapshot is null)
            return 0;

        if (mode == SessionMode.Gaming && snapshot.PackagePrice is > 0 && snapshot.PackageDurationMinutes is > 0
            && billableUnitsOverride is null)
            return CalculatePackageCost(snapshot, controllerCount, elapsedSeconds, billingRoundUp);

        var units = billableUnitsOverride ?? GetBillableUnits(snapshot.TimeUnit, elapsedSeconds, billingRoundUp);

        return mode switch
        {
            SessionMode.Gaming => CalculateGamingCost(snapshot, controllerCount, units),
            SessionMode.Watching => CalculateWatchingCost(snapshot, watcherCount, units),
            _ => throw new InvalidOperationException("Unsupported session mode.")
        };
    }

    private static RateSnapshot? Deserialize(string rateSnapshotJson)
    {
        try
        {
            return JsonSerializer.Deserialize<RateSnapshot>(rateSnapshotJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static decimal GetBillableUnits(TimeUnit timeUnit, int elapsedSeconds, bool billingRoundUp)
    {
        // Flat per game/match — ignore elapsed time (override with MatchCount on close/convert).
        if (timeUnit == TimeUnit.PerGame)
            return 0;

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
        if (controllerCount is null or <= 0 || units <= 0)
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
        if (rate <= 0)
            return 0;

        var billing = snapshot.WatchingBilling == 0
            ? WatchingBilling.PerPerson
            : snapshot.WatchingBilling;

        if (watcherCount is null or <= 0)
            return 0;

        if (billing == WatchingBilling.PerPerson)
            return watcherCount.Value * rate;

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
