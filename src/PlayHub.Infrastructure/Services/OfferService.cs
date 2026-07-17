using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Common;
using PlayHub.Application.Offers;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

public class OfferService : IOfferService
{
    private readonly PlayHubDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IAuditService _audit;

    public OfferService(PlayHubDbContext db, TenantContext tenantContext, IAuditService audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<OfferDto>> GetAllAsync(bool? activeOnly = null, CancellationToken ct = default)
    {
        var query = _db.CustomerOffers.AsNoTracking().AsQueryable();
        var ownerFilter = await OwnerScope.ResolveCatalogOwnerFilterAsync(_db, _tenantContext, ct);
        if (ownerFilter.HasValue)
            query = query.Where(o => o.OwnerUserId == ownerFilter.Value);

        if (activeOnly == true)
            query = query.Where(o => o.IsActive);

        var items = await query.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<OfferDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var offer = await _db.CustomerOffers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (offer is null) return null;

        if (!_tenantContext.IsSuperAdmin)
        {
            var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
            if (!OwnerScope.CanAccess(offer.OwnerUserId, ownerId, false))
                return null;
        }

        return Map(offer);
    }

    public async Task<OfferDto> CreateAsync(CreateOfferRequest request, CancellationToken ct = default)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Offer title is required.");
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException("Offer message is required.");

        var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
        var offer = new CustomerOffer
        {
            TenantId = _tenantContext.TenantId,
            OwnerUserId = ownerId,
            Title = title,
            Message = message,
            IsActive = request.IsActive
        };

        _db.CustomerOffers.Add(offer);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Offer.Created", "CustomerOffer", offer.Id, new { offer.Title }, ct: ct);
        return Map(offer);
    }

    public async Task<OfferDto> UpdateAsync(Guid id, UpdateOfferRequest request, CancellationToken ct = default)
    {
        var offer = await RequireOwnedAsync(id, ct);

        var title = request.Title?.Trim() ?? string.Empty;
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Offer title is required.");
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException("Offer message is required.");

        offer.Title = title;
        offer.Message = message;
        offer.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Offer.Updated", "CustomerOffer", offer.Id, new { offer.Title, offer.IsActive }, ct: ct);
        return Map(offer);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var offer = await RequireOwnedAsync(id, ct);

        offer.MarkAsDeleted(_tenantContext.UserId == Guid.Empty ? null : _tenantContext.UserId);
        offer.IsActive = false;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Offer.SoftDeleted", "CustomerOffer", offer.Id, new { offer.Title }, ct: ct);
    }

    private async Task<CustomerOffer> RequireOwnedAsync(Guid id, CancellationToken ct)
    {
        var offer = await _db.CustomerOffers.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new KeyNotFoundException("Offer not found.");

        if (!_tenantContext.IsSuperAdmin)
        {
            var ownerId = await OwnerScope.ResolveBusinessOwnerIdAsync(_db, _tenantContext, ct);
            if (!OwnerScope.CanAccess(offer.OwnerUserId, ownerId, false))
                throw new KeyNotFoundException("Offer not found.");
        }

        return offer;
    }

    private static OfferDto Map(CustomerOffer o) =>
        new(o.Id, o.Title, o.Message, o.IsActive, o.CreatedAt);
}
