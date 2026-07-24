using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Loyalty;
using PlayHub.Application.Sessions;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class LoyaltyOfferService : ILoyaltyOfferService
{
    private const decimal FallbackRateEgp = 50m;

    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;
    private readonly ISessionCostCalculator _costCalculator;

    public LoyaltyOfferService(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IAuditService audit,
        ISessionCostCalculator costCalculator)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
        _costCalculator = costCalculator;
    }

    public async Task<IReadOnlyList<LoyaltyOfferDto>> GetAllAsync(bool? activeOnly = null, CancellationToken ct = default)
    {
        var query = BaseOfferQuery();
        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        if (ownerFilter.HasValue)
            query = query.Where(o => o.OwnerUserId == ownerFilter.Value);

        if (activeOnly == true)
            query = query.Where(o => o.IsActive);

        var items = await query.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        return items.Select(MapOffer).ToList();
    }

    public async Task<LoyaltyOfferDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var offer = await BaseOfferQuery().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (offer is null) return null;

        if (!_tenantContext.IsSuperAdmin)
        {
            var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
            if (!OwnerScope.CanAccess(offer.OwnerUserId, ownerId, false))
                return null;
        }

        return MapOffer(offer);
    }

    public async Task<LoyaltyOfferDto> CreateAsync(CreateLoyaltyOfferRequest request, CancellationToken ct = default)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Loyalty offer title is required.");

        ValidateChildren(request.Conditions, request.Rewards);
        await EnsureDevicesExistAsync(request.DeviceIds, ct);

        var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
        var offer = new LoyaltyOffer
        {
            TenantId = _tenantContext.TenantId,
            OwnerUserId = ownerId,
            BranchId = _tenantContext.BranchId,
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            PlayerScope = request.PlayerScope,
            Fulfillment = request.Fulfillment,
            ConditionLogic = request.ConditionLogic
        };

        ApplyChildren(offer, request.Conditions, request.Rewards, request.DeviceIds);

        _db.LoyaltyOffers.Add(offer);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LoyaltyOffer.Created", "LoyaltyOffer", offer.Id, new { offer.Title }, ct: ct);

        return await GetMappedAsync(offer.Id, ct);
    }

    public async Task<LoyaltyOfferDto> UpdateAsync(Guid id, UpdateLoyaltyOfferRequest request, CancellationToken ct = default)
    {
        var offer = await RequireOwnedAsync(id, ct);

        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Loyalty offer title is required.");

        ValidateChildren(request.Conditions, request.Rewards);
        await EnsureDevicesExistAsync(request.DeviceIds, ct);

        offer.Title = title;
        offer.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        offer.IsActive = request.IsActive;
        offer.StartsAt = request.StartsAt;
        offer.EndsAt = request.EndsAt;
        offer.PlayerScope = request.PlayerScope;
        offer.Fulfillment = request.Fulfillment;
        offer.ConditionLogic = request.ConditionLogic;

        _db.LoyaltyOfferConditions.RemoveRange(offer.Conditions);
        _db.LoyaltyOfferRewards.RemoveRange(offer.Rewards);
        _db.LoyaltyOfferDevices.RemoveRange(offer.Devices);
        offer.Conditions.Clear();
        offer.Rewards.Clear();
        offer.Devices.Clear();

        ApplyChildren(offer, request.Conditions, request.Rewards, request.DeviceIds);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LoyaltyOffer.Updated", "LoyaltyOffer", offer.Id, new { offer.Title, offer.IsActive }, ct: ct);

        return await GetMappedAsync(offer.Id, ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var offer = await RequireOwnedAsync(id, ct);

        offer.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        offer.IsActive = false;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LoyaltyOffer.SoftDeleted", "LoyaltyOffer", offer.Id, new { offer.Title }, ct: ct);
    }

    public async Task<IReadOnlyList<LoyaltyCreditDto>> GetCustomerCreditsAsync(
        Guid customerId, bool availableOnly = true, CancellationToken ct = default)
    {
        await RequireAccessibleCustomerAsync(customerId, ct);

        var query = _db.LoyaltyCredits.AsNoTracking()
            .Include(c => c.Offer)
            .Include(c => c.CafeteriaItem)
            .Include(c => c.Variant)
            .Where(c => c.TenantId == _tenantContext.TenantId && c.CustomerId == customerId);

        if (availableOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(c =>
                c.Status == LoyaltyCreditStatus.Available
                && c.QuantityRemaining > 0
                && (c.ExpiresAt == null || c.ExpiresAt > now));
        }

        var items = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return items.Select(MapCredit).ToList();
    }

    public async Task RedeemCreditAsync(RedeemLoyaltyCreditRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
            throw new InvalidOperationException("Redeem quantity must be greater than zero.");

        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);

        var credit = await _db.LoyaltyCredits
            .Include(c => c.CafeteriaItem)
            .Include(c => c.Variant)
            .FirstOrDefaultAsync(c =>
                c.Id == request.CreditId
                && c.TenantId == _tenantContext.TenantId, ct)
            ?? throw new KeyNotFoundException("Loyalty credit not found.");

        await RequireAccessibleCustomerAsync(credit.CustomerId, ct);

        if (credit.Status != LoyaltyCreditStatus.Available || credit.QuantityRemaining <= 0)
            throw new InvalidOperationException("Loyalty credit is not available.");

        if (credit.ExpiresAt is not null && credit.ExpiresAt <= DateTime.UtcNow)
        {
            credit.Status = LoyaltyCreditStatus.Expired;
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Loyalty credit has expired.");
        }

        if (request.Quantity > credit.QuantityRemaining)
            throw new InvalidOperationException("Redeem quantity exceeds remaining credit.");

        var session = await _db.Sessions
            .Include(s => s.CafeteriaLines)
            .Include(s => s.Pauses)
            .FirstOrDefaultAsync(s =>
                s.Id == request.SessionId
                && s.BranchId == branchId
                && (s.Status == SessionStatus.Open || s.Status == SessionStatus.Paused), ct)
            ?? throw new KeyNotFoundException("Open or paused session not found.");

        if (session.CustomerId != credit.CustomerId)
            throw new InvalidOperationException("Credit belongs to a different customer than the session.");

        var qty = decimal.Round(request.Quantity, 2);

        switch (credit.RewardMetric)
        {
            case LoyaltyRewardMetric.FreeHours:
            case LoyaltyRewardMetric.FreeMatches:
                ApplyTimeOrMatchDiscount(session, credit.RewardMetric, qty);
                break;
            case LoyaltyRewardMetric.CafeteriaItem:
                await ApplyFreeCafeteriaLineAsync(session, credit, qty, ct);
                break;
            default:
                throw new InvalidOperationException("Unsupported loyalty reward metric.");
        }

        credit.QuantityRemaining = decimal.Round(credit.QuantityRemaining - qty, 2);
        credit.RedeemedOnSessionId = session.Id;
        if (credit.QuantityRemaining <= 0)
        {
            credit.QuantityRemaining = 0;
            credit.Status = LoyaltyCreditStatus.FullyRedeemed;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LoyaltyCredit.Redeemed", "LoyaltyCredit", credit.Id, new
        {
            credit.CustomerId,
            SessionId = session.Id,
            Metric = credit.RewardMetric.ToString(),
            Quantity = qty
        }, ct: ct);
    }

    public async Task EvaluateAfterSessionCloseAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions
            .Include(s => s.Device)
            .Include(s => s.Customer)
            .Include(s => s.CafeteriaLines)
            .Include(s => s.PricingPlan).ThenInclude(p => p.GamingRates)
            .Include(s => s.PricingPlan).ThenInclude(p => p.WatchingRates)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Status == SessionStatus.Closed, ct);

        if (session is null || session.CustomerId is null)
            return;

        var customerId = session.CustomerId.Value;
        var now = session.ClosedAt ?? DateTime.UtcNow;

        var offersQuery = _db.LoyaltyOffers
            .Include(o => o.Conditions)
            .Include(o => o.Rewards)
            .Include(o => o.Devices)
            .Where(o =>
                o.TenantId == _tenantContext.TenantId
                && !o.IsDeleted
                && o.IsActive
                && (o.StartsAt == null || o.StartsAt <= now)
                && (o.EndsAt == null || o.EndsAt >= now));

        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        if (ownerFilter.HasValue)
            offersQuery = offersQuery.Where(o => o.OwnerUserId == ownerFilter.Value);

        var offers = (await offersQuery.ToListAsync(ct))
            .Where(o => o.Devices.Count == 0 || o.Devices.Any(d => d.DeviceId == session.DeviceId))
            .Where(o => MatchesPlayerScope(o.PlayerScope, session))
            .ToList();

        if (offers.Count == 0)
            return;

        var awardedSet = (await _db.LoyaltyCredits.AsNoTracking()
                .Where(c => c.SourceSessionId == session.Id)
                .Select(c => c.OfferId)
                .ToListAsync(ct))
            .ToHashSet();

        var segments = SessionBillingSegments.Read(session);
        var playHours = ComputePlayHours(session, segments);
        var matches = segments
            .Where(s => string.Equals(s.Kind, "Match", StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.Quantity);

        // Preload rolling-window hours for any PlayHoursInDays conditions.
        var maxWindow = offers
            .SelectMany(o => o.Conditions)
            .Where(c => c.Metric == LoyaltyConditionMetric.PlayHoursInDays)
            .Select(c => c.WindowDays ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        Dictionary<int, decimal>? hoursByWindow = null;
        if (maxWindow > 0)
            hoursByWindow = await BuildPlayHoursInWindowsAsync(customerId, now, maxWindow, ct);

        foreach (var offer in offers)
        {
            if (awardedSet.Contains(offer.Id))
                continue;

            if (!await ConditionsMetAsync(offer, session, playHours, matches, hoursByWindow, ct))
                continue;

            foreach (var reward in offer.Rewards)
            {
                _db.LoyaltyCredits.Add(new LoyaltyCredit
                {
                    TenantId = _tenantContext.TenantId,
                    CustomerId = customerId,
                    OfferId = offer.Id,
                    SourceSessionId = session.Id,
                    RewardMetric = reward.Metric,
                    QuantityOriginal = reward.Quantity,
                    QuantityRemaining = reward.Quantity,
                    CafeteriaItemId = reward.CafeteriaItemId,
                    VariantId = reward.VariantId,
                    Status = LoyaltyCreditStatus.Available
                });
            }

            awardedSet.Add(offer.Id);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> ConditionsMetAsync(
        LoyaltyOffer offer,
        Session session,
        decimal playHours,
        decimal matches,
        Dictionary<int, decimal>? hoursByWindow,
        CancellationToken ct)
    {
        if (offer.Conditions.Count == 0)
            return false;

        var results = new List<bool>(offer.Conditions.Count);
        foreach (var condition in offer.Conditions)
        {
            results.Add(await ConditionMetAsync(condition, session, playHours, matches, hoursByWindow, ct));
        }

        return offer.ConditionLogic == LoyaltyConditionLogic.Any
            ? results.Any(x => x)
            : results.All(x => x);
    }

    private async Task<bool> ConditionMetAsync(
        LoyaltyOfferCondition condition,
        Session session,
        decimal playHours,
        decimal matches,
        Dictionary<int, decimal>? hoursByWindow,
        CancellationToken ct)
    {
        switch (condition.Metric)
        {
            case LoyaltyConditionMetric.PlayHours:
                return playHours >= condition.RequiredQuantity;

            case LoyaltyConditionMetric.Matches:
                return matches >= condition.RequiredQuantity;

            case LoyaltyConditionMetric.CafeteriaQuantity:
                var cafeQty = session.CafeteriaLines
                    .Where(l => l.CafeteriaItemId == condition.CafeteriaItemId
                                && (condition.VariantId == null || l.VariantId == condition.VariantId))
                    .Sum(l => l.Quantity - l.ReturnedQuantity);
                return cafeQty >= condition.RequiredQuantity;

            case LoyaltyConditionMetric.PlayHoursInDays:
                var window = condition.WindowDays ?? 0;
                if (window < 1)
                    return false;
                if (hoursByWindow is not null && hoursByWindow.TryGetValue(window, out var cached))
                    return cached >= condition.RequiredQuantity;

                // Fallback single-window query if cache missing this key.
                var end = session.ClosedAt ?? DateTime.UtcNow;
                var start = end.AddDays(-window);
                var sessions = await _db.Sessions.AsNoTracking()
                    .Where(s =>
                        s.CustomerId == session.CustomerId
                        && s.Status == SessionStatus.Closed
                        && s.ClosedAt != null
                        && s.ClosedAt >= start
                        && s.ClosedAt <= end)
                    .ToListAsync(ct);
                var total = sessions.Sum(s => ComputePlayHours(s, SessionBillingSegments.Read(s)));
                return total >= condition.RequiredQuantity;

            default:
                return false;
        }
    }

    private async Task<Dictionary<int, decimal>> BuildPlayHoursInWindowsAsync(
        Guid customerId, DateTime end, int maxWindowDays, CancellationToken ct)
    {
        var start = end.AddDays(-maxWindowDays);
        var sessions = await _db.Sessions.AsNoTracking()
            .Where(s =>
                s.CustomerId == customerId
                && s.Status == SessionStatus.Closed
                && s.ClosedAt != null
                && s.ClosedAt >= start
                && s.ClosedAt <= end)
            .Select(s => new { s.ClosedAt, s.StartedAt, s.TotalPausedSeconds, s.PlannedDurationMinutes, s.BillingSegmentsJson })
            .ToListAsync(ct);

        var result = new Dictionary<int, decimal>();
        for (var days = 1; days <= maxWindowDays; days++)
        {
            var windowStart = end.AddDays(-days);
            decimal total = 0;
            foreach (var s in sessions)
            {
                if (s.ClosedAt < windowStart) continue;
                var stub = new Session
                {
                    StartedAt = s.StartedAt,
                    ClosedAt = s.ClosedAt,
                    TotalPausedSeconds = s.TotalPausedSeconds,
                    PlannedDurationMinutes = s.PlannedDurationMinutes,
                    BillingSegmentsJson = s.BillingSegmentsJson,
                    Status = SessionStatus.Closed
                };
                total += ComputePlayHours(stub, SessionBillingSegments.Read(stub));
            }

            result[days] = total;
        }

        return result;
    }

    private async Task<LoyaltyOffer> RequireOwnedAsync(Guid id, CancellationToken ct)
    {
        var offer = await _db.LoyaltyOffers
            .Include(o => o.Conditions)
            .Include(o => o.Rewards)
            .Include(o => o.Devices)
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == _tenantContext.TenantId && !o.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Loyalty offer not found.");

        if (!_tenantContext.IsSuperAdmin)
        {
            var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
            if (!OwnerScope.CanAccess(offer.OwnerUserId, ownerId, false))
                throw new KeyNotFoundException("Loyalty offer not found.");
        }

        return offer;
    }

    private IQueryable<LoyaltyOffer> BaseOfferQuery() =>
        _db.LoyaltyOffers.AsNoTracking()
            .Include(o => o.Conditions).ThenInclude(c => c.CafeteriaItem)
            .Include(o => o.Conditions).ThenInclude(c => c.Variant)
            .Include(o => o.Rewards).ThenInclude(r => r.CafeteriaItem)
            .Include(o => o.Rewards).ThenInclude(r => r.Variant)
            .Include(o => o.Devices).ThenInclude(d => d.Device)
            .Where(o => o.TenantId == _tenantContext.TenantId && !o.IsDeleted);

    private async Task<LoyaltyOfferDto> GetMappedAsync(Guid id, CancellationToken ct)
    {
        var offer = await BaseOfferQuery().FirstAsync(o => o.Id == id, ct);
        return MapOffer(offer);
    }

    private static void ApplyChildren(
        LoyaltyOffer offer,
        IReadOnlyList<UpsertLoyaltyOfferConditionRequest> conditions,
        IReadOnlyList<UpsertLoyaltyOfferRewardRequest> rewards,
        IReadOnlyList<Guid>? deviceIds)
    {
        foreach (var c in conditions)
        {
            offer.Conditions.Add(new LoyaltyOfferCondition
            {
                Metric = c.Metric,
                RequiredQuantity = c.RequiredQuantity,
                WindowDays = c.Metric == LoyaltyConditionMetric.PlayHoursInDays ? c.WindowDays : null,
                CafeteriaItemId = c.Metric == LoyaltyConditionMetric.CafeteriaQuantity ? c.CafeteriaItemId : null,
                VariantId = c.Metric == LoyaltyConditionMetric.CafeteriaQuantity ? c.VariantId : null
            });
        }

        foreach (var r in rewards)
        {
            offer.Rewards.Add(new LoyaltyOfferReward
            {
                Metric = r.Metric,
                Quantity = r.Quantity,
                CafeteriaItemId = r.Metric == LoyaltyRewardMetric.CafeteriaItem ? r.CafeteriaItemId : null,
                VariantId = r.Metric == LoyaltyRewardMetric.CafeteriaItem ? r.VariantId : null
            });
        }

        if (deviceIds is null) return;

        foreach (var deviceId in deviceIds.Distinct())
            offer.Devices.Add(new LoyaltyOfferDevice { DeviceId = deviceId });
    }

    private static void ValidateChildren(
        IReadOnlyList<UpsertLoyaltyOfferConditionRequest> conditions,
        IReadOnlyList<UpsertLoyaltyOfferRewardRequest> rewards)
    {
        if (conditions.Count == 0)
            throw new InvalidOperationException("At least one condition is required.");
        if (rewards.Count == 0)
            throw new InvalidOperationException("At least one reward is required.");

        foreach (var c in conditions)
        {
            if (c.RequiredQuantity <= 0)
                throw new InvalidOperationException("Condition required quantity must be greater than zero.");

            if (c.Metric == LoyaltyConditionMetric.PlayHoursInDays && (c.WindowDays is null or < 1))
                throw new InvalidOperationException("PlayHoursInDays conditions require WindowDays >= 1.");

            if (c.Metric == LoyaltyConditionMetric.CafeteriaQuantity && c.CafeteriaItemId is null)
                throw new InvalidOperationException("Cafeteria quantity conditions require a cafeteria item.");
        }

        foreach (var r in rewards)
        {
            if (r.Quantity <= 0)
                throw new InvalidOperationException("Reward quantity must be greater than zero.");

            if (r.Metric == LoyaltyRewardMetric.CafeteriaItem && r.CafeteriaItemId is null)
                throw new InvalidOperationException("Cafeteria item rewards require a cafeteria item.");
        }
    }

    private async Task EnsureDevicesExistAsync(IReadOnlyList<Guid>? deviceIds, CancellationToken ct)
    {
        if (deviceIds is null || deviceIds.Count == 0)
            return;

        var distinct = deviceIds.Distinct().ToList();
        var found = await _db.Devices.AsNoTracking()
            .Where(d => distinct.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync(ct);

        if (found.Count != distinct.Count)
            throw new InvalidOperationException("One or more devices were not found.");
    }

    private async Task RequireAccessibleCustomerAsync(Guid id, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Customer not found.");

        if (_tenantContext.IsSuperAdmin)
            return;

        var userId = _tenantContext.UserId;
        var allowed = _tenantContext.AllowedBranchIds;
        var isMine = customer.CreatedByUserId == userId
            || (customer.BranchId != null && allowed.Contains(customer.BranchId.Value))
            || await _db.Users.IgnoreQueryFilters().AnyAsync(
                u => u.Id == customer.CreatedByUserId && u.ParentUserId == userId && !u.IsDeleted, ct);

        if (!isMine)
            throw new KeyNotFoundException("Customer not found.");
    }

    private void ApplyTimeOrMatchDiscount(Session session, LoyaltyRewardMetric metric, decimal quantity)
    {
        var rate = metric == LoyaltyRewardMetric.FreeMatches
            ? EstimateMatchRate(session)
            : EstimateHourlyRate(session);

        var add = decimal.Round(rate * quantity, 2);
        if (add <= 0)
            return;

        var running = EstimateRunningSubtotal(session);
        var room = Math.Max(0, running - session.DiscountAmount);
        if (room <= 0)
            return;

        add = Math.Min(add, room);
        session.DiscountAmount = decimal.Round(session.DiscountAmount + add, 2);
        session.DiscountReason = "Loyalty credit";
    }

    private async Task ApplyFreeCafeteriaLineAsync(
        Session session, LoyaltyCredit credit, decimal quantity, CancellationToken ct)
    {
        if (credit.CafeteriaItemId is null)
            throw new InvalidOperationException("Cafeteria credit is missing an item.");

        var qty = (int)Math.Round(quantity, MidpointRounding.AwayFromZero);
        if (qty < 1)
            throw new InvalidOperationException("Cafeteria redeem quantity must be at least 1.");

        var item = await _db.CafeteriaItems
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == credit.CafeteriaItemId.Value, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        CafeteriaItemVariant? variant = null;
        if (credit.VariantId is Guid variantId)
        {
            variant = item.Variants.FirstOrDefault(v => v.Id == variantId)
                ?? throw new KeyNotFoundException("Cafeteria variant not found.");
        }
        else
        {
            variant = item.Variants.Where(v => v.IsActive).OrderBy(v => v.SortOrder).FirstOrDefault()
                ?? item.Variants.OrderBy(v => v.SortOrder).FirstOrDefault();
        }

        session.CafeteriaLines.Add(new SessionCafeteriaLine
        {
            CafeteriaItemId = item.Id,
            VariantId = variant?.Id,
            VariantName = variant?.Name,
            Quantity = qty,
            StockDeductQuantity = 0,
            UnitPrice = 0,
            LineTotal = 0,
            AddedByUserId = _tenantContext.UserId == Guid.Empty
                ? session.OpenedByUserId
                : _tenantContext.UserId
        });
    }

    private decimal EstimateHourlyRate(Session session)
    {
        if (session.SessionMode == SessionMode.Watching)
        {
            var watching = _costCalculator.GetWatchingRatePerPerson(session.RateSnapshot);
            if (watching > 0) return watching;
        }
        else
        {
            var gaming = _costCalculator.GetGamingRate(session.RateSnapshot, session.ControllerCount ?? 1);
            if (gaming > 0) return gaming;

            var first = TryFirstGamingRate(session.RateSnapshot);
            if (first > 0) return first;
        }

        return FallbackRateEgp;
    }

    private decimal EstimateMatchRate(Session session)
    {
        var gaming = _costCalculator.GetGamingRate(session.RateSnapshot, session.ControllerCount ?? 1);
        if (gaming > 0) return gaming;

        var first = TryFirstGamingRate(session.RateSnapshot);
        return first > 0 ? first : FallbackRateEgp;
    }

    private static decimal TryFirstGamingRate(string rateSnapshotJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rateSnapshotJson) ? "{}" : rateSnapshotJson);
            if (!doc.RootElement.TryGetProperty("gamingRates", out var rates)
                && !doc.RootElement.TryGetProperty("GamingRates", out rates))
                return 0;

            foreach (var r in rates.EnumerateArray())
            {
                if (r.TryGetProperty("rate", out var rateEl) || r.TryGetProperty("Rate", out rateEl))
                {
                    if (rateEl.TryGetDecimal(out var rate) && rate > 0)
                        return rate;
                }
            }
        }
        catch (JsonException)
        {
            // ignore corrupt snapshot
        }

        return 0;
    }

    private decimal EstimateRunningSubtotal(Session session)
    {
        var activePause = session.Pauses.LastOrDefault(p => p.ResumedAt == null);
        var elapsed = _costCalculator.GetElapsedSeconds(
            session.Status, session.StartedAt, session.TotalPausedSeconds, activePause?.PausedAt, session.ClosedAt);

        var timeUnit = _costCalculator.GetTimeUnit(session.RateSnapshot);
        var isPerGame = session.SessionMode == SessionMode.Gaming && timeUnit == TimeUnit.PerGame;
        decimal timeCost;
        if (isPerGame)
        {
            timeCost = session.AccruedTimeCost;
        }
        else
        {
            var billableSeconds = session.PlannedDurationMinutes is > 0
                ? Math.Min(elapsed, session.PlannedDurationMinutes.Value * 60)
                : elapsed;
            timeCost = session.AccruedTimeCost + _costCalculator.CalculateTimeCost(
                session.RateSnapshot, session.SessionMode, billableSeconds,
                session.ControllerCount, session.WatcherCount, billingRoundUp: false);
        }

        var cafeteria = session.CafeteriaLines.Sum(l => l.LineTotal);
        var surcharge = session.RoomSurchargePerHour > 0
            ? decimal.Round(session.RoomSurchargePerHour * elapsed / 3600m, 2)
            : 0m;

        return Math.Max(0, timeCost + cafeteria + surcharge);
    }

    private static bool MatchesPlayerScope(LoyaltyPlayerScope scope, Session session)
    {
        if (scope == LoyaltyPlayerScope.Any)
            return true;

        // Watching: only Any-scoped offers apply.
        if (session.SessionMode == SessionMode.Watching)
            return false;

        var controllers = session.ControllerCount ?? 0;
        return scope switch
        {
            LoyaltyPlayerScope.Individual => controllers is >= 1 and <= 2,
            LoyaltyPlayerScope.Couple => controllers >= 3,
            _ => true
        };
    }

    private decimal ComputePlayHours(Session session, IReadOnlyList<BillingSegmentDto> segments)
    {
        var hourQty = segments
            .Where(s => string.Equals(s.QuantityUnit, "hour", StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.Quantity);
        if (hourQty > 0)
            return hourQty;

        if (session.PlannedDurationMinutes is > 0)
            return decimal.Round(session.PlannedDurationMinutes.Value / 60m, 4);

        var elapsed = _costCalculator.GetElapsedSeconds(
            SessionStatus.Closed,
            session.StartedAt,
            session.TotalPausedSeconds,
            null,
            session.ClosedAt);
        return decimal.Round(elapsed / 3600m, 4);
    }

    private static LoyaltyOfferDto MapOffer(LoyaltyOffer o) =>
        new(
            o.Id,
            o.Title,
            o.Description,
            o.IsActive,
            o.StartsAt,
            o.EndsAt,
            o.PlayerScope,
            o.Fulfillment,
            o.ConditionLogic,
            o.Conditions.Select(c => new LoyaltyOfferConditionDto(
                c.Id,
                c.Metric,
                c.RequiredQuantity,
                c.WindowDays,
                c.CafeteriaItemId,
                c.CafeteriaItem?.Name,
                c.VariantId,
                c.Variant?.Name)).ToList(),
            o.Rewards.Select(r => new LoyaltyOfferRewardDto(
                r.Id,
                r.Metric,
                r.Quantity,
                r.CafeteriaItemId,
                r.CafeteriaItem?.Name,
                r.VariantId,
                r.Variant?.Name)).ToList(),
            o.Devices.Select(d => d.DeviceId).ToList(),
            o.Devices.Select(d => d.Device?.Name ?? string.Empty).Where(n => n.Length > 0).ToList(),
            o.CreatedAt);

    private static LoyaltyCreditDto MapCredit(LoyaltyCredit c) =>
        new(
            c.Id,
            c.CustomerId,
            c.OfferId,
            c.Offer?.Title ?? string.Empty,
            c.RewardMetric,
            c.QuantityOriginal,
            c.QuantityRemaining,
            c.CafeteriaItemId,
            c.CafeteriaItem?.Name,
            c.VariantId,
            c.Variant?.Name,
            c.Status,
            c.ExpiresAt,
            c.CreatedAt);
}
