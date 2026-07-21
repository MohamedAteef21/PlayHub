import type { CafeteriaItem } from '@/types';
import { InventoryUnitKind } from '@/types';

/** How a recipe line quantity is entered in the UI (converted to warehouse base units on save). */
export type RecipeDeductUnit = 'base' | 'gram' | 'kilogram';

const GRAMS_PER_KG = 1000;

export function hasLargeUnit(item: CafeteriaItem): boolean {
  return !!item.largeUnitName && item.unitsPerLarge > 1;
}

function normalizeUnitName(name: string | null | undefined): string {
  return (name ?? '').trim().toLowerCase();
}

export function isGramUnitName(name: string | null | undefined): boolean {
  const n = normalizeUnitName(name);
  return n === 'جرام' || n === 'جم' || n === 'g' || n === 'gram' || n === 'grams';
}

export function isKilogramUnitName(name: string | null | undefined): boolean {
  const n = normalizeUnitName(name);
  return (
    n === 'كجم' ||
    n === 'كيلو' ||
    n === 'كيلوجرام' ||
    n === 'كيلو جرام' ||
    n === 'kg' ||
    n === 'kilo' ||
    n === 'kilogram' ||
    n === 'kilograms'
  );
}

export function isWeightItem(item: Pick<CafeteriaItem, 'baseUnitName' | 'largeUnitName'>): boolean {
  return isGramUnitName(item.baseUnitName) || isKilogramUnitName(item.baseUnitName)
    || isGramUnitName(item.largeUnitName) || isKilogramUnitName(item.largeUnitName);
}

/** Convert a recipe UI quantity (gram/kg/base) into integer warehouse base units. */
export function recipeQtyToBase(
  item: Pick<CafeteriaItem, 'baseUnitName'>,
  quantity: number,
  deductUnit: RecipeDeductUnit
): number {
  const qty = Number(quantity);
  if (!Number.isFinite(qty) || qty <= 0) return 0;

  if (deductUnit === 'base') return Math.round(qty);

  const asGrams = deductUnit === 'kilogram' ? qty * GRAMS_PER_KG : qty;

  if (isGramUnitName(item.baseUnitName)) return Math.round(asGrams);
  if (isKilogramUnitName(item.baseUnitName)) {
    // Prefer جرام as base for weight stock; fall back to rounded kg.
    return Math.max(0, Math.round(asGrams / GRAMS_PER_KG));
  }
  return Math.round(qty);
}

/** Prefer showing weight recipe lines in grams when the warehouse item is weight-based. */
export function recipeQtyFromBase(
  item: Pick<CafeteriaItem, 'baseUnitName'>,
  baseQuantity: number
): { quantity: string; deductUnit: RecipeDeductUnit } {
  if (isGramUnitName(item.baseUnitName)) {
    return { quantity: String(baseQuantity), deductUnit: 'gram' };
  }
  if (isKilogramUnitName(item.baseUnitName)) {
    return { quantity: String(baseQuantity), deductUnit: 'kilogram' };
  }
  return { quantity: String(baseQuantity), deductUnit: 'base' };
}

export function toBaseQuantity(
  item: Pick<CafeteriaItem, 'largeUnitName' | 'unitsPerLarge'> | CafeteriaItem,
  quantity: number,
  unit: InventoryUnitKind
): number {
  const qty = Number(quantity);
  if (!Number.isFinite(qty)) return 0;
  const factor = item.unitsPerLarge > 1 ? item.unitsPerLarge : 1;
  const canLarge = !!item.largeUnitName && factor > 1;
  if (unit === InventoryUnitKind.Large && canLarge) {
    return Math.round(qty * factor);
  }
  return Math.round(qty);
}

/** Convert an entered qty to warehouse base using form unit ids (before item exists). */
export function enteredQtyToBase(
  quantity: number,
  unit: InventoryUnitKind,
  unitsPerLarge: number,
  hasLarge: boolean
): number {
  const qty = Number(quantity);
  if (!Number.isFinite(qty)) return 0;
  const factor = unitsPerLarge > 1 ? unitsPerLarge : 1;
  if (unit === InventoryUnitKind.Large && hasLarge && factor > 1) {
    return Math.round(qty * factor);
  }
  return Math.round(qty);
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
