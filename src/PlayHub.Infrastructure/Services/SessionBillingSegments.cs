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
        out decimal amount,
        bool chargeFullPlannedBooking = false)
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
            var tierAr = tier == 2 ? "زوجي" : "فردي";
            var matchLabel =
                $"ماتش ({tierAr}) · تمن المباراة {matchRate:0.##} × {matchCount} مباريات = {amount:0.##}";

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
        // Booked sessions: closing early still bills the reserved time (Max). Mid-session
        // splits (convert) only bill elapsed so far (Min) so the next segment can continue.
        int billableSeconds;
        if (session.PlannedDurationMinutes is > 0)
        {
            var plannedSeconds = session.PlannedDurationMinutes.Value * 60;
            billableSeconds = chargeFullPlannedBooking
                ? Math.Max(elapsed, plannedSeconds)
                : Math.Min(elapsed, plannedSeconds);
        }
        else
        {
            billableSeconds = elapsed;
        }

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
        int? peopleCount = null;
        var tierName = tier == 2 ? "زوجي" : "فردي";
        var minutes = decimal.Round(billableSeconds / 60m, 2);

        if (session.SessionMode == SessionMode.Watching)
        {
            var watchers = session.WatcherCount ?? 0;
            peopleCount = watchers;
            var perPerson = calc.GetWatchingRatePerPerson(session.RateSnapshot);
            var watchingBilling = calc.GetWatchingBilling(session.RateSnapshot) ?? WatchingBilling.PerPerson;

            if (watchingBilling == WatchingBilling.PerPerson)
            {
                // Flat: تمن الفرد × عدد الأفراد
                rate = perPerson;
                qty = watchers;
                qtyUnit = "guest";
                label = $"مشاهدة · تمن الفرد {perPerson:0.##} × {watchers} أفراد = {amount:0.##}";
            }
            else if (timeUnit == TimeUnit.PerHour)
            {
                rate = perPerson;
                qty = !billingRoundUp
                    ? decimal.Round(billableSeconds / 3600m, 4)
                    : (hours > 0 ? (decimal)hours : 0);
                qtyUnit = "hour";
                label =
                    $"مشاهدة · تمن الفرد {perPerson:0.##} × {watchers} أفراد × {qty:0.####} ساعة = {amount:0.##}";
            }
            else
            {
                rate = perPerson;
                qty = minutes;
                qtyUnit = "min";
                label =
                    $"مشاهدة · تمن الفرد {perPerson:0.##} × {watchers} أفراد × {qty:0.##} دقيقة = {amount:0.##}";
            }
        }
        else if (timeUnit == TimeUnit.PerHour)
        {
            rate = calc.GetGamingRate(session.RateSnapshot, session.ControllerCount);
            qty = hours > 0 ? (decimal)hours : 0;
            if (!billingRoundUp)
                qty = decimal.Round(billableSeconds / 3600m, 4);
            qtyUnit = "hour";
            label = $"لعب ({tierName}) · سعر الساعة {rate:0.##} × {qty:0.####} ساعة = {amount:0.##}";
        }
        else
        {
            rate = calc.GetGamingRate(session.RateSnapshot, session.ControllerCount);
            qty = minutes;
            qtyUnit = "min";
            label = $"لعب ({tierName}) · السعر {rate:0.##} × {qty:0.##} دقيقة = {amount:0.##}";
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
            tier,
            peopleCount);
    }
}
