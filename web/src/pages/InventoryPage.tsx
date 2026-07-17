import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cafeteriaApi, inventoryApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import type { CafeteriaItem, StockVoucher } from '@/types';
import {
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

type Tab = 'stock' | 'vouchers' | 'movements';

type VariantFormRow = {
  key: string;
  id?: string;
  name: string;
  sellPrice: string;
  isActive: boolean;
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

function newVariantRow(partial?: Partial<VariantFormRow>): VariantFormRow {
  return {
    key: crypto.randomUUID(),
    name: '',
    sellPrice: '',
    isActive: true,
    ...partial,
  };
}

function itemMinVariantPrice(item: CafeteriaItem): number {
  const active = (item.variants ?? []).filter((v) => v.isActive);
  if (active.length === 0) return item.sellPrice;
  return Math.min(...active.map((v) => v.sellPrice));
}

function itemStockValue(item: CafeteriaItem): number {
  return itemMinVariantPrice(item) * item.currentQuantity;
}

export function InventoryPage() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);
  const [tab, setTab] = useState<Tab>('stock');
  const [adjustItem, setAdjustItem] = useState<CafeteriaItem | null>(null);
  const [newQty, setNewQty] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [voucherType, setVoucherType] = useState<number>(StockVoucherType.StockIn);
  const [voucherOpen, setVoucherOpen] = useState(false);
  const [voucherNotes, setVoucherNotes] = useState('');
  const [voucherLines, setVoucherLines] = useState<Record<string, string>>({});
  const [addItemOpen, setAddItemOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<CafeteriaItem | null>(null);
  const [itemName, setItemName] = useState('');
  const [itemNameAr, setItemNameAr] = useState('');
  const [itemQty, setItemQty] = useState('0');
  const [itemThreshold, setItemThreshold] = useState('5');
  const [itemIsActive, setItemIsActive] = useState(true);
  const [variantRows, setVariantRows] = useState<VariantFormRow[]>([newVariantRow()]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const canAdjust = hasPermission(user, Permissions.InventoryAdjust);
  const canManageItems = hasPermission(user, Permissions.InventoryManageItems);

  const { data: items = [], isLoading: itemsLoading } = useQuery({
    queryKey: ['cafeteria-items', user?.id, activeBranchId],
    queryFn: cafeteriaApi.getItems,
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

  const createItemMutation = useMutation({
    mutationFn: () => {
      const variants = variantRows
        .filter((row) => row.name.trim())
        .map((row) => ({
          ...(row.id ? { id: row.id } : {}),
          name: row.name.trim(),
          sellPrice: Number(row.sellPrice),
          ...(editingItem ? { isActive: row.isActive } : {}),
        }));

      if (editingItem) {
        return cafeteriaApi.updateItem(editingItem.id, {
          name: itemName.trim(),
          nameAr: itemNameAr.trim() || undefined,
          minThreshold: Number(itemThreshold) || 0,
          isActive: itemIsActive,
          variants,
        });
      }

      return cafeteriaApi.createItem({
        name: itemName.trim(),
        nameAr: itemNameAr.trim() || undefined,
        currentQuantity: Number(itemQty) || 0,
        minThreshold: Number(itemThreshold) || 0,
        variants: variants.map(({ name, sellPrice }) => ({ name, sellPrice })),
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
        nameAr: item.nameAr ?? undefined,
        minThreshold: item.minThreshold,
        isActive: !item.isActive,
        variants: (item.variants ?? []).map((v) => ({
          id: v.id,
          name: v.name,
          sellPrice: v.sellPrice,
          isActive: v.isActive,
        })),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] }),
    onError: (e: Error) => setError(e.message),
  });

  const deleteItemMutation = useMutation({
    mutationFn: (id: string) => cafeteriaApi.deleteItem(id),
    onSuccess: (_data, id) => {
      setError('');
      queryClient.setQueriesData<import('@/types').CafeteriaItem[]>(
        { queryKey: ['cafeteria-items'] },
        (old) => old?.filter((i) => i.id !== id)
      );
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const createVoucherMutation = useMutation({
    mutationFn: async () => {
      const lines = items
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
            return {
              cafeteriaItemId: item.id,
              quantity: qty,
              unit: InventoryUnitKind.Base,
            };
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

  function variantLine(item: CafeteriaItem) {
    return (item.variants ?? [])
      .filter((v) => v.isActive)
      .map((v) => `${v.name} · ${formatCurrency(v.sellPrice)}`)
      .join(' · ');
  }

  function resetItemForm() {
    setAddItemOpen(false);
    setEditingItem(null);
    setItemName('');
    setItemNameAr('');
    setItemQty('0');
    setItemThreshold('5');
    setItemIsActive(true);
    setVariantRows([newVariantRow()]);
    setError('');
  }

  function openCreateItem() {
    setError('');
    setEditingItem(null);
    setItemName('');
    setItemNameAr('');
    setItemQty('0');
    setItemThreshold('5');
    setItemIsActive(true);
    setVariantRows([newVariantRow()]);
    setAddItemOpen(true);
  }

  function openEditItem(item: CafeteriaItem) {
    setEditingItem(item);
    setItemName(item.name);
    setItemNameAr(item.nameAr ?? '');
    setItemThreshold(String(item.minThreshold));
    setItemIsActive(item.isActive);
    setVariantRows(
      (item.variants ?? []).length > 0
        ? (item.variants ?? []).map((v) =>
            newVariantRow({
              id: v.id,
              name: v.name,
              sellPrice: String(v.sellPrice),
              isActive: v.isActive,
            })
          )
        : [newVariantRow()]
    );
    setError('');
    setAddItemOpen(true);
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

  function validVariantRows() {
    return variantRows.filter((row) => row.name.trim() && row.sellPrice !== '' && !Number.isNaN(Number(row.sellPrice)));
  }

  function voucherTypeLabel(type: number) {
    if (type === StockVoucherType.StockIn) return t('inventory.stockIn');
    if (type === StockVoucherType.StockCount) return t('inventory.stockCount');
    return t('inventory.settlement');
  }

  const tabs: { id: Tab; label: string }[] = [
    { id: 'stock', label: t('inventory.stock') },
    { id: 'vouchers', label: t('inventory.vouchers') },
    { id: 'movements', label: t('inventory.movements') },
  ];

  return (
    <div>
      <PageHeader title={t('inventory.title')}>
        {tab === 'stock' && canManageItems && (
          <Button onClick={openCreateItem}>
            <Icon name="plus" className="h-4 w-4" />
            {t('inventory.addItem')}
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

      {error && (
        <div className="mb-4 rounded-xl border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-danger">
          {error}
        </div>
      )}

      {tab === 'stock' &&
        (itemsLoading ? (
          <PageLoader />
        ) : (
          <DataTable
            headers={[
              t('inventory.item'),
              t('common.status'),
              t('inventory.qty'),
              t('inventory.threshold'),
              t('inventory.value'),
              '',
            ]}
          >
            {items.map((item) => (
              <tr key={item.id} className="hover:bg-surface-hover">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    {itemLabel(item)}
                    {item.isLowStock && <Badge status="paused">{t('cafeteria.lowStock')}</Badge>}
                  </div>
                  {variantLine(item) && (
                    <p className="text-xs text-muted">{variantLine(item)}</p>
                  )}
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
                        <Button variant="ghost" size="sm" onClick={() => openEditItem(item)}>
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
            {items
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
        open={addItemOpen}
        onClose={resetItemForm}
        title={editingItem ? t('users.edit') : t('inventory.addItem')}
      >
        <div className="space-y-3">
          <div className="space-y-2">
            <p className="text-sm font-medium text-text">{t('inventory.item')}</p>
            <Input
              label={t('inventory.itemNameAr')}
              value={itemNameAr}
              onChange={(e) => setItemNameAr(e.target.value)}
            />
            <Input label={t('inventory.itemName')} value={itemName} onChange={(e) => setItemName(e.target.value)} />
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between gap-2">
              <p className="text-sm font-medium text-text">
                {t('inventory.variants', { defaultValue: 'Variants' })}
              </p>
              <Button type="button" size="sm" variant="secondary" onClick={addVariantRow}>
                <Icon name="plus" className="h-3 w-3" />
                {t('inventory.addVariant', { defaultValue: 'Add variant' })}
              </Button>
            </div>
            {variantRows.map((row, index) => (
              <div key={row.key} className="flex flex-wrap items-end gap-2 rounded-lg border border-border p-3">
                <div className="min-w-[8rem] flex-1">
                  <Input
                    label={
                      index === 0
                        ? t('inventory.variant', { defaultValue: 'Variant' })
                        : `${t('inventory.variant', { defaultValue: 'Variant' })} ${index + 1}`
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
            ))}
          </div>

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
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={createItemMutation.isPending}
            disabled={!itemName.trim() || validVariantRows().length === 0}
            onClick={() => createItemMutation.mutate()}
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
