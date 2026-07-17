using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Pricing;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class PricingService : IPricingService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public PricingService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<PricingPlanDto>> GetPlansAsync(SessionMode? mode = null, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var query = _db.PricingPlans
            .Include(p => p.GamingRates)
            .Include(p => p.WatchingRates)
            .Where(p => p.IsActive && (p.BranchId == branchId || (p.BranchId == null && _tenantContext.IsSuperAdmin)));

        if (mode.HasValue)
            query = query.Where(p => p.SessionMode == mode.Value);

        var plans = await query.OrderBy(p => p.Name).ToListAsync(ct);
        return plans.Select(MapPlan).ToList();
    }

    public async Task<PricingPlanDto?> GetPlanByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var plan = await _db.PricingPlans
            .Include(p => p.GamingRates)
            .Include(p => p.WatchingRates)
            .FirstOrDefaultAsync(p => p.Id == id && (p.BranchId == branchId || (p.BranchId == null && _tenantContext.IsSuperAdmin)), ct);

        return plan is null ? null : MapPlan(plan);
    }

    public async Task<PricingPlanDto> CreatePlanAsync(CreatePricingPlanRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        if (request.BranchId.HasValue && request.BranchId.Value != branchId)
            throw new InvalidOperationException("Pricing plan branch must match the active branch.");

        ValidatePlan(request.SessionMode, request.TimeUnit, request.WatchingBilling, request.GamingRates, request.WatchingRates,
            request.PackageDurationMinutes, request.PackagePrice);

        var plan = new PricingPlan
        {
            TenantId = _tenantContext.TenantId,
            BranchId = request.BranchId ?? branchId,
            Name = request.Name.Trim(),
            SessionMode = request.SessionMode,
            TimeUnit = request.TimeUnit == TimeUnit.PerMinute ? TimeUnit.PerHour : request.TimeUnit,
            WatchingBilling = request.SessionMode == SessionMode.Watching
                ? request.WatchingBilling
                : WatchingBilling.PerPerson,
            PackageDurationMinutes = request.SessionMode == SessionMode.Gaming ? request.PackageDurationMinutes : null,
            PackagePrice = request.SessionMode == SessionMode.Gaming ? request.PackagePrice : null,
            VipSurchargePerHour = Math.Max(0, request.VipSurchargePerHour),
        };

        ApplyRates(plan, request.GamingRates, request.WatchingRates);
        _db.PricingPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PricingPlan.Created", "PricingPlan", plan.Id, new { plan.Name, plan.SessionMode }, ct: ct);

        return MapPlan(plan);
    }

    public async Task<PricingPlanDto> UpdatePlanAsync(Guid id, UpdatePricingPlanRequest request, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);

        var plan = await _db.PricingPlans
            .Include(p => p.GamingRates)
            .Include(p => p.WatchingRates)
            .FirstOrDefaultAsync(p => p.Id == id && (p.BranchId == branchId || (p.BranchId == null && _tenantContext.IsSuperAdmin)), ct)
            ?? throw new KeyNotFoundException("Pricing plan not found.");

        if (request.SessionMode != plan.SessionMode &&
            await _db.Sessions.AnyAsync(s => s.PricingPlanId == id && s.Status != SessionStatus.Closed, ct))
        {
            throw new InvalidOperationException(
                "Cannot change play/watching mode while this plan is used by an open session. Close the session first.");
        }

        ValidatePlan(request.SessionMode, request.TimeUnit, request.WatchingBilling, request.GamingRates, request.WatchingRates,
            request.PackageDurationMinutes, request.PackagePrice);

        plan.Name = request.Name.Trim();
        plan.SessionMode = request.SessionMode;
        plan.TimeUnit = request.TimeUnit == TimeUnit.PerMinute ? TimeUnit.PerHour : request.TimeUnit;
        plan.WatchingBilling = request.SessionMode == SessionMode.Watching
            ? request.WatchingBilling
            : WatchingBilling.PerPerson;
        plan.PackageDurationMinutes = request.SessionMode == SessionMode.Gaming ? request.PackageDurationMinutes : null;
        plan.PackagePrice = request.SessionMode == SessionMode.Gaming ? request.PackagePrice : null;
        plan.VipSurchargePerHour = Math.Max(0, request.VipSurchargePerHour);
        plan.IsActive = request.IsActive;

        _db.GamingRates.RemoveRange(plan.GamingRates);
        _db.WatchingRates.RemoveRange(plan.WatchingRates);
        plan.GamingRates.Clear();
        plan.WatchingRates.Clear();
        ApplyRates(plan, request.GamingRates, request.WatchingRates);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PricingPlan.Updated", "PricingPlan", plan.Id, new { plan.Name, plan.SessionMode, plan.IsActive }, ct: ct);

        return MapPlan(plan);
    }

    public async Task SoftDeletePlanAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = BranchGuard.RequireBranchId(_tenantContext);
        var plan = await _db.PricingPlans
            .FirstOrDefaultAsync(p => p.Id == id && (p.BranchId == branchId || (p.BranchId == null && _tenantContext.IsSuperAdmin)), ct)
            ?? throw new KeyNotFoundException("Pricing plan not found.");

        if (await _db.Sessions.AnyAsync(s => s.PricingPlanId == id && s.Status != SessionStatus.Closed, ct))
            throw new InvalidOperationException("Cannot delete a pricing plan used by an active session.");

        plan.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        plan.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PricingPlan.SoftDeleted", "PricingPlan", plan.Id, new { plan.Name }, ct: ct);
    }

    private static void ValidatePlan(
        SessionMode mode,
        TimeUnit timeUnit,
        WatchingBilling watchingBilling,
        IReadOnlyList<GamingRateInput>? gamingRates,
        IReadOnlyList<WatchingRateInput>? watchingRates,
        int? packageDurationMinutes = null,
        decimal? packagePrice = null)
    {
        // Per-minute billing is retired — keep enum for legacy snapshots only.
        if (timeUnit == TimeUnit.PerMinute)
            timeUnit = TimeUnit.PerHour;

        if (timeUnit is not (TimeUnit.PerHour or TimeUnit.PerGame))
            throw new InvalidOperationException("Invalid time unit. Use per hour or per game.");

        var isPackage = packageDurationMinutes.HasValue || packagePrice.HasValue;
        if (isPackage)
        {
            if (mode != SessionMode.Gaming)
                throw new InvalidOperationException("Packages are only supported on gaming plans.");
            if (timeUnit == TimeUnit.PerGame)
                throw new InvalidOperationException("Package plans cannot use per-game billing.");
            if (packageDurationMinutes is null or < 15)
                throw new InvalidOperationException("Package duration must be at least 15 minutes.");
            if (packagePrice is null or <= 0)
                throw new InvalidOperationException("Package single (individual) price must be greater than zero.");

            // For packages, gaming rates hold flat individual/couple package prices (not hourly).
            var single = gamingRates?.FirstOrDefault(r => r.ControllerCount == 1)?.Rate ?? 0;
            var couple = gamingRates?.FirstOrDefault(r => r.ControllerCount == 2)?.Rate ?? 0;
            if (single <= 0)
                throw new InvalidOperationException("Package individual price is required.");
            if (couple <= 0)
                throw new InvalidOperationException("Package couple price is required.");
        }
        else if (mode == SessionMode.Gaming && (gamingRates is null || gamingRates.Count == 0))
        {
            throw new InvalidOperationException("Gaming plans require individual and couple rates.");
        }

        if (mode == SessionMode.Watching && (watchingRates is null || watchingRates.Count == 0))
            throw new InvalidOperationException("Watching plans require a rate.");

        if (mode == SessionMode.Watching &&
            watchingBilling is not (WatchingBilling.PerPerson or WatchingBilling.PerScreen))
            throw new InvalidOperationException("Invalid watching billing mode.");

        if (mode == SessionMode.Watching &&
            watchingBilling == WatchingBilling.PerScreen &&
            timeUnit == TimeUnit.PerGame)
            throw new InvalidOperationException("Screen watching plans must use per hour (not per game).");
    }

    private static void ApplyRates(
        PricingPlan plan,
        IReadOnlyList<GamingRateInput>? gamingRates,
        IReadOnlyList<WatchingRateInput>? watchingRates)
    {
        if (gamingRates is not null)
        {
            foreach (var rate in gamingRates.OrderBy(r => r.ControllerCount))
            {
                plan.GamingRates.Add(new GamingRate
                {
                    ControllerCount = rate.ControllerCount,
                    Rate = rate.Rate
                });
            }
        }

        if (watchingRates is not null)
        {
            foreach (var rate in watchingRates)
            {
                plan.WatchingRates.Add(new WatchingRate { RatePerPerson = rate.RatePerPerson });
            }
        }
    }

    private static PricingPlanDto MapPlan(PricingPlan plan) =>
        new(
            plan.Id,
            plan.BranchId,
            plan.Name,
            plan.SessionMode,
            plan.TimeUnit,
            plan.WatchingBilling,
            plan.PackageDurationMinutes,
            plan.PackagePrice,
            plan.VipSurchargePerHour,
            plan.IsActive,
            plan.GamingRates.OrderBy(r => r.ControllerCount).Select(r => new GamingRateDto(r.ControllerCount, r.Rate)).ToList(),
            plan.WatchingRates.Select(r => new WatchingRateDto(r.RatePerPerson)).ToList(),
            plan.CreatedAt);
}
