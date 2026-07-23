using System.Text.Json;
using PlayHub.Application.Sessions;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;

namespace PlayHub.Infrastructure.Services;

internal static class SessionBillingSegments
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static List<BillingSegmentDto> Read(Session session)
    {
        if (string.IsNullOrWhiteSpace(session.BillingSegmentsJson) || session.BillingSegmentsJson == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<BillingSegmentDto>>(session.BillingSegmentsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static void Write(Session session, IReadOnlyList<BillingSegmentDto> segments)
    {
        session.BillingSegmentsJson = JsonSerializer.Serialize(segments, JsonOptions);
    }

    public static void Append(Session session, BillingSegmentDto segment)
    {
        var list = Read(session);
        list.Add(segment);
        Write(session, list);
        session.AccruedTimeCost = decimal.Round(list.Sum(s => s.Amount), 2);
    }

    public static BillingSegmentDto Build(
        ISessionCostCalculator calc,
        Session session,
        DateTime endedAt,
        bool billingRoundUp,
        int? matchCount,
        out decimal amount)
    {
        var timeUnit = calc.GetTimeUnit(session.RateSnapshot) ?? TimeUnit.PerHour;
        var isMatch = session.SessionMode == SessionMode.Gaming && timeUnit == TimeUnit.PerGame;
        var tier = session.ControllerCount is > 0
            ? SessionCostCalculator.GamingRateTier(session.ControllerCount.Value)
            : (int?)null;

        if (isMatch)
        {
            if (matchCount is null or < 1)
                throw new InvalidOperationException("Match count is required for per-match billing.");

            var matchRate = calc.GetGamingRate(session.RateSnapshot, session.ControllerCount);
            amount = decimal.Round(matchRate * matchCount.Value, 2);
            var matchLabel = tier == 2
                ? $"Matches · Couple × {matchCount}"
                : $"Matches · Individual × {matchCount}";

            return new BillingSegmentDto(
                "Match",
                matchLabel,
                matchRate,
                matchCount.Value,
                "match",
                amount,
                session.StartedAt,
                endedAt,
                tier);
        }

        var elapsed = calc.GetElapsedSeconds(SessionStatus.Open, session.StartedAt, session.TotalPausedSeconds, null, endedAt);
        var billableSeconds = session.PlannedDurationMinutes is > 0
            ? Math.Min(elapsed, session.PlannedDurationMinutes.Value * 60)
            : elapsed;

        amount = decimal.Round(
            calc.CalculateTimeCost(
                session.RateSnapshot,
                session.SessionMode,
                billableSeconds,
                session.ControllerCount,
                session.WatcherCount,
                billingRoundUp),
            2);

        var hours = billingRoundUp
            ? Math.Max(1, (int)Math.Ceiling(billableSeconds / 3600.0))
            : Math.Round(billableSeconds / 3600.0, 2);

        string label;
        decimal rate;
        decimal qty;
        string qtyUnit;

        if (session.SessionMode == SessionMode.Watching)
        {
            label = $"Watching · {session.WatcherCount ?? 0} guest(s)";
            rate = amount;
            qty = 1;
            qtyUnit = "segment";
        }
        else if (timeUnit == TimeUnit.PerHour)
        {
            rate = calc.GetGamingRate(session.RateSnapshot, session.ControllerCount);
            qty = (decimal)hours;
            qtyUnit = "hour";
            label = tier == 2
                ? $"Hourly · Couple · {qty}h @ {rate:0.##}"
                : $"Hourly · Individual · {qty}h @ {rate:0.##}";
        }
        else
        {
            rate = calc.GetGamingRate(session.RateSnapshot, session.ControllerCount);
            qty = billableSeconds / 60m;
            qtyUnit = "min";
            label = tier == 2 ? $"Timed · Couple" : $"Timed · Individual";
        }

        return new BillingSegmentDto(
            "Time",
            label,
            rate,
            qty,
            qtyUnit,
            amount,
            session.StartedAt,
            endedAt,
            tier);
    }
}
