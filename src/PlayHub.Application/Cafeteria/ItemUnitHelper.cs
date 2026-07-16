using PlayHub.Domain.Entities;
using PlayHub.Domain.Enums;

namespace PlayHub.Application.Cafeteria;

public static class ItemUnitHelper
{
    public static void NormalizeUnits(
        ref string baseUnitName,
        ref string? largeUnitName,
        ref int unitsPerLarge)
    {
        baseUnitName = string.IsNullOrWhiteSpace(baseUnitName) ? "قطعة" : baseUnitName.Trim();
        largeUnitName = string.IsNullOrWhiteSpace(largeUnitName) ? null : largeUnitName.Trim();

        if (largeUnitName is null)
        {
            unitsPerLarge = 1;
            return;
        }

        if (unitsPerLarge < 2)
            throw new InvalidOperationException("Units per large unit must be at least 2 when a large unit is set.");
    }

    public static bool HasLargeUnit(CafeteriaItem item) =>
        !string.IsNullOrWhiteSpace(item.LargeUnitName) && item.UnitsPerLarge >= 2;

    public static int ToBaseQuantity(CafeteriaItem item, int quantity, InventoryUnitKind unit)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be positive.");

        if (unit == InventoryUnitKind.Base)
            return quantity;

        if (!HasLargeUnit(item))
            throw new InvalidOperationException($"{item.Name} has no large unit configured.");

        return checked(quantity * item.UnitsPerLarge);
    }

    public static decimal UnitPriceFor(CafeteriaItem item, InventoryUnitKind unit) =>
        unit == InventoryUnitKind.Large && HasLargeUnit(item)
            ? item.SellPrice * item.UnitsPerLarge
            : item.SellPrice;
}
