using Microsoft.EntityFrameworkCore;
using PlayHub.Application.Cafeteria;
using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;
using PlayHub.Infrastructure.Data;

namespace PlayHub.Infrastructure.Services;

/// <summary>Shared sell-time stock planning for walk-in and session cafeteria lines.</summary>
public static class CafeteriaStockPlanner
{
    public sealed record PlannedAddOn(
        CafeteriaAddOn AddOn,
        int Quantity,
        int StockDeduct,
        bool Skipped,
        decimal LineTotal);

    public sealed record PlannedIngredient(
        CafeteriaItem WarehouseItem,
        int Required,
        int Deduct,
        bool Skipped);

    public sealed record SaleLinePlan(
        CafeteriaItem Item,
        CafeteriaItemVariant Variant,
        int Quantity,
        int ParentStockDeduct,
        /// <summary>Warehouse item that receives parent deduct (linked sell-as-is), else the item itself.</summary>
        CafeteriaItem? ParentStockTarget,
        decimal UnitPrice,
        decimal ProductTotal,
        decimal AddOnsTotal,
        decimal LineTotal,
        IReadOnlyList<PlannedAddOn> AddOns,
        IReadOnlyList<PlannedIngredient> Ingredients);

    public static async Task<SaleLinePlan> PlanAsync(
        PlayHubDbContext db,
        Guid branchId,
        Guid itemId,
        Guid variantId,
        int quantity,
        int stockDeductQuantity,
        InventoryUnitKind unit,
        IReadOnlyList<CafeteriaSaleLineAddOnInput>? addOns,
        bool allowSkipMissing,
        CancellationToken ct)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be at least 1.");

        var item = await db.CafeteriaItems
            .Include(i => i.LinkedWarehouseItem)
            .Include(i => i.Variants).ThenInclude(v => v.RecipeLines).ThenInclude(r => r.WarehouseItem)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.BranchId == branchId && i.IsActive, ct)
            ?? throw new KeyNotFoundException("Cafeteria item not found.");

        if (item.Kind == CafeteriaItemKind.Warehouse)
            throw new InvalidOperationException("Warehouse items cannot be sold directly. Configure a product first.");

        var variant = item.Variants.FirstOrDefault(v => v.Id == variantId && v.IsActive)
            ?? throw new KeyNotFoundException("Product option not found for this item.");

        var recipeLines = variant.RecipeLines.Where(r => r.Quantity > 0).ToList();
        var hasRecipe = recipeLines.Count > 0;

        var requiredByWarehouse = new Dictionary<Guid, int>();
        void AddNeed(Guid warehouseId, int qty)
        {
            requiredByWarehouse[warehouseId] = requiredByWarehouse.GetValueOrDefault(warehouseId) + qty;
        }

        if (hasRecipe)
        {
            foreach (var line in recipeLines)
            {
                var wh = line.WarehouseItem
                    ?? await db.CafeteriaItems.FirstAsync(i => i.Id == line.WarehouseItemId, ct);
                var perPortion = ItemUnitHelper.ToBaseQuantity(wh, line.Quantity, line.Unit);
                AddNeed(line.WarehouseItemId, checked(perPortion * quantity));
            }
        }

        var plannedAddOns = new List<PlannedAddOn>();
        decimal addOnsTotal = 0;
        if (addOns is { Count: > 0 })
        {
            foreach (var input in addOns)
            {
                if (input.Quantity <= 0)
                    throw new InvalidOperationException("Add-on quantity must be at least 1.");

                var addOn = await db.CafeteriaAddOns
                    .Include(a => a.WarehouseItem)
                    .FirstOrDefaultAsync(a => a.Id == input.AddOnId && a.BranchId == branchId && a.IsActive, ct)
                    ?? throw new KeyNotFoundException("Add-on not found.");

                var perAddOn = ItemUnitHelper.ToBaseQuantity(
                    addOn.WarehouseItem, addOn.DeductQuantity, addOn.DeductUnit);
                var deduct = checked(perAddOn * input.Quantity);
                AddNeed(addOn.WarehouseItemId, deduct);
                var lineTotal = addOn.SellPrice * input.Quantity;
                addOnsTotal += lineTotal;
                plannedAddOns.Add(new PlannedAddOn(addOn, input.Quantity, deduct, false, lineTotal));
            }
        }

        var warehouseIds = requiredByWarehouse.Keys.ToList();
        var warehouses = warehouseIds.Count == 0
            ? new Dictionary<Guid, CafeteriaItem>()
            : await db.CafeteriaItems
                .Where(i => warehouseIds.Contains(i.Id) && i.BranchId == branchId)
                .ToDictionaryAsync(i => i.Id, ct);

        var missing = new List<MissingIngredientDto>();
        var ingredients = new List<PlannedIngredient>();

        foreach (var (warehouseId, required) in requiredByWarehouse)
        {
            if (!warehouses.TryGetValue(warehouseId, out var wh) || !wh.IsActive)
                throw new InvalidOperationException("A recipe/add-on warehouse item is missing or inactive.");

            var available = wh.CurrentQuantity;
            if (available < required)
            {
                missing.Add(new MissingIngredientDto(wh.Id, wh.Name, required, available));
                ingredients.Add(new PlannedIngredient(wh, required, allowSkipMissing ? 0 : required, allowSkipMissing));
            }
            else
            {
                ingredients.Add(new PlannedIngredient(wh, required, required, false));
            }
        }

        if (missing.Count > 0 && !allowSkipMissing)
            throw new MissingIngredientsException(missing);

        var skippedIds = ingredients.Where(i => i.Skipped).Select(i => i.WarehouseItem.Id).ToHashSet();
        for (var i = 0; i < plannedAddOns.Count; i++)
        {
            var a = plannedAddOns[i];
            if (skippedIds.Contains(a.AddOn.WarehouseItemId))
                plannedAddOns[i] = a with { Skipped = true, StockDeduct = 0 };
        }

        int parentStockDeduct = 0;
        CafeteriaItem? parentStockTarget = null;
        decimal unitPrice = variant.SellPrice;

        if (!hasRecipe)
        {
            // Sell-as-is: deduct from linked warehouse (preferred) or parent stock.
            parentStockTarget = item.LinkedWarehouseItemId.HasValue
                ? item.LinkedWarehouseItem
                    ?? await db.CafeteriaItems.FirstAsync(i => i.Id == item.LinkedWarehouseItemId, ct)
                : item;

            var stockSource = parentStockTarget;
            var portions = stockDeductQuantity > 0 ? stockDeductQuantity : quantity;
            parentStockDeduct = ItemUnitHelper.ToBaseQuantity(stockSource, portions, unit);

            unitPrice = unit == InventoryUnitKind.Large
                ? (item.LargeSellPrice ?? item.BaseSellPrice ?? variant.SellPrice)
                : (item.BaseSellPrice ?? variant.SellPrice);

            if (stockSource.CurrentQuantity < parentStockDeduct)
            {
                if (!allowSkipMissing)
                {
                    throw new MissingIngredientsException([
                        new MissingIngredientDto(stockSource.Id, stockSource.Name, parentStockDeduct, stockSource.CurrentQuantity)
                    ]);
                }

                parentStockDeduct = 0;
            }
        }

        var productTotal = unitPrice * quantity;
        return new SaleLinePlan(
            item,
            variant,
            quantity,
            parentStockDeduct,
            parentStockTarget,
            unitPrice,
            productTotal,
            addOnsTotal,
            productTotal + addOnsTotal,
            plannedAddOns,
            ingredients);
    }

    public static void ApplyDeducts(
        PlayHubDbContext db,
        TenantContext tenant,
        Guid branchId,
        SaleLinePlan plan,
        string referenceType,
        Guid referenceId,
        Action<CafeteriaSaleLineIngredientDeduct>? trackSaleIngredient = null,
        Action<SessionCafeteriaLineIngredientDeduct>? trackSessionIngredient = null,
        Action<CafeteriaHoldLineIngredientDeduct>? trackHoldIngredient = null,
        bool sessionMode = false,
        bool holdMode = false)
    {
        if (plan.ParentStockDeduct > 0)
        {
            var target = plan.ParentStockTarget ?? plan.Item;
            target.CurrentQuantity -= plan.ParentStockDeduct;
            db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = tenant.TenantId,
                BranchId = branchId,
                CafeteriaItemId = target.Id,
                MovementType = InventoryMovementType.Sale,
                QuantityChange = -plan.ParentStockDeduct,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                Notes = $"{plan.Item.Name} — {plan.Variant.Name}; sold {plan.Quantity}, stock -{plan.ParentStockDeduct}",
                PerformedByUserId = tenant.UserId
            });
        }

        foreach (var ing in plan.Ingredients)
        {
            if (ing.Skipped || ing.Deduct <= 0)
            {
                if (holdMode)
                {
                    trackHoldIngredient?.Invoke(new CafeteriaHoldLineIngredientDeduct
                    {
                        WarehouseItemId = ing.WarehouseItem.Id,
                        Quantity = 0,
                        WasSkipped = true
                    });
                }
                else if (sessionMode)
                {
                    trackSessionIngredient?.Invoke(new SessionCafeteriaLineIngredientDeduct
                    {
                        WarehouseItemId = ing.WarehouseItem.Id,
                        Quantity = 0,
                        WasSkipped = true
                    });
                }
                else
                {
                    trackSaleIngredient?.Invoke(new CafeteriaSaleLineIngredientDeduct
                    {
                        WarehouseItemId = ing.WarehouseItem.Id,
                        Quantity = 0,
                        WasSkipped = true
                    });
                }

                continue;
            }

            ing.WarehouseItem.CurrentQuantity -= ing.Deduct;
            db.InventoryMovements.Add(new InventoryMovement
            {
                TenantId = tenant.TenantId,
                BranchId = branchId,
                CafeteriaItemId = ing.WarehouseItem.Id,
                MovementType = InventoryMovementType.Sale,
                QuantityChange = -ing.Deduct,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                Notes = $"Recipe/add-on for {plan.Item.Name} — {plan.Variant.Name}",
                PerformedByUserId = tenant.UserId
            });

            if (holdMode)
            {
                trackHoldIngredient?.Invoke(new CafeteriaHoldLineIngredientDeduct
                {
                    WarehouseItemId = ing.WarehouseItem.Id,
                    Quantity = ing.Deduct,
                    WasSkipped = false
                });
            }
            else if (sessionMode)
            {
                trackSessionIngredient?.Invoke(new SessionCafeteriaLineIngredientDeduct
                {
                    WarehouseItemId = ing.WarehouseItem.Id,
                    Quantity = ing.Deduct,
                    WasSkipped = false
                });
            }
            else
            {
                trackSaleIngredient?.Invoke(new CafeteriaSaleLineIngredientDeduct
                {
                    WarehouseItemId = ing.WarehouseItem.Id,
                    Quantity = ing.Deduct,
                    WasSkipped = false
                });
            }
        }
    }
}
