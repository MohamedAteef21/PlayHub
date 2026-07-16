import type { CafeteriaItem } from '@/types';
import { InventoryUnitKind } from '@/types';

export function hasLargeUnit(item: CafeteriaItem): boolean {
  return !!item.largeUnitName && item.unitsPerLarge > 1;
}

export function toBaseQuantity(
  item: CafeteriaItem,
  quantity: number,
  unit: InventoryUnitKind
): number {
  if (unit === InventoryUnitKind.Large && hasLargeUnit(item)) {
    return quantity * item.unitsPerLarge;
  }
  return quantity;
}

export function maxSellQuantity(item: CafeteriaItem, unit: InventoryUnitKind): number {
  if (unit === InventoryUnitKind.Large && hasLargeUnit(item)) {
    return Math.floor(item.currentQuantity / item.unitsPerLarge);
  }
  return item.currentQuantity;
}

export function lineUnitPrice(item: CafeteriaItem, unit: InventoryUnitKind): number {
  if (unit === InventoryUnitKind.Large && hasLargeUnit(item)) {
    return item.sellPrice * item.unitsPerLarge;
  }
  return item.sellPrice;
}

export function formatStockDisplay(item: CafeteriaItem): string {
  const base = `${item.currentQuantity} ${item.baseUnitName || ''}`.trim();
  if (!hasLargeUnit(item)) return base;
  const large = Math.floor(item.currentQuantity / item.unitsPerLarge);
  const rem = item.currentQuantity % item.unitsPerLarge;
  if (rem === 0) return `${base} (${large} ${item.largeUnitName})`;
  return `${base} (${large} ${item.largeUnitName} + ${rem})`;
}

export function unitLabel(item: CafeteriaItem, unit: InventoryUnitKind): string {
  if (unit === InventoryUnitKind.Large && hasLargeUnit(item)) {
    return item.largeUnitName!;
  }
  return item.baseUnitName || '';
}
