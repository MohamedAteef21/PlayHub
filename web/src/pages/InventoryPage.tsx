import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cafeteriaApi, inventoryApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import type { CafeteriaAddOn, CafeteriaItem, InventoryUnit, StockVoucher } from '@/types';
import {
  CafeteriaItemKind,
  InventoryMovementType,
  InventoryUnitKind,
  StockVoucherStatus,
  StockVoucherType,
} from '@/types';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { DataTable, PageHeader } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';
import { Pagination } from '@/components/ui/Pagination';

type Tab = 'warehouse' | 'menu' | 'addons' | 'vouchers' | 'movements' | 'units';

type VariantFormRow = {
  key: string;
  id?: string;
  name: string;
  sellPrice: string;
  isActive: boolean;
  recipeLines: RecipeLineFormRow[];
};

type RecipeLineFormRow = {
  key: string;
  id?: string;
  warehouseItemId: string;
  quantity: string;
};

const movementLabels: Record<number, string> = {
  [InventoryMovementType.Sale]: 'Sale',
  [InventoryMovementType.Return]: 'Return',
  [InventoryMovementType.PurchaseReceive]: 'Purchase',
  [InventoryMovementType.ManualAdjust]: 'Adjust',
  [InventoryMovementType.InitialStock]: 'Initial',
  [InventoryMovementType.StockIn]: 'Stock in',
  [InventoryMovementType.StockCount]: 'Count',
  [InventoryMovementType.Settlement]: 'Settlement',
};

function newRecipeLineRow(partial?: Partial<RecipeLineFormRow>): RecipeLineFormRow {
  return { key: crypto.randomUUID(), warehouseItemId: '', quantity: '1', ...partial };
}

function newVariantRow(partial?: Partial<VariantFormRow>): VariantFormRow {
  return {
    key: crypto.randomUUID(),
    name: '',
    sellPrice: '',
    isActive: true,
    recipeLines: [],
    ...partial,
  };
}

function isStockKind(kind: number) {
  return kind === CafeteriaItemKind.Warehouse || kind === CafeteriaItemKind.SellAsIs;
}

function itemMinVariantPrice(item: CafeteriaItem): number {
  const active = (item.variants ?? []).filter((v) => v.isActive);
  if (active.length === 0) return item.sellPrice;
  return Math.min(...active.map((v) => v.sellPrice));
}

function itemStockValue(item: CafeteriaItem): number {
  return itemMinVariantPrice(item) * item.currentQuantity;
}

function formatUnitName(unit: InventoryUnit, lang: string) {
  return lang === 'ar' && unit.nameAr ? unit.nameAr : unit.name;
}

function resolveUnitId(units: InventoryUnit[], name: string | null | undefined) {
  if (!name) return '';
  return units.find((u) => u.name === name || u.nameAr === name)?.id ?? '';
}

export function InventoryPage() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);
  const [tab, setTab] = useState<Tab>('warehouse');
  const [adjustItem, setAdjustItem] = useState<CafeteriaItem | null>(null);
  const [newQty, setNewQty] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [voucherType, setVoucherType] = useState<number>(StockVoucherType.StockIn);
  const [voucherOpen, setVoucherOpen] = useState(false);
  const [voucherNotes, setVoucherNotes] = useState('');
  const [voucherLines, setVoucherLines] = useState<Record<string, string>>({});
  const [itemFormOpen, setItemFormOpen] = useState(false);
  const [formContext, setFormContext] = useState<'warehouse' | 'menu'>('warehouse');
  const [editingItem, setEditingItem] = useState<CafeteriaItem | null>(null);
  const [itemKind, setItemKind] = useState<number>(CafeteriaItemKind.Warehouse);
  const [itemName, setItemName] = useState('');
  const [itemNameAr, setItemNameAr] = useState('');
  const [itemQty, setItemQty] = useState('0');
  const [itemThreshold, setItemThreshold] = useState('5');
  const [itemIsActive, setItemIsActive] = useState(true);
  const [baseUnitId, setBaseUnitId] = useState('');
  const [largeUnitId, setLargeUnitId] = useState('');
  const [unitsPerLarge, setUnitsPerLarge] = useState('1');
  const [variantRows, setVariantRows] = useState<VariantFormRow[]>([newVariantRow()]);
  const [addonFormOpen, setAddonFormOpen] = useState(false);
  const [editingAddon, setEditingAddon] = useState<CafeteriaAddOn | null>(null);
  const [addonName, setAddonName] = useState('');
  const [addonPrice, setAddonPrice] = useState('');
  const [addonWarehouseId, setAddonWarehouseId] = useState('');
  const [addonDeductQty, setAddonDeductQty] = useState('1');
  const [addonIsActive, setAddonIsActive] = useState(true);
  const [unitFormOpen, setUnitFormOpen] = useState(false);
  const [editingUnit, setEditingUnit] = useState<InventoryUnit | null>(null);
  const [unitName, setUnitName] = useState('');
  const [unitNameAr, setUnitNameAr] = useState('');
  const [unitIsActive, setUnitIsActive] = useState(true);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const canAdjust = hasPermission(user, Permissions.InventoryAdjust);
  const canManageItems = hasPermission(user, Permissions.InventoryManageItems);

  const { data: allItems = [], isLoading: itemsLoading } = useQuery({
    queryKey: ['cafeteria-items', user?.id, activeBranchId],
    queryFn: () => cafeteriaApi.getItems(),
  });

  const { data: units = [] } = useQuery({
    queryKey: ['inventory-units', user?.id, activeBranchId],
    queryFn: () => inventoryApi.getUnits(),
  });

  const { data: allUnits = [], isLoading: unitsLoading } = useQuery({
    queryKey: ['inventory-units', user?.id, activeBranchId, 'all'],
    queryFn: () => inventoryApi.getUnits(false),
    enabled: tab === 'units',
  });

  const warehouseItems = useMemo(
    () => allItems.filter((i) => i.kind === CafeteriaItemKind.Warehouse),
    [allItems]
  );

  const stockItems = useMemo(
    () => allItems.filter((i) => isStockKind(i.kind)),
    [allItems]
  );

  const warehouseTabItems = useMemo(
    () => allItems.filter((i) => i.kind === CafeteriaItemKind.Warehouse || i.kind === CafeteriaItemKind.SellAsIs),
    [allItems]
  );

  const menuTabItems = useMemo(
    () => allItems.filter((i) => i.kind === CafeteriaItemKind.Menu || i.kind === CafeteriaItemKind.SellAsIs),
    [allItems]
  );

  const { data: addOns = [], isLoading: addOnsLoading } = useQuery({
    queryKey: ['cafeteria-addons', user?.id, activeBranchId],
    queryFn: () => cafeteriaApi.getAddOns(),
    enabled: tab === 'addons',
  });

  const { data: movementsPage, isLoading: movLoading } = useQuery({
    queryKey: ['inventory-movements', user?.id, activeBranchId, page, pageSize],
    queryFn: () => inventoryApi.getMovements(undefined, page, pageSize),
    enabled: tab === 'movements',
  });
  const movements = movementsPage?.items ?? [];

  const { data: vouchersPage, isLoading: voucherLoading } = useQuery({
    queryKey: ['stock-vouchers', user?.id, activeBranchId, page, pageSize],
    queryFn: () => inventoryApi.getVouchers(undefined, page, pageSize),
    enabled: tab === 'vouchers',
  });
  const vouchers = vouchersPage?.items ?? [];

  const adjustMutation = useMutation({
    mutationFn: () => inventoryApi.adjust(adjustItem!.id, Number(newQty), reason),
    onSuccess: () => {
      setAdjustItem(null);
      setNewQty('');
      setReason('');
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
      queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
      queryClient.invalidateQueries({ queryKey: ['stock-vouchers'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  function buildVariantsPayload(includeRecipes: boolean) {
    return variantRows
      .filter((row) => row.name.trim())
      .map((row) => ({
        ...(row.id ? { id: row.id } : {}),
        name: row.name.trim(),
        sellPrice: Number(row.sellPrice),
        ...(editingItem ? { isActive: row.isActive } : {}),
        ...(includeRecipes
          ? {
              recipeLines: row.recipeLines
                .filter((rl) => rl.warehouseItemId && rl.quantity !== '')
                .map((rl) => ({
                  ...(rl.id ? { id: rl.id } : {}),
                  warehouseItemId: rl.warehouseItemId,
                  quantity: Number(rl.quantity),
                })),
            }
          : {}),
      }));
  }

  const saveItemMutation = useMutation({
    mutationFn: () => {
      const includeRecipes = formContext === 'menu' && itemKind === CafeteriaItemKind.Menu;
      const needsVariants =
        itemKind === CafeteriaItemKind.SellAsIs ||
        itemKind === CafeteriaItemKind.Menu;
      const variants = needsVariants ? buildVariantsPayload(includeRecipes) : [];

      const unitPayload = formContext === 'warehouse' && isStockKind(itemKind)
        ? {
            baseUnitId: baseUnitId || undefined,
            largeUnitId: largeUnitId || undefined,
            unitsPerLarge: largeUnitId ? Number(unitsPerLarge) || 1 : undefined,
          }
        : {};

      if (editingItem) {
        return cafeteriaApi.updateItem(editingItem.id, {
          name: itemName.trim(),
          kind: itemKind,
          nameAr: itemNameAr.trim() || undefined,
          minThreshold: Number(itemThreshold) || 0,
          isActive: itemIsActive,
          variants,
          ...unitPayload,
        });
      }

      return cafeteriaApi.createItem({
        name: itemName.trim(),
        kind: itemKind,
        nameAr: itemNameAr.trim() || undefined,
        currentQuantity: formContext === 'warehouse' ? Number(itemQty) || 0 : 0,
        minThreshold: Number(itemThreshold) || 0,
        variants: variants.map(({ name, sellPrice, recipeLines }) => ({
          name,
          sellPrice,
          ...(recipeLines ? { recipeLines } : {}),
        })),
        ...unitPayload,
      });
    },
    onSuccess: () => {
      resetItemForm();
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const toggleActiveMutation = useMutation({
    mutationFn: (item: CafeteriaItem) =>
      cafeteriaApi.updateItem(item.id, {
        name: item.name,
        kind: item.kind,
        nameAr: item.nameAr ?? undefined,
        minThreshold: item.minThreshold,
        isActive: !item.isActive,
        variants: (item.variants ?? []).map((v) => ({
          id: v.id,
          name: v.name,
          sellPrice: v.sellPrice,
          isActive: v.isActive,
          recipeLines: (v.recipeLines ?? []).map((rl) => ({
            id: rl.id,
            warehouseItemId: rl.warehouseItemId,
            quantity: rl.quantity,
          })),
        })),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] }),
    onError: (e: Error) => setError(e.message),
  });

  const deleteItemMutation = useMutation({
    mutationFn: (id: string) => cafeteriaApi.deleteItem(id),
    onSuccess: (_data, id) => {
      setError('');
      queryClient.setQueriesData<CafeteriaItem[]>({ queryKey: ['cafeteria-items'] }, (old) =>
        old?.filter((i) => i.id !== id)
      );
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const saveAddonMutation = useMutation({
    mutationFn: () => {
      const payload = {
        name: addonName.trim(),
        sellPrice: Number(addonPrice),
        warehouseItemId: addonWarehouseId,
        deductQuantity: Number(addonDeductQty) || 1,
      };
      if (editingAddon) {
        return cafeteriaApi.updateAddOn(editingAddon.id, { ...payload, isActive: addonIsActive });
      }
      return cafeteriaApi.createAddOn(payload);
    },
    onSuccess: () => {
      resetAddonForm();
      queryClient.invalidateQueries({ queryKey: ['cafeteria-addons'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteAddonMutation = useMutation({
    mutationFn: (id: string) => cafeteriaApi.deleteAddOn(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cafeteria-addons'] }),
    onError: (e: Error) => setError(e.message),
  });

  const saveUnitMutation = useMutation({
    mutationFn: () => {
      if (editingUnit) {
        return inventoryApi.updateUnit(editingUnit.id, {
          name: unitName.trim(),
          nameAr: unitNameAr.trim() || null,
          isActive: unitIsActive,
        });
      }
      return inventoryApi.createUnit({
        name: unitName.trim(),
        nameAr: unitNameAr.trim() || undefined,
      });
    },
    onSuccess: () => {
      resetUnitForm();
      queryClient.invalidateQueries({ queryKey: ['inventory-units'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteUnitMutation = useMutation({
    mutationFn: (id: string) => inventoryApi.deleteUnit(id),
    onSuccess: () => {
      setError('');
      queryClient.invalidateQueries({ queryKey: ['inventory-units'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const createVoucherMutation = useMutation({
    mutationFn: async () => {
      const lines = stockItems
        .map((item) => {
          const raw = voucherLines[item.id];
          if (raw === undefined || raw === '') return null;
          const qty = Number(raw);
          if (Number.isNaN(qty)) return null;
          if (voucherType === StockVoucherType.StockIn && qty <= 0) return null;
          if (voucherType === StockVoucherType.StockCount && qty < 0) return null;
          if (voucherType === StockVoucherType.Settlement) {
            const delta = qty - item.currentQuantity;
            if (delta === 0) return null;
            return { cafeteriaItemId: item.id, quantity: delta };
          }
          if (voucherType === StockVoucherType.StockIn) {
            return { cafeteriaItemId: item.id, quantity: qty, unit: InventoryUnitKind.Base };
          }
          return { cafeteriaItemId: item.id, quantity: qty };
        })
        .filter(Boolean) as { cafeteriaItemId: string; quantity: number; unit?: number }[];

      if (lines.length === 0) throw new Error(t('inventory.voucherNeedLines'));

      const created = await inventoryApi.createVoucher({
        voucherType,
        lines,
        notes: voucherNotes.trim() || undefined,
      });
      return inventoryApi.postVoucher(created.id);
    },
    onSuccess: () => {
      setVoucherOpen(false);
      setVoucherLines({});
      setVoucherNotes('');
      setError('');
      queryClient.invalidateQueries({ queryKey: ['stock-vouchers'] });
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
      queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const postVoucherMutation = useMutation({
    mutationFn: (id: string) => inventoryApi.postVoucher(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stock-vouchers'] });
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
      queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const settleMutation = useMutation({
    mutationFn: async (countId: string) => {
      const draft = await inventoryApi.settlementFromCount(countId);
      return inventoryApi.postVoucher(draft.id);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stock-vouchers'] });
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
      queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  function itemLabel(item: CafeteriaItem) {
    return i18n.language === 'ar' && item.nameAr ? item.nameAr : item.name;
  }

  function kindLabel(kind: number) {
    if (kind === CafeteriaItemKind.Warehouse) return t('inventory.kindWarehouse');
    if (kind === CafeteriaItemKind.Menu) return t('inventory.kindMenu');
    return t('inventory.kindSellAsIs');
  }

  function variantLine(item: CafeteriaItem) {
    return (item.variants ?? [])
      .filter((v) => v.isActive)
      .map((v) => `${v.name} · ${formatCurrency(v.sellPrice)}`)
      .join(' · ');
  }

  function resetItemForm() {
    setItemFormOpen(false);
    setEditingItem(null);
    setItemName('');
    setItemNameAr('');
    setItemQty('0');
    setItemThreshold('5');
    setItemIsActive(true);
    setBaseUnitId('');
    setLargeUnitId('');
    setUnitsPerLarge('1');
    setVariantRows([newVariantRow()]);
    setError('');
  }

  function openCreateItem(ctx: 'warehouse' | 'menu') {
    setFormContext(ctx);
    setEditingItem(null);
    setItemKind(ctx === 'warehouse' ? CafeteriaItemKind.Warehouse : CafeteriaItemKind.Menu);
    setItemName('');
    setItemNameAr('');
    setItemQty('0');
    setItemThreshold('5');
    setItemIsActive(true);
    setBaseUnitId(units[0]?.id ?? '');
    setLargeUnitId('');
    setUnitsPerLarge('1');
    setVariantRows([newVariantRow()]);
    setError('');
    setItemFormOpen(true);
  }

  function openEditItem(item: CafeteriaItem, ctx: 'warehouse' | 'menu') {
    setFormContext(ctx);
    setEditingItem(item);
    setItemKind(item.kind);
    setItemName(item.name);
    setItemNameAr(item.nameAr ?? '');
    setItemThreshold(String(item.minThreshold));
    setItemIsActive(item.isActive);
    setBaseUnitId(resolveUnitId(units, item.baseUnitName));
    setLargeUnitId(resolveUnitId(units, item.largeUnitName));
    setUnitsPerLarge(String(item.unitsPerLarge || 1));
    setVariantRows(
      (item.variants ?? []).length > 0
        ? (item.variants ?? []).map((v) =>
            newVariantRow({
              id: v.id,
              name: v.name,
              sellPrice: String(v.sellPrice),
              isActive: v.isActive,
              recipeLines: (v.recipeLines ?? []).map((rl) =>
                newRecipeLineRow({
                  id: rl.id,
                  warehouseItemId: rl.warehouseItemId,
                  quantity: String(rl.quantity),
                })
              ),
            })
          )
        : [newVariantRow()]
    );
    setError('');
    setItemFormOpen(true);
  }

  function resetAddonForm() {
    setAddonFormOpen(false);
    setEditingAddon(null);
    setAddonName('');
    setAddonPrice('');
    setAddonWarehouseId('');
    setAddonDeductQty('1');
    setAddonIsActive(true);
    setError('');
  }

  function openCreateAddon() {
    setEditingAddon(null);
    setAddonName('');
    setAddonPrice('');
    setAddonWarehouseId(warehouseItems[0]?.id ?? '');
    setAddonDeductQty('1');
    setAddonIsActive(true);
    setError('');
    setAddonFormOpen(true);
  }

  function openEditAddon(addon: CafeteriaAddOn) {
    setEditingAddon(addon);
    setAddonName(addon.name);
    setAddonPrice(String(addon.sellPrice));
    setAddonWarehouseId(addon.warehouseItemId);
    setAddonDeductQty(String(addon.deductQuantity));
    setAddonIsActive(addon.isActive);
    setError('');
    setAddonFormOpen(true);
  }

  function resetUnitForm() {
    setUnitFormOpen(false);
    setEditingUnit(null);
    setUnitName('');
    setUnitNameAr('');
    setUnitIsActive(true);
    setError('');
  }

  function openCreateUnit() {
    setEditingUnit(null);
    setUnitName('');
    setUnitNameAr('');
    setUnitIsActive(true);
    setError('');
    setUnitFormOpen(true);
  }

  function openEditUnit(unit: InventoryUnit) {
    setEditingUnit(unit);
    setUnitName(unit.name);
    setUnitNameAr(unit.nameAr ?? '');
    setUnitIsActive(unit.isActive);
    setError('');
    setUnitFormOpen(true);
  }

  function addVariantRow() {
    setVariantRows((prev) => [...prev, newVariantRow()]);
  }

  function removeVariantRow(key: string) {
    setVariantRows((prev) => (prev.length <= 1 ? prev : prev.filter((r) => r.key !== key)));
  }

  function updateVariantRow(key: string, patch: Partial<VariantFormRow>) {
    setVariantRows((prev) => prev.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  }

  function addRecipeLine(variantKey: string) {
    setVariantRows((prev) =>
      prev.map((r) =>
        r.key === variantKey ? { ...r, recipeLines: [...r.recipeLines, newRecipeLineRow()] } : r
      )
    );
  }

  function updateRecipeLine(variantKey: string, lineKey: string, patch: Partial<RecipeLineFormRow>) {
    setVariantRows((prev) =>
      prev.map((r) =>
        r.key === variantKey
          ? {
              ...r,
              recipeLines: r.recipeLines.map((rl) => (rl.key === lineKey ? { ...rl, ...patch } : rl)),
            }
          : r
      )
    );
  }

  function removeRecipeLine(variantKey: string, lineKey: string) {
    setVariantRows((prev) =>
      prev.map((r) =>
        r.key === variantKey
          ? { ...r, recipeLines: r.recipeLines.filter((rl) => rl.key !== lineKey) }
          : r
      )
    );
  }

  function validVariantRows() {
    return variantRows.filter(
      (row) => row.name.trim() && row.sellPrice !== '' && !Number.isNaN(Number(row.sellPrice))
    );
  }

  function needsVariants() {
    return itemKind === CafeteriaItemKind.SellAsIs || itemKind === CafeteriaItemKind.Menu;
  }

  function canSaveItem() {
    if (!itemName.trim()) return false;
    if (formContext === 'warehouse' && isStockKind(itemKind) && !baseUnitId) return false;
    if (needsVariants() && validVariantRows().length === 0) return false;
    return true;
  }

  function voucherTypeLabel(type: number) {
    if (type === StockVoucherType.StockIn) return t('inventory.stockIn');
    if (type === StockVoucherType.StockCount) return t('inventory.stockCount');
    return t('inventory.settlement');
  }

  const tabs: { id: Tab; label: string }[] = [
    { id: 'warehouse', label: t('inventory.warehouse') },
    { id: 'menu', label: t('inventory.menuProducts') },
    { id: 'addons', label: t('inventory.addons') },
    { id: 'units', label: t('inventory.units') },
    { id: 'vouchers', label: t('inventory.vouchers') },
    { id: 'movements', label: t('inventory.movements') },
  ];

  return (
    <div>
      <PageHeader title={t('inventory.title')}>
        {tab === 'warehouse' && canManageItems && (
          <Button onClick={() => openCreateItem('warehouse')}>
            <Icon name="plus" className="h-4 w-4" />
            {t('inventory.addItem')}
          </Button>
        )}
        {tab === 'menu' && canManageItems && (
          <Button onClick={() => openCreateItem('menu')}>
            <Icon name="plus" className="h-4 w-4" />
            {t('inventory.addMenuProduct')}
          </Button>
        )}
        {tab === 'addons' && canManageItems && (
          <Button onClick={openCreateAddon}>
            <Icon name="plus" className="h-4 w-4" />
            {t('inventory.addAddOn')}
          </Button>
        )}
        {tab === 'units' && canManageItems && (
          <Button onClick={openCreateUnit}>
            <Icon name="plus" className="h-4 w-4" />
            {t('inventory.addUnit')}
          </Button>
        )}
        {tab === 'vouchers' && canAdjust && (
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              onClick={() => {
                setVoucherType(StockVoucherType.StockIn);
                setVoucherOpen(true);
                setError('');
              }}
            >
              {t('inventory.newStockIn')}
            </Button>
            <Button
              size="sm"
              variant="secondary"
              onClick={() => {
                setVoucherType(StockVoucherType.StockCount);
                setVoucherOpen(true);
                setError('');
              }}
            >
              {t('inventory.newStockCount')}
            </Button>
            <Button
              size="sm"
              variant="secondary"
              onClick={() => {
                setVoucherType(StockVoucherType.Settlement);
                setVoucherOpen(true);
                setError('');
              }}
            >
              {t('inventory.newSettlement')}
            </Button>
          </div>
        )}
      </PageHeader>

      <div className="mb-4 text-sm text-muted">
        {tab === 'warehouse' && t('inventory.warehouseHint')}
        {tab === 'menu' && t('inventory.menuHint')}
        {tab === 'addons' && t('inventory.addonsHint')}
      </div>

      <div className="mb-6 flex flex-wrap gap-2">
        {tabs.map(({ id, label }) => (
          <Button
            key={id}
            variant={tab === id ? 'primary' : 'secondary'}
            size="sm"
            onClick={() => {
              setTab(id);
              setPage(1);
            }}
          >
            {label}
          </Button>
        ))}
      </div>

      {error && tab !== 'vouchers' && (
        <div className="mb-4 rounded-xl border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-danger">
          {error}
        </div>
      )}

      {tab === 'warehouse' &&
        (itemsLoading ? (
          <PageLoader />
        ) : (
          <DataTable
            headers={[
              t('inventory.item'),
              t('inventory.kind'),
              t('common.status'),
              t('inventory.qty'),
              t('inventory.threshold'),
              t('inventory.value'),
              '',
            ]}
          >
            {warehouseTabItems.map((item) => (
              <tr key={item.id} className="hover:bg-surface-hover">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    {itemLabel(item)}
                    {item.isLowStock && <Badge status="paused">{t('cafeteria.lowStock')}</Badge>}
                  </div>
                  {item.kind === CafeteriaItemKind.SellAsIs && variantLine(item) && (
                    <p className="text-xs text-muted">{variantLine(item)}</p>
                  )}
                  {item.baseUnitName && (
                    <p className="text-xs text-muted">
                      {item.baseUnitName}
                      {item.largeUnitName ? ` / ${item.largeUnitName}` : ''}
                    </p>
                  )}
                </td>
                <td className="px-4 py-3">
                  <Badge status="idle">{kindLabel(item.kind)}</Badge>
                </td>
                <td className="px-4 py-3">
                  <Badge status={item.isActive ? 'gaming' : 'idle'}>
                    {item.isActive ? t('common.active') : t('common.inactive')}
                  </Badge>
                </td>
                <td className="px-4 py-3">{item.currentQuantity}</td>
                <td className="px-4 py-3">{item.minThreshold}</td>
                <td className="px-4 py-3">{formatCurrency(itemStockValue(item))}</td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1">
                    {canAdjust && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setAdjustItem(item);
                          setNewQty(String(item.currentQuantity));
                          setReason('');
                          setError('');
                        }}
                      >
                        {t('inventory.adjust')}
                      </Button>
                    )}
                    {canManageItems && (
                      <>
                        <Button variant="ghost" size="sm" onClick={() => openEditItem(item, 'warehouse')}>
                          {t('users.edit')}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          loading={toggleActiveMutation.isPending}
                          onClick={() => toggleActiveMutation.mutate(item)}
                        >
                          {item.isActive ? t('inventory.deactivate') : t('inventory.activate')}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          loading={deleteItemMutation.isPending}
                          onClick={() => {
                            if (window.confirm(t('common.confirmDelete'))) {
                              deleteItemMutation.mutate(item.id);
                            }
                          }}
                        >
                          {t('common.delete')}
                        </Button>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </DataTable>
        ))}

      {tab === 'menu' &&
        (itemsLoading ? (
          <PageLoader />
        ) : (
          <DataTable
            headers={[t('inventory.item'), t('inventory.kind'), t('common.status'), t('inventory.variants'), '']}
          >
            {menuTabItems.map((item) => (
              <tr key={item.id} className="hover:bg-surface-hover">
                <td className="px-4 py-3 font-medium">{itemLabel(item)}</td>
                <td className="px-4 py-3">
                  <Badge status="idle">{kindLabel(item.kind)}</Badge>
                </td>
                <td className="px-4 py-3">
                  <Badge status={item.isActive ? 'gaming' : 'idle'}>
                    {item.isActive ? t('common.active') : t('common.inactive')}
                  </Badge>
                </td>
                <td className="px-4 py-3 text-sm text-muted">
                  {(item.variants ?? [])
                    .filter((v) => v.isActive)
                    .map((v) => {
                      const recipes = (v.recipeLines ?? []).length;
                      return `${v.name} · ${formatCurrency(v.sellPrice)}${recipes ? ` (${recipes} ${t('inventory.recipeLines')})` : ''}`;
                    })
                    .join(' · ') || '—'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1">
                    {canManageItems && (
                      <>
                        <Button variant="ghost" size="sm" onClick={() => openEditItem(item, 'menu')}>
                          {t('users.edit')}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          loading={toggleActiveMutation.isPending}
                          onClick={() => toggleActiveMutation.mutate(item)}
                        >
                          {item.isActive ? t('inventory.deactivate') : t('inventory.activate')}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          loading={deleteItemMutation.isPending}
                          onClick={() => {
                            if (window.confirm(t('common.confirmDelete'))) {
                              deleteItemMutation.mutate(item.id);
                            }
                          }}
                        >
                          {t('common.delete')}
                        </Button>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </DataTable>
        ))}

      {tab === 'addons' &&
        (addOnsLoading ? (
          <PageLoader />
        ) : addOns.length === 0 ? (
          <p className="text-muted">{t('inventory.noAddOns')}</p>
        ) : (
          <DataTable
            headers={[
              t('inventory.addOn'),
              t('inventory.sellPrice'),
              t('inventory.warehouseItem'),
              t('inventory.deductQty'),
              t('inventory.qty'),
              t('common.status'),
              '',
            ]}
          >
            {addOns.map((addon) => (
              <tr key={addon.id} className="hover:bg-surface-hover">
                <td className="px-4 py-3 font-medium">{addon.name}</td>
                <td className="px-4 py-3">{formatCurrency(addon.sellPrice)}</td>
                <td className="px-4 py-3 text-sm">{addon.warehouseItemName}</td>
                <td className="px-4 py-3">{addon.deductQuantity}</td>
                <td className="px-4 py-3">{addon.availableQuantity}</td>
                <td className="px-4 py-3">
                  <Badge status={addon.isActive ? 'gaming' : 'idle'}>
                    {addon.isActive ? t('common.active') : t('common.inactive')}
                  </Badge>
                </td>
                <td className="px-4 py-3">
                  {canManageItems && (
                    <div className="flex flex-wrap gap-1">
                      <Button variant="ghost" size="sm" onClick={() => openEditAddon(addon)}>
                        {t('users.edit')}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        loading={deleteAddonMutation.isPending}
                        onClick={() => {
                          if (window.confirm(t('common.confirmDelete'))) {
                            deleteAddonMutation.mutate(addon.id);
                          }
                        }}
                      >
                        {t('common.delete')}
                      </Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </DataTable>
        ))}

      {tab === 'units' &&
        (unitsLoading ? (
          <PageLoader />
        ) : allUnits.length === 0 ? (
          <p className="text-muted">{t('inventory.noUnits')}</p>
        ) : (
          <DataTable
            headers={[
              t('inventory.unitName'),
              t('inventory.unitNameAr'),
              t('common.status'),
              '',
            ]}
          >
            {allUnits.map((unit) => (
              <tr key={unit.id} className="hover:bg-surface-hover">
                <td className="px-4 py-3 font-medium">{unit.name}</td>
                <td className="px-4 py-3 text-muted">{unit.nameAr || '—'}</td>
                <td className="px-4 py-3">
                  <Badge status={unit.isActive ? 'gaming' : 'idle'}>
                    {unit.isActive ? t('common.active') : t('common.inactive')}
                  </Badge>
                </td>
                <td className="px-4 py-3">
                  {canManageItems && (
                    <div className="flex flex-wrap gap-1">
                      <Button variant="ghost" size="sm" onClick={() => openEditUnit(unit)}>
                        {t('users.edit')}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        loading={deleteUnitMutation.isPending}
                        onClick={() => {
                          if (window.confirm(t('inventory.confirmDeleteUnit'))) {
                            deleteUnitMutation.mutate(unit.id);
                          }
                        }}
                      >
                        {t('common.delete')}
                      </Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </DataTable>
        ))}

      {tab === 'vouchers' &&
        (voucherLoading ? (
          <PageLoader />
        ) : vouchers.length === 0 ? (
          <p className="text-muted">{t('inventory.noVouchers')}</p>
        ) : (
          <div className="space-y-4">
            {error && <p className="text-sm text-danger">{error}</p>}
            {vouchers.map((v) => (
              <VoucherCard
                key={v.id}
                voucher={v}
                typeLabel={voucherTypeLabel(v.voucherType)}
                canAdjust={canAdjust}
                onPost={() => postVoucherMutation.mutate(v.id)}
                onSettle={() => settleMutation.mutate(v.id)}
                posting={postVoucherMutation.isPending}
                settling={settleMutation.isPending}
              />
            ))}
            <Pagination
              page={page}
              pageSize={pageSize}
              totalCount={vouchersPage?.totalCount ?? 0}
              onPageChange={setPage}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPage(1);
              }}
            />
          </div>
        ))}

      {tab === 'movements' &&
        (movLoading ? (
          <PageLoader />
        ) : movements.length === 0 ? (
          <p className="text-muted">{t('inventory.noMovements')}</p>
        ) : (
          <>
            <DataTable
              headers={[
                t('inventory.item'),
                t('inventory.type'),
                t('inventory.change'),
                t('inventory.by'),
                t('inventory.date'),
              ]}
            >
              {movements.map((m) => (
                <tr key={m.id} className="hover:bg-surface-hover transition-colors">
                  <td className="px-4 py-3">{m.itemName}</td>
                  <td className="px-4 py-3">{movementLabels[m.movementType] ?? m.movementType}</td>
                  <td
                    className={`px-4 py-3 font-medium ${m.quantityChange >= 0 ? 'text-success' : 'text-danger'}`}
                  >
                    {m.quantityChange >= 0 ? '+' : ''}
                    {m.quantityChange}
                  </td>
                  <td className="px-4 py-3 text-muted">{m.performedByName}</td>
                  <td className="px-4 py-3 text-muted">{new Date(m.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </DataTable>
            <Pagination
              page={page}
              pageSize={pageSize}
              totalCount={movementsPage?.totalCount ?? 0}
              onPageChange={setPage}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPage(1);
              }}
            />
          </>
        ))}

      <Modal open={!!adjustItem} onClose={() => setAdjustItem(null)} title={t('inventory.adjustStock')}>
        {adjustItem && (
          <div className="space-y-4">
            <p className="font-medium">{itemLabel(adjustItem)}</p>
            <Input
              label={t('inventory.newQty')}
              type="number"
              value={newQty}
              onChange={(e) => setNewQty(e.target.value)}
            />
            <Input label={t('inventory.reason')} value={reason} onChange={(e) => setReason(e.target.value)} />
            {error && <p className="text-sm text-danger">{error}</p>}
            <Button
              className="w-full"
              loading={adjustMutation.isPending}
              disabled={!reason.trim()}
              onClick={() => adjustMutation.mutate()}
            >
              {t('common.save')}
            </Button>
          </div>
        )}
      </Modal>

      <Modal
        open={voucherOpen}
        onClose={() => setVoucherOpen(false)}
        title={voucherTypeLabel(voucherType)}
        footer={
          <>
            <Button variant="secondary" onClick={() => setVoucherOpen(false)}>
              {t('session.cancel')}
            </Button>
            <Button loading={createVoucherMutation.isPending} onClick={() => createVoucherMutation.mutate()}>
              {t('inventory.createAndPost')}
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <p className="text-sm text-muted">
            {voucherType === StockVoucherType.StockIn && t('inventory.stockInHint')}
            {voucherType === StockVoucherType.StockCount && t('inventory.stockCountHint')}
            {voucherType === StockVoucherType.Settlement && t('inventory.settlementHint')}
          </p>
          <Input label={t('inventory.notes')} value={voucherNotes} onChange={(e) => setVoucherNotes(e.target.value)} />
          <div className="max-h-72 space-y-2 overflow-y-auto">
            {stockItems
              .filter((i) => i.isActive)
              .map((item) => (
                <div
                  key={item.id}
                  className="flex items-center justify-between gap-3 rounded-lg border border-border px-3 py-2"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{itemLabel(item)}</p>
                    <p className="text-xs text-muted">
                      {t('inventory.systemQty')}: {item.currentQuantity}
                    </p>
                  </div>
                  <Input
                    type="number"
                    className="w-24 shrink-0"
                    placeholder={
                      voucherType === StockVoucherType.Settlement ? String(item.currentQuantity) : '0'
                    }
                    value={voucherLines[item.id] ?? ''}
                    onChange={(e) => setVoucherLines((prev) => ({ ...prev, [item.id]: e.target.value }))}
                  />
                </div>
              ))}
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      </Modal>

      <Modal
        open={itemFormOpen}
        onClose={resetItemForm}
        title={
          editingItem
            ? t('users.edit')
            : formContext === 'warehouse'
              ? t('inventory.addItem')
              : t('inventory.addMenuProduct')
        }
      >
        <div className="max-h-[70vh] space-y-3 overflow-y-auto pe-1">
          <div>
            <label className="mb-1 block text-sm text-muted">{t('inventory.kind')}</label>
            <select
              className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
              value={itemKind}
              onChange={(e) => {
                const k = Number(e.target.value);
                setItemKind(k);
                if (k === CafeteriaItemKind.Warehouse) {
                  setVariantRows([]);
                } else if (variantRows.length === 0) {
                  setVariantRows([newVariantRow()]);
                }
              }}
              disabled={!!editingItem}
            >
              {formContext === 'warehouse' ? (
                <>
                  <option value={CafeteriaItemKind.Warehouse}>{t('inventory.kindWarehouse')}</option>
                  <option value={CafeteriaItemKind.SellAsIs}>{t('inventory.kindSellAsIs')}</option>
                </>
              ) : (
                <>
                  <option value={CafeteriaItemKind.Menu}>{t('inventory.kindMenu')}</option>
                  <option value={CafeteriaItemKind.SellAsIs}>{t('inventory.kindSellAsIs')}</option>
                </>
              )}
            </select>
          </div>

          <Input
            label={t('inventory.itemNameAr')}
            value={itemNameAr}
            onChange={(e) => setItemNameAr(e.target.value)}
          />
          <Input label={t('inventory.itemName')} value={itemName} onChange={(e) => setItemName(e.target.value)} />

          {formContext === 'warehouse' && isStockKind(itemKind) && (
            <>
              <div>
                <label className="mb-1 block text-sm text-muted">{t('inventory.baseUnit')}</label>
                <select
                  className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                  value={baseUnitId}
                  onChange={(e) => setBaseUnitId(e.target.value)}
                >
                  <option value="">{t('inventory.selectBaseUnit')}</option>
                  {units.map((u) => (
                    <option key={u.id} value={u.id}>
                      {formatUnitName(u, i18n.language)}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-sm text-muted">{t('inventory.largeUnit')}</label>
                <select
                  className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                  value={largeUnitId}
                  onChange={(e) => setLargeUnitId(e.target.value)}
                >
                  <option value="">{t('inventory.none')}</option>
                  {units
                    .filter((u) => u.id !== baseUnitId)
                    .map((u) => (
                      <option key={u.id} value={u.id}>
                        {formatUnitName(u, i18n.language)}
                      </option>
                    ))}
                </select>
              </div>
              {largeUnitId && (
                <Input
                  label={t('inventory.unitsPerLarge')}
                  type="number"
                  min={2}
                  value={unitsPerLarge}
                  onChange={(e) => setUnitsPerLarge(e.target.value)}
                />
              )}
              <Input
                label={t('inventory.threshold')}
                type="number"
                value={itemThreshold}
                onChange={(e) => setItemThreshold(e.target.value)}
              />
              {editingItem ? (
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={itemIsActive}
                    onChange={(e) => setItemIsActive(e.target.checked)}
                  />
                  {t('common.active')}
                </label>
              ) : (
                <Input
                  label={t('inventory.initialStock')}
                  type="number"
                  value={itemQty}
                  onChange={(e) => setItemQty(e.target.value)}
                />
              )}
            </>
          )}

          {formContext === 'menu' && (
            <>
              <Input
                label={t('inventory.threshold')}
                type="number"
                value={itemThreshold}
                onChange={(e) => setItemThreshold(e.target.value)}
              />
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={itemIsActive}
                  onChange={(e) => setItemIsActive(e.target.checked)}
                />
                {t('common.active')}
              </label>
              {itemKind === CafeteriaItemKind.SellAsIs && (
                <p className="text-xs text-muted">{t('inventory.sellAsIsMenuHint')}</p>
              )}
            </>
          )}

          {needsVariants() && (
            <div className="space-y-2">
              <div className="flex items-center justify-between gap-2">
                <p className="text-sm font-medium text-text">{t('inventory.variants')}</p>
                <Button type="button" size="sm" variant="secondary" onClick={addVariantRow}>
                  <Icon name="plus" className="h-3 w-3" />
                  {t('inventory.addVariant')}
                </Button>
              </div>
              {variantRows.map((row, index) => (
                <div key={row.key} className="space-y-2 rounded-lg border border-border p-3">
                  <div className="flex flex-wrap items-end gap-2">
                    <div className="min-w-[8rem] flex-1">
                      <Input
                        label={
                          index === 0
                            ? t('inventory.variant')
                            : `${t('inventory.variant')} ${index + 1}`
                        }
                        value={row.name}
                        onChange={(e) => updateVariantRow(row.key, { name: e.target.value })}
                      />
                    </div>
                    <div className="w-28">
                      <Input
                        label={t('inventory.sellPrice')}
                        type="number"
                        value={row.sellPrice}
                        onChange={(e) => updateVariantRow(row.key, { sellPrice: e.target.value })}
                      />
                    </div>
                    {editingItem && (
                      <label className="flex items-center gap-2 pb-2 text-sm">
                        <input
                          type="checkbox"
                          checked={row.isActive}
                          onChange={(e) => updateVariantRow(row.key, { isActive: e.target.checked })}
                        />
                        {t('common.active')}
                      </label>
                    )}
                    {variantRows.length > 1 && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="mb-1"
                        onClick={() => removeVariantRow(row.key)}
                      >
                        {t('common.delete')}
                      </Button>
                    )}
                  </div>

                  {formContext === 'menu' && itemKind === CafeteriaItemKind.Menu && (
                    <div className="space-y-2 border-t border-border pt-2">
                      <div className="flex items-center justify-between">
                        <p className="text-xs font-medium text-muted">{t('inventory.recipe')}</p>
                        <Button
                          type="button"
                          size="sm"
                          variant="ghost"
                          onClick={() => addRecipeLine(row.key)}
                        >
                          <Icon name="plus" className="h-3 w-3" />
                          {t('inventory.addRecipeLine')}
                        </Button>
                      </div>
                      {row.recipeLines.map((rl) => (
                        <div key={rl.key} className="flex flex-wrap items-end gap-2">
                          <div className="min-w-[10rem] flex-1">
                            <label className="mb-1 block text-xs text-muted">
                              {t('inventory.warehouseItem')}
                            </label>
                            <select
                              className="w-full rounded-lg border border-border bg-surface-elevated px-2 py-1.5 text-sm"
                              value={rl.warehouseItemId}
                              onChange={(e) =>
                                updateRecipeLine(row.key, rl.key, { warehouseItemId: e.target.value })
                              }
                            >
                              <option value="">{t('inventory.selectWarehouseItem')}</option>
                              {warehouseItems
                                .filter((w) => w.isActive)
                                .map((w) => (
                                  <option key={w.id} value={w.id}>
                                    {itemLabel(w)} ({w.currentQuantity})
                                  </option>
                                ))}
                            </select>
                          </div>
                          <div className="w-24">
                            <Input
                              label={t('inventory.qty')}
                              type="number"
                              min={0}
                              step="0.01"
                              value={rl.quantity}
                              onChange={(e) =>
                                updateRecipeLine(row.key, rl.key, { quantity: e.target.value })
                              }
                            />
                          </div>
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            onClick={() => removeRecipeLine(row.key, rl.key)}
                          >
                            {t('common.delete')}
                          </Button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={saveItemMutation.isPending}
            disabled={!canSaveItem()}
            onClick={() => saveItemMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <Modal
        open={addonFormOpen}
        onClose={resetAddonForm}
        title={editingAddon ? t('users.edit') : t('inventory.addAddOn')}
      >
        <div className="space-y-3">
          <Input label={t('inventory.addOn')} value={addonName} onChange={(e) => setAddonName(e.target.value)} />
          <Input
            label={t('inventory.sellPrice')}
            type="number"
            value={addonPrice}
            onChange={(e) => setAddonPrice(e.target.value)}
          />
          <div>
            <label className="mb-1 block text-sm text-muted">{t('inventory.warehouseItem')}</label>
            <select
              className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
              value={addonWarehouseId}
              onChange={(e) => setAddonWarehouseId(e.target.value)}
            >
              <option value="">{t('inventory.selectWarehouseItem')}</option>
              {warehouseItems
                .filter((w) => w.isActive)
                .map((w) => (
                  <option key={w.id} value={w.id}>
                    {itemLabel(w)} ({w.currentQuantity})
                  </option>
                ))}
            </select>
          </div>
          <Input
            label={t('inventory.deductQty')}
            type="number"
            min={0}
            step="0.01"
            value={addonDeductQty}
            onChange={(e) => setAddonDeductQty(e.target.value)}
          />
          {editingAddon && (
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={addonIsActive}
                onChange={(e) => setAddonIsActive(e.target.checked)}
              />
              {t('common.active')}
            </label>
          )}
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={saveAddonMutation.isPending}
            disabled={!addonName.trim() || !addonWarehouseId || addonPrice === ''}
            onClick={() => saveAddonMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <Modal
        open={unitFormOpen}
        onClose={resetUnitForm}
        title={editingUnit ? t('inventory.editUnit') : t('inventory.addUnit')}
      >
        <div className="space-y-3">
          <Input
            label={t('inventory.unitName')}
            value={unitName}
            onChange={(e) => setUnitName(e.target.value)}
          />
          <Input
            label={t('inventory.unitNameAr')}
            value={unitNameAr}
            onChange={(e) => setUnitNameAr(e.target.value)}
          />
          {editingUnit && (
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={unitIsActive}
                onChange={(e) => setUnitIsActive(e.target.checked)}
              />
              {t('common.active')}
            </label>
          )}
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={saveUnitMutation.isPending}
            disabled={!unitName.trim()}
            onClick={() => saveUnitMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>
    </div>
  );
}

function VoucherCard({
  voucher,
  typeLabel,
  canAdjust,
  onPost,
  onSettle,
  posting,
  settling,
}: {
  voucher: StockVoucher;
  typeLabel: string;
  canAdjust: boolean;
  onPost: () => void;
  onSettle: () => void;
  posting: boolean;
  settling: boolean;
}) {
  const { t } = useTranslation();
  const status =
    voucher.status === StockVoucherStatus.Posted
      ? 'watching'
      : voucher.status === StockVoucherStatus.Cancelled
        ? 'paused'
        : 'idle';

  return (
    <div className="rounded-xl border border-border bg-surface-elevated p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="font-medium">
            {voucher.voucherNumber} · {typeLabel}
          </p>
          <p className="text-sm text-muted">
            {voucher.createdByName} · {new Date(voucher.createdAt).toLocaleString()}
          </p>
          {voucher.notes && <p className="mt-1 text-sm text-muted">{voucher.notes}</p>}
        </div>
        <Badge status={status}>
          {t(`inventory.voucherStatus.${voucher.status}`, { defaultValue: String(voucher.status) })}
        </Badge>
      </div>
      <ul className="mb-3 space-y-1 text-sm text-muted">
        {voucher.lines.map((l) => (
          <li key={l.id}>
            {l.itemName}: {l.quantity}
            {l.systemQuantity != null && ` · ${t('inventory.systemQty')} ${l.systemQuantity}`}
            {l.variance != null &&
              l.variance !== 0 &&
              ` · ${t('inventory.variance')} ${l.variance > 0 ? '+' : ''}${l.variance}`}
          </li>
        ))}
      </ul>
      <div className="flex flex-wrap gap-2">
        {voucher.status === StockVoucherStatus.Draft && canAdjust && (
          <Button size="sm" loading={posting} onClick={onPost}>
            {t('inventory.postVoucher')}
          </Button>
        )}
        {voucher.status === StockVoucherStatus.Posted &&
          voucher.voucherType === StockVoucherType.StockCount &&
          canAdjust && (
            <Button size="sm" variant="secondary" loading={settling} onClick={onSettle}>
              {t('inventory.settleFromCount')}
            </Button>
          )}
      </div>
    </div>
  );
}
