using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Cafeteria;
using PlayHub.Application.Common;
using PlayHub.Application.Sessions;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;
    private readonly ISessionCostCalculator _costCalculator;
    private readonly ISessionNotifier _notifier;
    private readonly LowStockNotifier _lowStock;

    public SessionService(
        PlayHubDbContext db,
        TenantContext tenantContext,
        IAuditService audit,
        ISessionCostCalculator costCalculator,
        ISessionNotifier notifier,
        LowStockNotifier lowStock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
        _costCalculator = costCalculator;
        _notifier = notifier;
        _lowStock = lowStock;
    }

    public async Task<IReadOnlyList<SessionLiveDto>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var sessions = await LoadActiveSessionsQuery(branchId).ToListAsync(ct);
        return sessions.Select(s => MapLive(s, billingRoundUp)).ToList();
    }

    public async Task<PagedResult<SessionHistoryDto>> GetSessionHistoryAsync(
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 20,
        Guid? customerId = null,
        CancellationToken ct = default)
    {
        var (p, size, skip) = PagingHelper.Normalize(page, pageSize);

        var query = _db.Sessions
            .AsNoTracking()
            .Include(s => s.Device)
            .Include(s => s.Room)
            .Include(s => s.Branch)
            .Include(s => s.OpenedByUser)
            .Include(s => s.ClosedByUser)
            .Include(s => s.Customer)
            .Include(s => s.CafeteriaLines)
            .AsQueryable();

        if (customerId.HasValue)
        {
            var allowed = _tenantContext.AllowedBranchIds;
            query = query.Where(s =>
                s.CustomerId == customerId.Value
                && allowed.Contains(s.BranchId));
        }
        else
        {
            var branchId = BranchGuard.RequireBranchId(_tenantContext);
            query = query.Where(s => s.BranchId == branchId);
        }

        if (from.HasValue)
            query = query.Where(s => s.StartedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.StartedAt <= to.Value);

        var total = await query.CountAsync(ct);
        var sessions = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip(skip)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<SessionHistoryDto>(sessions.Select(MapHistory).ToList(), total, p, size);
    }

    public async Task<SessionDetailDto?> GetSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var session = await _db.Sessions
            .Include(s => s.Device)
            .Include(s => s.Room)
            .Include(s => s.PricingPlan)
            .Include(s => s.OpenedByUser)
            .Include(s => s.ClosedByUser)
            .Include(s => s.Customer)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.Invoice).ThenInclude(i => i!.Payments)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        return session is null ? null : MapDetail(session);
    }

    public async Task<SessionLiveDto> OpenSessionAsync(OpenSessionRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var device = await _db.Devices
            .Include(d => d.Room)
            .Include(d => d.DeviceControllers)
            .Include(d => d.Screens)
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.BranchId == branchId, ct)
            ?? throw new KeyNotFoundException("Device not found.");

        if (!device.IsActive)
            throw new InvalidOperationException("This device is inactive and cannot start a session.");

        if (await _db.Sessions.AnyAsync(s => s.DeviceId == device.Id && s.Status != SessionStatus.Closed, ct))
            throw new InvalidOperationException("This device already has an active session.");

        var plan = await _db.PricingPlans
            .Include(p => p.GamingRates)
            .Include(p => p.WatchingRates)
            .FirstOrDefaultAsync(p => p.Id == request.PricingPlanId && p.IsActive && p.SessionMode == request.SessionMode
                && (p.BranchId == branchId || (p.BranchId == null && _tenantContext.IsSuperAdmin)), ct)
            ?? throw new KeyNotFoundException("Pricing plan not found.");

        ValidateSessionCounts(request, device);
        ValidatePlanRates(request, plan);

        if (request.CustomerId.HasValue && request.IsQuickGuest)
            throw new InvalidOperationException("Cannot set both a registered customer and quick guest.");

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(
                c => c.Id == request.CustomerId.Value && c.IsActive, ct)
                ?? throw new KeyNotFoundException("Customer not found.");
        }

        if (request.PlannedDurationMinutes is < 1)
            throw new InvalidOperationException("Planned duration must be at least 1 minute.");

        if (request.PlannedDurationMinutes is > 24 * 60)
            throw new InvalidOperationException("Planned duration cannot exceed 24 hours.");

        var snapshot = JsonSerializer.Serialize(new
        {
            plan.TimeUnit,
            plan.WatchingBilling,
            plan.PackageDurationMinutes,
            plan.PackagePrice,
            GamingRates = plan.GamingRates.Select(r => new { r.ControllerCount, r.Rate }),
            WatchingRates = plan.WatchingRates.Select(r => new { r.RatePerPerson })
        });

        var session = new Session
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            DeviceId = device.Id,
            RoomId = device.RoomId,
            SessionMode = request.SessionMode,
            PricingPlanId = plan.Id,
            ControllerCount = request.ControllerCount,
            WatcherCount = request.WatcherCount,
            RoomSurchargePerHour = 0,
            RateSnapshot = snapshot,
            Status = SessionStatus.Open,
            OpenedByUserId = _tenantContext.UserId,
            StartedAt = DateTime.UtcNow,
            PlannedDurationMinutes = request.PlannedDurationMinutes,
            CustomerId = customer?.Id,
            IsQuickGuest = request.IsQuickGuest,
            QuickGuestName = request.IsQuickGuest
                ? (string.IsNullOrWhiteSpace(request.QuickGuestName) ? "ضيف سريع" : request.QuickGuestName.Trim())
                : null
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Session.Opened", "Session", session.Id, new
        {
            device.Identifier,
            request.SessionMode,
            request.ControllerCount,
            request.WatcherCount,
            request.PlannedDurationMinutes,
            PlanName = plan.Name,
            session.CustomerId,
            session.IsQuickGuest,
            session.QuickGuestName
        }, ct: ct);

        await _db.Entry(session).Reference(s => s.Device).LoadAsync(ct);
        await _db.Entry(session).Reference(s => s.Room).LoadAsync(ct);
        await _db.Entry(session).Reference(s => s.PricingPlan).LoadAsync(ct);
        await _db.Entry(session).Reference(s => s.OpenedByUser).LoadAsync(ct);
        if (session.CustomerId.HasValue)
            await _db.Entry(session).Reference(s => s.Customer).LoadAsync(ct);

        var live = MapLive(session, billingRoundUp);
        await _notifier.NotifySessionUpdatedAsync(branchId, live, ct);
        return live;
    }

    public async Task<SessionLiveDto> PauseSessionAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var session = await GetMutableActiveSessionAsync(id, branchId, ct);
        if (session.Status != SessionStatus.Open)
            throw new InvalidOperationException("Only open sessions can be paused.");

        session.Status = SessionStatus.Paused;
        session.Pauses.Add(new SessionPause
        {
            PausedAt = DateTime.UtcNow,
            PausedByUserId = _tenantContext.UserId
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Session.Paused", "Session", session.Id, ct: ct);

        await LoadSessionGraphAsync(session, ct);
        var live = MapLive(session, billingRoundUp);
        await _notifier.NotifySessionUpdatedAsync(branchId, live, ct);
        return live;
    }

    public async Task<SessionLiveDto> ResumeSessionAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var session = await GetMutableActiveSessionAsync(id, branchId, ct);
        if (session.Status != SessionStatus.Paused)
            throw new InvalidOperationException("Only paused sessions can be resumed.");

        var activePause = session.Pauses.LastOrDefault(p => p.ResumedAt == null)
            ?? throw new InvalidOperationException("No active pause record found.");

        activePause.ResumedAt = DateTime.UtcNow;
        session.TotalPausedSeconds += (int)Math.Max(0, (activePause.ResumedAt.Value - activePause.PausedAt).TotalSeconds);
        session.Status = SessionStatus.Open;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Session.Resumed", "Session", session.Id, ct: ct);

        await LoadSessionGraphAsync(session, ct);
        var live = MapLive(session, billingRoundUp);
        await _notifier.NotifySessionUpdatedAsync(branchId, live, ct);
        return live;
    }

    /// <summary>Extend a booked session's planned time, or switch it to an open timer (AdditionalMinutes = null).</summary>
    public async Task<SessionLiveDto> ExtendSessionAsync(Guid id, ExtendSessionRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var session = await GetMutableActiveSessionAsync(id, branchId, ct);

        if (request.AdditionalMinutes is null)
        {
            if (session.PlannedDurationMinutes is null)
                throw new InvalidOperationException("Session already has an open timer.");
            session.PlannedDurationMinutes = null;
        }
        else
        {
            if (request.AdditionalMinutes is < 1)
                throw new InvalidOperationException("Additional time must be at least 1 minute.");

            var activePause = session.Pauses.LastOrDefault(p => p.ResumedAt == null);
            var elapsed = _costCalculator.GetElapsedSeconds(
                session.Status, session.StartedAt, session.TotalPausedSeconds, activePause?.PausedAt, session.ClosedAt);

            // Extending an open timer converts it to a fixed booking counted from the time already played.
            var baseMinutes = session.PlannedDurationMinutes ?? (int)Math.Ceiling(elapsed / 60.0);
            var newPlanned = baseMinutes + request.AdditionalMinutes.Value;
            if (newPlanned > 24 * 60)
                throw new InvalidOperationException("Planned duration cannot exceed 24 hours.");
            session.PlannedDurationMinutes = newPlanned;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Session.TimeExtended", "Session", session.Id, new
        {
            request.AdditionalMinutes,
            session.PlannedDurationMinutes
        }, ct: ct);

        await LoadSessionGraphAsync(session, ct);
        var live = MapLive(session, billingRoundUp);
        await _notifier.NotifySessionUpdatedAsync(branchId, live, ct);
        return live;
    }

    /// <summary>Change the number of watchers on an active watching session.
    /// Per-screen (time-billed) plans accrue the elapsed segment at the old headcount first.</summary>
    public async Task<SessionLiveDto> UpdateWatcherCountAsync(Guid id, UpdateWatchersRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var session = await _db.Sessions
            .Include(s => s.Pauses)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.Device).ThenInclude(d => d.Room)
            .Include(s => s.Device).ThenInclude(d => d.Screens)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId && s.Status != SessionStatus.Closed, ct)
            ?? throw new KeyNotFoundException("Active session not found.");

        if (session.SessionMode != SessionMode.Watching)
            throw new InvalidOperationException("Watcher count can only be changed on watching sessions.");

        if (request.WatcherCount < 1)
            throw new InvalidOperationException("Watcher count must be at least 1.");

        var maxCapacity = GetMaxWatchingCapacity(session.Device);
        if (request.WatcherCount > maxCapacity)
            throw new InvalidOperationException($"This room supports at most {maxCapacity} watcher(s).");

        var oldCount = session.WatcherCount ?? 0;
        if (request.WatcherCount == oldCount)
        {
            await LoadSessionGraphAsync(session, ct);
            return MapLive(session, billingRoundUp);
        }

        if (IsTimeBilledWatching(session.RateSnapshot))
        {
            if (session.Status != SessionStatus.Open)
                throw new InvalidOperationException("Resume the session before changing the watcher count.");

            // Freeze the current headcount as a billing segment, then restart at the new count.
            // Intermediate splits use billingRoundUp=false so frequent changes don't each add a full hour.
            var changeAt = DateTime.UtcNow;
            var elapsed = _costCalculator.GetElapsedSeconds(SessionStatus.Open, session.StartedAt, session.TotalPausedSeconds, null, changeAt);
            var segment = SessionBillingSegments.Build(
                _costCalculator, session, changeAt, billingRoundUp: false, matchCount: null, out _);
            SessionBillingSegments.Append(session, segment);
            session.OriginalStartedAt ??= session.StartedAt;

            // Keep the booking end time: the new segment carries only the remaining minutes.
            if (session.PlannedDurationMinutes is > 0)
            {
                var consumedMinutes = (int)Math.Round(elapsed / 60.0);
                session.PlannedDurationMinutes = Math.Max(1, session.PlannedDurationMinutes.Value - consumedMinutes);
            }

            session.StartedAt = changeAt;
            session.TotalPausedSeconds = 0;
        }

        session.WatcherCount = request.WatcherCount;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Session.WatchersChanged", "Session", session.Id, new
        {
            From = oldCount,
            To = request.WatcherCount
        }, ct: ct);

        await LoadSessionGraphAsync(session, ct);
        var live = MapLive(session, billingRoundUp);
        await _notifier.NotifySessionUpdatedAsync(branchId, live, ct);
        return live;
    }

    /// <summary>Per-screen watching bills by time, so headcount changes must split the billing segments.</summary>
    private static bool IsTimeBilledWatching(string rateSnapshotJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rateSnapshotJson);
            return doc.RootElement.TryGetProperty("WatchingBilling", out var billing)
                && billing.GetInt32() == (int)WatchingBilling.PerScreen;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task<SessionLiveDto> ConvertSessionAsync(Guid id, ConvertSessionRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var session = await _db.Sessions
            .Include(s => s.Pauses)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.Device).ThenInclude(d => d.DeviceControllers)
            .Include(s => s.Device).ThenInclude(d => d.Room)
            .Include(s => s.Device).ThenInclude(d => d.Screens)
            .Include(s => s.PricingPlan)
            .Include(s => s.OpenedByUser)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId && s.Status != SessionStatus.Closed, ct)
            ?? throw new KeyNotFoundException("Active session not found.");

        var plan = await _db.PricingPlans
            .Include(p => p.GamingRates)
            .Include(p => p.WatchingRates)
            .FirstOrDefaultAsync(p => p.Id == request.PricingPlanId && p.IsActive
                && (p.BranchId == null || p.BranchId == branchId), ct)
            ?? throw new KeyNotFoundException("Pricing plan not found.");

        int? nextControllers = null;
        int? nextWatchers = null;

        if (plan.SessionMode == SessionMode.Gaming)
        {
            var controllers = request.ControllerCount
                ?? throw new InvalidOperationException("Controller count is required for gaming plans.");
            if (controllers is < 1 or > 4)
                throw new InvalidOperationException("Controller count must be between 1 and 4.");

            var rateTier = SessionCostCalculator.GamingRateTier(controllers);
            var hasPackage = plan.PackagePrice is > 0 && plan.PackageDurationMinutes is > 0;
            var hasRate = plan.GamingRates.Any(r => r.ControllerCount == rateTier && r.Rate > 0);
            if (!hasRate && !hasPackage)
                throw new InvalidOperationException(
                    $"No gaming rate configured for {(rateTier == 1 ? "individual" : "couple")} in this plan.");

            nextControllers = controllers;
            nextWatchers = null;
        }
        else if (plan.SessionMode == SessionMode.Watching)
        {
            var watchers = request.WatcherCount
                ?? throw new InvalidOperationException("Watcher count is required for watching plans.");
            if (watchers < 1)
                throw new InvalidOperationException("Watcher count must be at least 1.");

            var maxCapacity = GetMaxWatchingCapacity(session.Device);
            if (watchers > maxCapacity)
                throw new InvalidOperationException($"This room supports at most {maxCapacity} watcher(s).");

            if (!plan.WatchingRates.Any(r => r.RatePerPerson > 0))
                throw new InvalidOperationException("No watching rate configured in this plan.");

            nextWatchers = watchers;
            nextControllers = null;
        }
        else
        {
            throw new InvalidOperationException("Unsupported pricing plan mode.");
        }

        // End any active pause and finalize the current billing segment
        if (session.Status == SessionStatus.Paused)
        {
            var activePause = session.Pauses.LastOrDefault(p => p.ResumedAt == null);
            if (activePause is not null)
            {
                activePause.ResumedAt = DateTime.UtcNow;
                session.TotalPausedSeconds += (int)Math.Max(0, (activePause.ResumedAt.Value - activePause.PausedAt).TotalSeconds);
            }
        }

        var convertAt = DateTime.UtcNow;
        var leavingPerGame = session.SessionMode == SessionMode.Gaming
            && _costCalculator.GetTimeUnit(session.RateSnapshot) == TimeUnit.PerGame;

        var segment = SessionBillingSegments.Build(
            _costCalculator, session, convertAt, billingRoundUp,
            leavingPerGame ? request.MatchCount : null,
            out var segmentCost);
        SessionBillingSegments.Append(session, segment);

        session.OriginalStartedAt ??= session.StartedAt;

        var snapshot = JsonSerializer.Serialize(new
        {
            plan.TimeUnit,
            plan.WatchingBilling,
            plan.PackageDurationMinutes,
            plan.PackagePrice,
            GamingRates = plan.GamingRates.Select(r => new { r.ControllerCount, r.Rate }),
            WatchingRates = plan.WatchingRates.Select(r => new { r.RatePerPerson })
        });

        session.SessionMode = plan.SessionMode;
        session.PricingPlanId = plan.Id;
        session.ControllerCount = nextControllers;
        session.WatcherCount = nextWatchers;
        session.RoomSurchargePerHour = 0;
        session.RateSnapshot = snapshot;
        session.StartedAt = convertAt;
        session.TotalPausedSeconds = 0;
        session.PlannedDurationMinutes = null;
        session.Status = SessionStatus.Open;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Session.Converted", "Session", session.Id, new
        {
            SegmentCost = segmentCost,
            session.AccruedTimeCost,
            TargetMode = plan.SessionMode.ToString(),
            request.ControllerCount,
            request.WatcherCount,
            PlanName = plan.Name,
            plan.TimeUnit,
            request.MatchCount
        }, ct: ct);

        await LoadSessionGraphAsync(session, ct);
        var live = MapLive(session, billingRoundUp);
        await _notifier.NotifySessionUpdatedAsync(branchId, live, ct);
        return live;
    }

    public async Task<SessionDetailDto> CloseSessionAsync(Guid id, CloseSessionRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var session = await _db.Sessions
            .Include(s => s.Pauses)
            .Include(s => s.CafeteriaLines)
            .Include(s => s.Device)
            .Include(s => s.Room)
            .Include(s => s.PricingPlan)
            .Include(s => s.OpenedByUser)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId && s.Status != SessionStatus.Closed, ct)
            ?? throw new KeyNotFoundException("Active session not found.");

        if (session.Status == SessionStatus.Paused)
        {
            var activePause = session.Pauses.LastOrDefault(p => p.ResumedAt == null);
            if (activePause is not null)
            {
                activePause.ResumedAt = DateTime.UtcNow;
                session.TotalPausedSeconds += (int)Math.Max(0, (activePause.ResumedAt.Value - activePause.PausedAt).TotalSeconds);
            }
        }

        var closedAt = DateTime.UtcNow;
        var currentTimeUnit = _costCalculator.GetTimeUnit(session.RateSnapshot);
        var isPerGame = session.SessionMode == SessionMode.Gaming && currentTimeUnit == TimeUnit.PerGame;

        var finalSegment = SessionBillingSegments.Build(
            _costCalculator, session, closedAt, billingRoundUp,
            isPerGame ? request.MatchCount : null,
            out var segmentCost,
            chargeFullPlannedBooking: true);
        SessionBillingSegments.Append(session, finalSegment);

        var elapsed = _costCalculator.GetElapsedSeconds(SessionStatus.Open, session.StartedAt, session.TotalPausedSeconds, null, closedAt);
        var billableSeconds = GetBillableSeconds(session.PlannedDurationMinutes, elapsed);
        session.TimeCost = decimal.Round(session.AccruedTimeCost, 2);
        session.RoomSurchargeCost = isPerGame
            ? 0m
            : decimal.Round(CalculateRoomSurcharge(session.RoomSurchargePerHour, billableSeconds), 2);
        session.CafeteriaCost = session.CafeteriaLines.Sum(l => l.LineTotal);

        var subtotal = session.TimeCost + session.RoomSurchargeCost + session.CafeteriaCost;
        if (request.DiscountAmount < 0)
            throw new InvalidOperationException("Discount cannot be negative.");
        if (request.DiscountAmount > subtotal)
            throw new InvalidOperationException("Discount cannot exceed the bill total.");

        session.DiscountAmount = decimal.Round(request.DiscountAmount, 2);
        session.DiscountReason = string.IsNullOrWhiteSpace(request.DiscountReason)
            ? null
            : request.DiscountReason.Trim();
        session.TotalCost = subtotal - session.DiscountAmount;
        session.Status = SessionStatus.Closed;
        session.ClosedAt = closedAt;
        session.ClosedByUserId = _tenantContext.UserId;

        var payment = request.Payment;
        ValidatePayment(payment);

        // Prepaid wallet split: wallet part is settled instantly, remainder via the chosen method.
        var walletAmount = decimal.Round(Math.Max(0, payment.WalletAmount), 2);
        Customer? walletCustomer = null;
        if (walletAmount > 0)
        {
            if (session.CustomerId is null)
                throw new InvalidOperationException("Wallet payment requires a registered customer on the session.");
            if (walletAmount > session.TotalCost)
                throw new InvalidOperationException("Wallet amount cannot exceed the bill total.");

            walletCustomer = await _db.Customers
                .FirstOrDefaultAsync(c => c.Id == session.CustomerId.Value, ct)
                ?? throw new KeyNotFoundException("Customer not found.");

            if (walletCustomer.WalletBalance < walletAmount)
                throw new InvalidOperationException(
                    $"Insufficient wallet balance ({walletCustomer.WalletBalance:0.##}). Reduce the wallet amount or top up.");
        }

        var remainder = session.TotalCost - walletAmount;
        if (remainder > 0 && payment.PaymentMethod == PaymentMethod.CustomerWallet)
            throw new InvalidOperationException("Wallet balance does not cover the bill — choose a method for the remainder.");

        var branch = await _db.Branches.FirstAsync(b => b.Id == branchId, ct);
        var invoiceNumber = $"{branch.InvoicePrefix}-{branch.NextInvoiceNumber:D5}";
        branch.NextInvoiceNumber++;

        var invoiceStatus = remainder > 0 && payment.PaymentMethod == PaymentMethod.Deferred
            ? InvoiceStatus.Deferred
            : InvoiceStatus.Paid;

        var paymentStatus = payment.PaymentMethod switch
        {
            PaymentMethod.Cash => PaymentStatus.Completed,
            PaymentMethod.Deferred => PaymentStatus.Deferred,
            PaymentMethod.BankTransfer or PaymentMethod.DigitalWallet => string.IsNullOrWhiteSpace(payment.ProofFileUrl)
                ? PaymentStatus.PendingVerification
                : PaymentStatus.Completed,
            _ => PaymentStatus.Completed
        };

        var invoice = new Invoice
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            InvoiceNumber = invoiceNumber,
            InvoiceType = InvoiceType.Session,
            SessionId = session.Id,
            Subtotal = subtotal,
            DiscountAmount = session.DiscountAmount,
            DiscountReason = session.DiscountReason,
            Total = session.TotalCost,
            Status = invoiceStatus,
            ClosedByUserId = _tenantContext.UserId,
            ClosedAt = closedAt
        };

        if (walletAmount > 0)
        {
            invoice.Payments.Add(new InvoicePayment
            {
                PaymentMethod = PaymentMethod.CustomerWallet,
                Amount = walletAmount,
                Status = PaymentStatus.Completed
            });
        }

        if (remainder > 0 || walletAmount == 0)
        {
            var invoicePayment = new InvoicePayment
            {
                PaymentMethod = payment.PaymentMethod,
                Amount = remainder,
                Status = paymentStatus,
                DebtorName = payment.DebtorName?.Trim(),
                DebtorPhone = payment.DebtorPhone?.Trim()
            };

            if (payment.PaymentMethod == PaymentMethod.Deferred && session.CustomerId.HasValue)
            {
                invoicePayment.CustomerId = session.CustomerId;
                if (string.IsNullOrWhiteSpace(invoicePayment.DebtorName))
                    invoicePayment.DebtorName = session.Customer?.Name;
                if (string.IsNullOrWhiteSpace(invoicePayment.DebtorPhone))
                    invoicePayment.DebtorPhone = session.Customer?.Phone;
            }

            if (!string.IsNullOrWhiteSpace(payment.ProofFileUrl))
            {
                invoicePayment.Proof = new PaymentProof
                {
                    FileUrl = payment.ProofFileUrl.Trim(),
                    FileName = "receipt",
                    ContentType = "image/jpeg",
                    UploadedByUserId = _tenantContext.UserId
                };
            }

            invoice.Payments.Add(invoicePayment);
        }

        if (walletCustomer is not null && walletAmount > 0)
        {
            walletCustomer.WalletBalance -= walletAmount;
            _db.WalletTransactions.Add(new WalletTransaction
            {
                TenantId = _tenantContext.TenantId,
                BranchId = session.BranchId,
                CustomerId = walletCustomer.Id,
                Type = WalletTransactionType.Payment,
                Amount = -walletAmount,
                BalanceAfter = walletCustomer.WalletBalance,
                Note = invoiceNumber,
                Invoice = invoice,
                CreatedByUserId = _tenantContext.UserId
            });
        }

        var revenue = new RevenueEntry
        {
            TenantId = _tenantContext.TenantId,
            BranchId = branchId,
            Amount = session.TotalCost,
            RevenueType = RevenueType.Session,
            RecordedAt = closedAt
        };

        invoice.RevenueEntry = revenue;
        session.Invoice = invoice;

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Session.Closed", "Session", session.Id, new
        {
            Subtotal = subtotal,
            session.DiscountAmount,
            session.DiscountReason,
            session.TotalCost,
            session.TimeCost,
            session.RoomSurchargeCost,
            session.CafeteriaCost,
            invoice.InvoiceNumber,
            payment.PaymentMethod,
            WalletAmount = walletAmount
        }, ct: ct);

        await _db.Entry(session).Reference(s => s.ClosedByUser).LoadAsync(ct);
        await _db.Entry(session).Collection(s => s.CafeteriaLines).Query().Include(l => l.CafeteriaItem).LoadAsync(ct);
        await _db.Entry(session).Reference(s => s.Invoice).Query().Include(i => i!.Payments).LoadAsync(ct);

        var detail = MapDetail(session);
        await _notifier.NotifySessionClosedAsync(branchId, session.Id, ct);
        return detail;
    }

    public async Task<SessionLiveDto> AddCafeteriaItemAsync(Guid id, AddSessionCafeteriaRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        var session = await GetMutableActiveSessionAsync(id, branchId, ct);
        if (session.Status == SessionStatus.Closed)
            throw new InvalidOperationException("Cannot add items to a closed session.");

        var plan = await CafeteriaStockPlanner.PlanAsync(
            _db,
            branchId,
            request.CafeteriaItemId,
            request.VariantId,
            request.Quantity,
            request.StockDeductQuantity,
            request.Unit,
            request.AddOns,
            request.AllowSkipMissingIngredients,
            ct);

        var line = new SessionCafeteriaLine
        {
            CafeteriaItemId = plan.Item.Id,
            VariantId = plan.Variant.Id,
            VariantName = plan.Variant.Name,
            Quantity = plan.Quantity,
            StockDeductQuantity = plan.ParentStockDeduct,
            UnitPrice = plan.UnitPrice,
            LineTotal = plan.LineTotal,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim(),
            AddedByUserId = _tenantContext.UserId
        };

        foreach (var a in plan.AddOns)
        {
            line.AddOns.Add(new SessionCafeteriaLineAddOn
            {
                AddOnId = a.AddOn.Id,
                Name = a.AddOn.Name,
                Quantity = a.Quantity,
                UnitPrice = a.AddOn.SellPrice,
                LineTotal = a.LineTotal,
                StockDeductQuantity = a.StockDeduct
            });
        }

        session.CafeteriaLines.Add(line);

        CafeteriaStockPlanner.ApplyDeducts(
            _db, _tenantContext, branchId, plan, "Session", session.Id,
            trackSessionIngredient: d => line.IngredientDeducts.Add(d),
            sessionMode: true);

        await _db.SaveChangesAsync(ct);

        foreach (var wh in plan.Ingredients.Select(i => i.WarehouseItem).Append(plan.Item).DistinctBy(x => x.Id))
            await _lowStock.CheckAndNotifyAsync(wh, ct);

        await _audit.LogAsync("Session.CafeteriaAdded", "Session", session.Id,
            new { Item = plan.Item.Name, Variant = plan.Variant.Name, plan.Quantity, plan.ParentStockDeduct, line.CustomerName }, ct: ct);
        await _db.SaveChangesAsync(ct);

        await LoadSessionGraphAsync(session, ct);
        var live = MapLive(session, billingRoundUp);
        await _notifier.NotifySessionUpdatedAsync(branchId, live, ct);
        return live;
    }

    public async Task<SessionLiveDto> ReturnCafeteriaItemAsync(Guid id, ReturnSessionCafeteriaRequest request, CancellationToken ct = default)
    {
        var branchId = await BranchGuard.RequireOwnedBranchIdAsync(_db, _tenantContext, ct);
        var billingRoundUp = await GetBillingRoundUpAsync(ct);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Return reason is required.");

        if (request.Quantity <= 0)
            throw new InvalidOperationException("Return quantity must be positive.");

        var session = await _db.Sessions
            .Include(s => s.Pauses)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.IngredientDeducts).ThenInclude(d => d.WarehouseItem)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.AddOns)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId && s.Status != SessionStatus.Closed, ct)
            ?? throw new KeyNotFoundException("Active session not found. Returns are only allowed on open sessions.");

        var line = session.CafeteriaLines.FirstOrDefault(l => l.Id == request.SessionCafeteriaLineId)
            ?? throw new KeyNotFoundException("Session cafeteria line not found.");

        var returnable = line.Quantity - line.ReturnedQuantity;
        if (request.Quantity > returnable)
            throw new InvalidOperationException($"Invalid return quantity. Maximum returnable: {returnable}.");

        var stockOnLine = line.StockDeductQuantity;
        var stockReturnable = stockOnLine - line.ReturnedStockQuantity;
        var stockRestore = line.Quantity == 0
            ? 0
            : (int)Math.Round((double)stockOnLine * request.Quantity / line.Quantity);
        stockRestore = Math.Clamp(stockRestore, 0, Math.Max(0, stockReturnable));

        var refundAmount = line.UnitPrice * request.Quantity;
        // Add-ons refund (proportional by returned sell qty)
        var addOnRefund = line.AddOns.Sum(a => a.UnitPrice * a.Quantity) * request.Quantity / Math.Max(line.Quantity, 1);
        refundAmount += addOnRefund;

        line.ReturnedQuantity += request.Quantity;
        line.ReturnedStockQuantity += stockRestore;
        var remainingQty = line.Quantity - line.ReturnedQuantity;
        line.LineTotal = line.UnitPrice * remainingQty
            + line.AddOns.Sum(a => a.UnitPrice * a.Quantity) * remainingQty / Math.Max(line.Quantity, 1);
        if (stockRestore > 0)
            line.CafeteriaItem.CurrentQuantity += stockRestore;

        // Restore recipe/add-on ingredient deducts proportionally
        foreach (var ded in line.IngredientDeducts.Where(d => !d.WasSkipped && d.Quantity > 0))
        {
            var restoreable = ded.Quantity - ded.ReturnedQuantity;
            var restore = (int)Math.Round((double)ded.Quantity * request.Quantity / line.Quantity);
            restore = Math.Clamp(restore, 0, restoreable);
            if (restore <= 0) continue;
            ded.ReturnedQuantity += restore;
            ded.WarehouseItem.CurrentQuantity += restore;
            _db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = _tenantContext.TenantId,
                BranchId = branchId,
                CafeteriaItemId = ded.WarehouseItemId,
                MovementType = InventoryMovementType.Return,
                QuantityChange = restore,
                ReferenceType = "SessionCafeteriaReturn",
                ReferenceId = session.Id,
                Notes = request.Reason.Trim(),
                PerformedByUserId = _tenantContext.UserId
            });
        }

        var cafeteriaReturn = new SessionCafeteriaReturn
        {
            SessionId = session.Id,
            SessionCafeteriaLineId = line.Id,
            Quantity = request.Quantity,
            Reason = request.Reason.Trim(),
            ReturnedByUserId = _tenantContext.UserId
        };
        _db.SessionCafeteriaReturns.Add(cafeteriaReturn);

        if (stockRestore > 0)
        {
            _db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = _tenantContext.TenantId,
                BranchId = branchId,
                CafeteriaItemId = line.CafeteriaItemId,
                MovementType = InventoryMovementType.Return,
                QuantityChange = stockRestore,
                ReferenceType = "SessionCafeteriaReturn",
                ReferenceId = cafeteriaReturn.Id,
                Notes = request.Reason.Trim(),
                PerformedByUserId = _tenantContext.UserId
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Session.CafeteriaReturned", "Session", session.Id, new
        {
            line.CafeteriaItem.Name,
            request.Quantity,
            refundAmount
        }, ct: ct);

        await LoadSessionGraphAsync(session, ct);
        var live = MapLive(session, billingRoundUp);
        await _notifier.NotifySessionUpdatedAsync(branchId, live, ct);
        return live;
    }

    private IQueryable<Session> LoadActiveSessionsQuery(Guid branchId) =>
        _db.Sessions
            .Include(s => s.Device)
            .Include(s => s.Room)
            .Include(s => s.PricingPlan)
            .Include(s => s.OpenedByUser)
            .Include(s => s.Customer)
            .Include(s => s.Pauses)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.CafeteriaItem)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.AddOns)
            .Where(s => s.BranchId == branchId && s.Status != SessionStatus.Closed);

    private async Task<Session> GetMutableActiveSessionAsync(Guid id, Guid branchId, CancellationToken ct)
    {
        var session = await _db.Sessions
            .Include(s => s.Pauses)
            .Include(s => s.CafeteriaLines).ThenInclude(l => l.CafeteriaItem)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId && s.Status != SessionStatus.Closed, ct)
            ?? throw new KeyNotFoundException("Active session not found.");

        return session;
    }

    private async Task LoadSessionGraphAsync(Session session, CancellationToken ct)
    {
        await _db.Entry(session).Reference(s => s.Device).LoadAsync(ct);
        await _db.Entry(session).Reference(s => s.Room).LoadAsync(ct);
        await _db.Entry(session).Reference(s => s.PricingPlan).LoadAsync(ct);
        await _db.Entry(session).Reference(s => s.OpenedByUser).LoadAsync(ct);
        if (session.CustomerId.HasValue)
            await _db.Entry(session).Reference(s => s.Customer).LoadAsync(ct);
    }

    private async Task<bool> GetBillingRoundUpAsync(CancellationToken ct)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstAsync(t => t.Id == _tenantContext.TenantId, ct);
        return tenant.BillingRoundUp;
    }

    private static void ValidateSessionCounts(OpenSessionRequest request, Device device)
    {
        if (request.SessionMode == SessionMode.Gaming)
        {
            // Single play: 1-2 controllers, couple play: 3-4 controllers.
            if (request.ControllerCount is null or < 1 or > 4)
                throw new InvalidOperationException("Controller count must be between 1 and 4.");
        }
        else
        {
            if (request.WatcherCount is null or <= 0)
                throw new InvalidOperationException("Watcher count is required for watching sessions.");
            var maxWatchers = GetMaxWatchingCapacity(device);
            if (request.WatcherCount > maxWatchers)
                throw new InvalidOperationException($"This device supports at most {maxWatchers} watcher(s).");
        }
    }

    private static int GetMaxWatchingCapacity(Device device) =>
        device.Room?.MaxWatchingCapacity
        ?? Math.Max(device.Screens.Sum(s => s.WorkingCount), 10);

    /// <summary>Refuse to open a session the plan cannot bill — otherwise it gets stuck (cost calc would fail on every screen).</summary>
    private static void ValidatePlanRates(OpenSessionRequest request, PricingPlan plan)
    {
        if (request.SessionMode == SessionMode.Gaming)
        {
            var tier = SessionCostCalculator.GamingRateTier(request.ControllerCount ?? 1);
            var hasPackage = plan.PackagePrice is > 0 && plan.PackageDurationMinutes is > 0;
            var hasRate = plan.GamingRates.Any(r => r.ControllerCount == tier && r.Rate > 0);
            if (!hasRate && !hasPackage)
                throw new InvalidOperationException(
                    $"Pricing plan '{plan.Name}' has no {(tier == 1 ? "single" : "couple")} rate. Configure the rate first.");
        }
        else if (!plan.WatchingRates.Any(r => r.RatePerPerson > 0))
        {
            throw new InvalidOperationException(
                $"Pricing plan '{plan.Name}' has no watching rate configured. Configure the rate first.");
        }
    }

    private static void ValidatePayment(CloseSessionPaymentRequest payment)
    {
        if (payment.PaymentMethod == PaymentMethod.Deferred && string.IsNullOrWhiteSpace(payment.DebtorName))
            throw new InvalidOperationException("Debtor name is required for deferred payments.");
        // Bank transfer / wallet: receipt image is optional.
    }

    private static int GetBillableSeconds(int? plannedDurationMinutes, int elapsedSeconds)
    {
        if (plannedDurationMinutes is null or < 1)
            return elapsedSeconds;

        var plannedSeconds = plannedDurationMinutes.Value * 60;
        return Math.Max(elapsedSeconds, plannedSeconds);
    }

    /// <summary>VIP room premium, prorated per second on the billable time.</summary>
    private static decimal CalculateRoomSurcharge(decimal surchargePerHour, int billableSeconds) =>
        surchargePerHour <= 0 || billableSeconds <= 0
            ? 0m
            : decimal.Round(surchargePerHour * billableSeconds / 3600m, 2);

    private SessionLiveDto MapLive(Session session, bool billingRoundUp)
    {
        var activePause = session.Pauses.LastOrDefault(p => p.ResumedAt == null);
        var elapsed = _costCalculator.GetElapsedSeconds(
            session.Status, session.StartedAt, session.TotalPausedSeconds, activePause?.PausedAt, session.ClosedAt);

        var billableSeconds = GetBillableSeconds(session.PlannedDurationMinutes, elapsed);
        var timeUnit = _costCalculator.GetTimeUnit(session.RateSnapshot);
        var isPerGame = session.SessionMode == SessionMode.Gaming && timeUnit == TimeUnit.PerGame;
        var segmentCost = session.Status == SessionStatus.Closed
            ? Math.Max(0, session.TimeCost - SessionBillingSegments.Read(session).SkipLast(1).Sum(s => s.Amount))
            : isPerGame
                ? 0m
                : _costCalculator.CalculateTimeCost(
                    session.RateSnapshot, session.SessionMode, billableSeconds,
                    session.ControllerCount, session.WatcherCount, billingRoundUp);
        var timeCost = session.Status == SessionStatus.Closed
            ? session.TimeCost
            : session.AccruedTimeCost + segmentCost;

        var surchargeCost = session.Status == SessionStatus.Closed
            ? session.RoomSurchargeCost
            : CalculateRoomSurcharge(session.RoomSurchargePerHour, billableSeconds);

        var cafeteriaCost = session.CafeteriaLines.Sum(l => l.LineTotal);
        int? remaining = null;
        var timeExpired = false;
        if (session.PlannedDurationMinutes is > 0)
        {
            var plannedSeconds = session.PlannedDurationMinutes.Value * 60;
            remaining = Math.Max(0, plannedSeconds - elapsed);
            timeExpired = elapsed >= plannedSeconds;
        }

        return new SessionLiveDto(
            session.Id,
            session.BranchId,
            session.DeviceId,
            session.Device.Name,
            session.Device.Identifier,
            session.RoomId,
            session.Room?.Name,
            session.SessionMode,
            session.Status,
            session.PricingPlanId,
            session.PricingPlan.Name,
            session.ControllerCount,
            session.WatcherCount,
            session.StartedAt,
            session.OriginalStartedAt,
            session.TotalPausedSeconds,
            activePause?.PausedAt,
            elapsed,
            session.AccruedTimeCost,
            timeCost,
            surchargeCost,
            cafeteriaCost,
            timeCost + surchargeCost + cafeteriaCost,
            session.OpenedByUser.FullName,
            session.PlannedDurationMinutes,
            remaining,
            timeExpired,
            // Watching can convert to gaming; gaming can also switch to watching / match / other plans.
            session.SessionMode == SessionMode.Watching && session.Status != SessionStatus.Closed,
            session.Status != SessionStatus.Closed,
            timeUnit,
            session.SessionMode == SessionMode.Gaming
                ? _costCalculator.GetGamingRate(session.RateSnapshot, session.ControllerCount)
                : session.SessionMode == SessionMode.Watching
                    ? _costCalculator.GetWatchingRatePerPerson(session.RateSnapshot)
                    : null,
            session.SessionMode == SessionMode.Gaming && session.ControllerCount is > 0
                ? (SessionCostCalculator.GamingRateTier(session.ControllerCount.Value) == 2 ? "Couple" : "Individual")
                : session.SessionMode == SessionMode.Watching
                    ? $"Watching×{session.WatcherCount ?? 0}"
                    : null,
            session.CustomerId,
            session.Customer?.Code,
            session.IsQuickGuest ? session.QuickGuestName : session.Customer?.Name,
            session.Customer?.Phone,
            session.IsQuickGuest,
            session.QuickGuestName,
            session.CafeteriaLines.Select(MapCafeteriaLine).ToList());
    }

    private static SessionCafeteriaLineDto MapCafeteriaLine(SessionCafeteriaLine l) =>
        new(
            l.Id,
            l.CafeteriaItemId,
            l.VariantName is null ? l.CafeteriaItem.Name : $"{l.CafeteriaItem.Name} — {l.VariantName}",
            l.VariantId,
            l.VariantName,
            l.Quantity,
            l.StockDeductQuantity,
            l.ReturnedQuantity,
            l.UnitPrice,
            l.LineTotal,
            l.CustomerName,
            l.AddedAt,
            (l.AddOns ?? [])
                .Select(a => new CafeteriaSaleLineAddOnDto(
                    a.Id, a.AddOnId, a.Name, a.Quantity, a.UnitPrice, a.LineTotal, a.StockDeductQuantity))
                .ToList());

    private static SessionHistoryDto MapHistory(Session session)
    {
        var cafeteriaCost = session.Status == SessionStatus.Closed
            ? session.CafeteriaCost
            : session.CafeteriaLines.Sum(l => l.LineTotal);
        var timeCost = session.Status == SessionStatus.Closed ? session.TimeCost : 0m;
        var total = session.Status == SessionStatus.Closed
            ? session.TotalCost
            : timeCost + cafeteriaCost;

        return new SessionHistoryDto(
            session.Id,
            session.DeviceId,
            session.Device.Name,
            session.Room?.Name,
            session.Branch?.Name,
            session.SessionMode,
            session.Status,
            session.StartedAt,
            session.ClosedAt,
            session.OpenedByUser.FullName,
            session.ClosedByUser?.FullName,
            timeCost,
            cafeteriaCost,
            total,
            session.CustomerId,
            session.IsQuickGuest ? session.QuickGuestName : session.Customer?.Name,
            session.IsQuickGuest,
            session.QuickGuestName);
    }

    private static SessionDetailDto MapDetail(Session session)
    {
        SessionInvoiceDto? invoiceDto = null;
        if (session.Invoice is not null)
        {
            // Prefer the non-wallet payment so the receipt shows the cash/transfer method for split payments.
            var payment = session.Invoice.Payments
                .OrderBy(p => p.PaymentMethod == PaymentMethod.CustomerWallet ? 1 : 0)
                .FirstOrDefault();
            invoiceDto = new SessionInvoiceDto(
                session.Invoice.Id,
                session.Invoice.InvoiceNumber,
                session.Invoice.Total,
                payment?.PaymentMethod ?? PaymentMethod.Cash,
                payment?.Status ?? PaymentStatus.Completed);
        }

        return new SessionDetailDto(
            session.Id,
            session.BranchId,
            session.DeviceId,
            session.Device.Name,
            session.RoomId,
            session.Room?.Name,
            session.SessionMode,
            session.Status,
            session.PricingPlanId,
            session.PricingPlan.Name,
            session.ControllerCount,
            session.WatcherCount,
            session.StartedAt,
            session.OriginalStartedAt,
            session.ClosedAt,
            session.TotalPausedSeconds,
            session.AccruedTimeCost,
            session.TimeCost,
            session.RoomSurchargeCost,
            session.CafeteriaCost,
            session.DiscountAmount,
            session.DiscountReason,
            session.TotalCost,
            session.OpenedByUser.FullName,
            session.ClosedByUser?.FullName,
            session.PlannedDurationMinutes,
            session.CustomerId,
            session.Customer?.Code,
            session.IsQuickGuest ? session.QuickGuestName : session.Customer?.Name,
            session.Customer?.Phone,
            session.IsQuickGuest,
            session.QuickGuestName,
            session.Invoice?.InvoiceNumber,
            SessionBillingSegments.Read(session),
            session.CafeteriaLines.Select(MapCafeteriaLine).ToList(),
            invoiceDto);
    }
}
