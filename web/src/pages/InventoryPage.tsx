import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cafeteriaApi, inventoryApi, purchaseOrdersApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { formatStockDisplay, hasLargeUnit } from '@/lib/itemUnits';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import type { CafeteriaItem, InventoryUnit, PurchaseOrder, StockVoucher } from '@/types';
import {
  InventoryMovementType,
  InventoryUnitKind,
  PurchaseOrderStatus,
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

type Tab = 'stock' | 'vouchers' | 'movements' | 'orders' | 'units';

const selectClass = 'w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm';

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

const poStatusKey: Record<number, 'idle' | 'gaming' | 'watching' | 'paused'> = {
  [PurchaseOrderStatus.Draft]: 'idle',
  [PurchaseOrderStatus.Ordered]: 'gaming',
  [PurchaseOrderStatus.Received]: 'watching',
  [PurchaseOrderStatus.Cancelled]: 'paused',
};

export function InventoryPage() {
  const { t, i18n } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>('stock');
  const [adjustItem, setAdjustItem] = useState<CafeteriaItem | null>(null);
  const [newQty, setNewQty] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [voucherType, setVoucherType] = useState<number>(StockVoucherType.StockIn);
  const [voucherOpen, setVoucherOpen] = useState(false);
  const [voucherNotes, setVoucherNotes] = useState('');
  const [voucherLines, setVoucherLines] = useState<Record<string, string>>({});
  const [voucherLineUnits, setVoucherLineUnits] = useState<Record<string, number>>({});
  const [addItemOpen, setAddItemOpen] = useState(false);
  const [itemName, setItemName] = useState('');
  const [itemNameAr, setItemNameAr] = useState('');
  const [itemPrice, setItemPrice] = useState('');
  const [itemQty, setItemQty] = useState('0');
  const [itemThreshold, setItemThreshold] = useState('5');
  const [baseUnitId, setBaseUnitId] = useState('');
  const [largeUnitId, setLargeUnitId] = useState('');
  const [unitsPerLarge, setUnitsPerLarge] = useState('24');
  const [initialStockUnit, setInitialStockUnit] = useState<InventoryUnitKind>(InventoryUnitKind.Base);
  const [newUnitName, setNewUnitName] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const canAdjust = hasPermission(user, Permissions.InventoryAdjust);
  const canManageItems = hasPermission(user, Permissions.InventoryManageItems);
  const canCreatePo = hasPermission(user, Permissions.PurchaseOrdersCreate);
  const canReceivePo = hasPermission(user, Permissions.PurchaseOrdersReceive);

  const { data: items = [], isLoading: itemsLoading } = useQuery({
    queryKey: ['cafeteria-items'],
    queryFn: cafeteriaApi.getItems,
  });

  const needsUnits =
    tab === 'units' || addItemOpen || voucherOpen || canManageItems;

  const { data: units = [], isLoading: unitsLoading } = useQuery({
    queryKey: ['inventory-units'],
    queryFn: () => inventoryApi.getUnits(false),
    enabled: needsUnits,
  });

  const { data: conversionLogs = [], isLoading: logsLoading } = useQuery({
    queryKey: ['inventory-conversion-logs'],
    queryFn: () => inventoryApi.getConversionLogs(),
    enabled: tab === 'units' && canManageItems,
  });

  const { data: movementsPage, isLoading: movLoading } = useQuery({
    queryKey: ['inventory-movements', page, pageSize],
    queryFn: () => inventoryApi.getMovements(undefined, page, pageSize),
    enabled: tab === 'movements',
  });
  const movements = movementsPage?.items ?? [];

  const { data: ordersPage, isLoading: poLoading } = useQuery({
    queryKey: ['purchase-orders', page, pageSize],
    queryFn: () => purchaseOrdersApi.getAll(page, pageSize),
    enabled: tab === 'orders',
  });
  const orders = ordersPage?.items ?? [];

  const { data: vouchersPage, isLoading: voucherLoading } = useQuery({
    queryKey: ['stock-vouchers', page, pageSize],
    queryFn: () => inventoryApi.getVouchers(undefined, page, pageSize),
    enabled: tab === 'vouchers',
  });
  const vouchers = vouchersPage?.items ?? [];

  const activeUnits = units.filter((u) => u.isActive);

  function catalogUnitLabel(u: InventoryUnit) {
    return i18n.language === 'ar' && u.nameAr ? u.nameAr : u.name;
  }

  const selectedBaseUnit = units.find((u) => u.id === baseUnitId);
  const selectedLargeUnit = largeUnitId ? units.find((u) => u.id === largeUnitId) : undefined;

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
      const hasLarge = !!largeUnitId;
      return cafeteriaApi.createItem({
        name: itemName.trim(),
        nameAr: itemNameAr.trim() || undefined,
        sellPrice: Number(itemPrice),
        currentQuantity: Number(itemQty) || 0,
        minThreshold: Number(itemThreshold) || 0,
        baseUnitId,
        largeUnitId: largeUnitId || null,
        ...(hasLarge ? { unitsPerLarge: Number(unitsPerLarge) || 1 } : {}),
        initialStockUnit: hasLarge ? initialStockUnit : InventoryUnitKind.Base,
      });
    },
    onSuccess: () => {
      setAddItemOpen(false);
      setItemName('');
      setItemNameAr('');
      setItemPrice('');
      setItemQty('0');
      setItemThreshold('5');
      setBaseUnitId('');
      setLargeUnitId('');
      setUnitsPerLarge('24');
      setInitialStockUnit(InventoryUnitKind.Base);
      setError('');
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const toggleActiveMutation = useMutation({
    mutationFn: (item: CafeteriaItem) => {
      const base = units.find((u) => u.name.trim() === item.baseUnitName.trim());
      if (!base) {
        throw new Error(
          t('inventory.baseUnitMissing', {
            defaultValue: `Unit "${item.baseUnitName}" not found. Create it in the Units tab first.`,
            name: item.baseUnitName,
          })
        );
      }
      const large = item.largeUnitName
        ? units.find((u) => u.name.trim() === item.largeUnitName!.trim())
        : undefined;
      return cafeteriaApi.updateItem(item.id, {
        name: item.name,
        nameAr: item.nameAr ?? undefined,
        sellPrice: item.sellPrice,
        minThreshold: item.minThreshold,
        isActive: !item.isActive,
        baseUnitId: base.id,
        largeUnitId: large?.id ?? null,
        unitsPerLarge: item.unitsPerLarge,
      });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] }),
    onError: (e: Error) => setError(e.message),
  });

  const deleteItemMutation = useMutation({
    mutationFn: (id: string) => cafeteriaApi.deleteItem(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] }),
    onError: (e: Error) => setError(e.message),
  });

  const createUnitMutation = useMutation({
    mutationFn: () => inventoryApi.createUnit({ name: newUnitName.trim() }),
    onSuccess: () => {
      setNewUnitName('');
      setError('');
      queryClient.invalidateQueries({ queryKey: ['inventory-units'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const toggleUnitMutation = useMutation({
    mutationFn: (unit: InventoryUnit) =>
      inventoryApi.updateUnit(unit.id, {
        name: unit.name,
        nameAr: unit.nameAr,
        isActive: !unit.isActive,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-units'] }),
    onError: (e: Error) => setError(e.message),
  });

  const deleteUnitMutation = useMutation({
    mutationFn: (id: string) => inventoryApi.deleteUnit(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-units'] }),
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
            const unit = voucherLineUnits[item.id] ?? InventoryUnitKind.Base;
            return { cafeteriaItemId: item.id, quantity: qty, unit };
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
      setVoucherLineUnits({});
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

  const orderMutation = useMutation({
    mutationFn: (id: string) => purchaseOrdersApi.markOrdered(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-orders'] }),
  });

  const receiveMutation = useMutation({
    mutationFn: (id: string) => purchaseOrdersApi.receive(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['purchase-orders'] });
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
    },
  });

  function itemLabel(item: CafeteriaItem) {
    return i18n.language === 'ar' && item.nameAr ? item.nameAr : item.name;
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
    { id: 'orders', label: t('inventory.purchaseOrders') },
    ...(canManageItems
      ? [{ id: 'units' as const, label: t('inventory.units', { defaultValue: 'Units' }) }]
      : []),
  ];

  return (
    <div>
      <PageHeader title={t('inventory.title')}>
        {tab === 'stock' && canManageItems && (
          <Button onClick={() => { setError(''); setAddItemOpen(true); }}>
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
            onClick={() => { setTab(id); setPage(1); }}
          >
            {label}
          </Button>
        ))}
      </div>

      {tab === 'stock' && (
        itemsLoading ? (
          <PageLoader />
        ) : (
          <DataTable headers={[t('inventory.item'), t('common.status'), t('inventory.qty'), t('inventory.threshold'), t('inventory.value'), '']}>
            {items.map((item) => (
              <tr key={item.id} className="hover:bg-surface-hover">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    {itemLabel(item)}
                    {item.isLowStock && <Badge status="paused">{t('cafeteria.lowStock')}</Badge>}
                  </div>
                  <p className="text-xs text-muted">
                    {formatCurrency(item.sellPrice)} / {item.baseUnitName}
                  </p>
                </td>
                <td className="px-4 py-3">
                  <Badge status={item.isActive ? 'gaming' : 'idle'}>
                    {item.isActive ? t('common.active') : t('common.inactive')}
                  </Badge>
                </td>
                <td className="px-4 py-3">
                  <div>{formatStockDisplay(item)}</div>
                  {hasLargeUnit(item) && (
                    <p className="text-xs text-muted">
                      1 {item.largeUnitName} = {item.unitsPerLarge} {item.baseUnitName}
                    </p>
                  )}
                </td>
                <td className="px-4 py-3">{item.minThreshold}</td>
                <td className="px-4 py-3">{formatCurrency(item.sellPrice * item.currentQuantity)}</td>
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
        )
      )}

      {tab === 'vouchers' && (
        voucherLoading ? (
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
              onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
            />
          </div>
        )
      )}

      {tab === 'movements' && (
        movLoading ? (
          <PageLoader />
        ) : movements.length === 0 ? (
          <p className="text-muted">{t('inventory.noMovements')}</p>
        ) : (
          <>
            <DataTable headers={[t('inventory.item'), t('inventory.type'), t('inventory.change'), t('inventory.by'), t('inventory.date')]}>
              {movements.map((m) => (
                <tr key={m.id} className="hover:bg-surface-hover transition-colors">
                  <td className="px-4 py-3">{m.itemName}</td>
                  <td className="px-4 py-3">{movementLabels[m.movementType] ?? m.movementType}</td>
                  <td className={`px-4 py-3 font-medium ${m.quantityChange >= 0 ? 'text-success' : 'text-danger'}`}>
                    {m.quantityChange >= 0 ? '+' : ''}{m.quantityChange}
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
              onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
            />
          </>
        )
      )}

      {tab === 'orders' && (
        poLoading ? (
          <PageLoader />
        ) : orders.length === 0 ? (
          <p className="text-muted">{t('inventory.noOrders')}</p>
        ) : (
          <div className="space-y-4">
            {orders.map((po) => (
              <PoCard
                key={po.id}
                order={po}
                canOrder={canCreatePo}
                canReceive={canReceivePo}
                onOrder={() => orderMutation.mutate(po.id)}
                onReceive={() => receiveMutation.mutate(po.id)}
                ordering={orderMutation.isPending}
                receiving={receiveMutation.isPending}
              />
            ))}
            <Pagination
              page={page}
              pageSize={pageSize}
              totalCount={ordersPage?.totalCount ?? 0}
              onPageChange={setPage}
              onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
            />
          </div>
        )
      )}

      {tab === 'units' && canManageItems && (
        unitsLoading ? (
          <PageLoader />
        ) : (
          <div className="space-y-6">
            {error && <p className="text-sm text-danger">{error}</p>}
            <div className="flex flex-wrap items-end gap-2">
              <div className="min-w-[12rem] flex-1">
                <Input
                  label={t('inventory.unitName', { defaultValue: 'Unit name' })}
                  value={newUnitName}
                  onChange={(e) => setNewUnitName(e.target.value)}
                />
              </div>
              <Button
                loading={createUnitMutation.isPending}
                disabled={!newUnitName.trim()}
                onClick={() => createUnitMutation.mutate()}
              >
                <Icon name="plus" className="h-4 w-4" />
                {t('inventory.addUnit', { defaultValue: 'Add unit' })}
              </Button>
            </div>

            {units.length === 0 ? (
              <p className="text-muted">{t('inventory.noUnits', { defaultValue: 'No units yet' })}</p>
            ) : (
              <DataTable headers={[t('inventory.unitName', { defaultValue: 'Unit name' }), t('common.status'), '']}>
                {units.map((unit) => (
                  <tr key={unit.id} className="hover:bg-surface-hover">
                    <td className="px-4 py-3">{catalogUnitLabel(unit)}</td>
                    <td className="px-4 py-3">
                      <Badge status={unit.isActive ? 'gaming' : 'idle'}>
                        {unit.isActive ? t('common.active') : t('common.inactive')}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          loading={toggleUnitMutation.isPending}
                          onClick={() => toggleUnitMutation.mutate(unit)}
                        >
                          {unit.isActive ? t('inventory.deactivate') : t('inventory.activate')}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          loading={deleteUnitMutation.isPending}
                          onClick={() => {
                            if (window.confirm(t('common.confirmDelete'))) {
                              deleteUnitMutation.mutate(unit.id);
                            }
                          }}
                        >
                          {t('common.delete')}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </DataTable>
            )}

            <div>
              <h3 className="mb-3 text-sm font-medium">
                {t('inventory.conversionLogs', { defaultValue: 'Conversion changes' })}
              </h3>
              {logsLoading ? (
                <PageLoader />
              ) : conversionLogs.length === 0 ? (
                <p className="text-sm text-muted">—</p>
              ) : (
                <ul className="space-y-2 text-sm text-muted">
                  {conversionLogs.map((log) => (
                    <li key={log.id} className="rounded-lg border border-border px-3 py-2">
                      <p className="font-medium text-text">{log.itemName}</p>
                      <p>
                        {log.oldUnitsPerLarge} → {log.newUnitsPerLarge}
                        {' · '}
                        {log.oldBaseUnitName}
                        {log.oldLargeUnitName ? ` / ${log.oldLargeUnitName}` : ''}
                        {' → '}
                        {log.newBaseUnitName}
                        {log.newLargeUnitName ? ` / ${log.newLargeUnitName}` : ''}
                      </p>
                      <p className="text-xs">
                        {log.changedByName} · {new Date(log.createdAt).toLocaleString()}
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        )
      )}

      <Modal open={!!adjustItem} onClose={() => setAdjustItem(null)} title={t('inventory.adjustStock')}>
        {adjustItem && (
          <div className="space-y-4">
            <p className="font-medium">{itemLabel(adjustItem)}</p>
            <Input label={t('inventory.newQty')} type="number" value={newQty} onChange={(e) => setNewQty(e.target.value)} />
            <Input label={t('inventory.reason')} value={reason} onChange={(e) => setReason(e.target.value)} />
            {error && <p className="text-sm text-danger">{error}</p>}
            <Button className="w-full" loading={adjustMutation.isPending} disabled={!reason.trim()} onClick={() => adjustMutation.mutate()}>
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
            <Button variant="secondary" onClick={() => setVoucherOpen(false)}>{t('session.cancel')}</Button>
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
          {voucherType === StockVoucherType.StockIn && (
            <p className="text-xs text-muted">
              {t('inventory.stockAlwaysBase', {
                defaultValue:
                  'Stock is always stored in the small unit. You can enter stock-in using the large unit; it is converted automatically.',
              })}
            </p>
          )}
          <Input label={t('inventory.notes')} value={voucherNotes} onChange={(e) => setVoucherNotes(e.target.value)} />
          <div className="max-h-72 space-y-2 overflow-y-auto">
            {items.filter((i) => i.isActive).map((item) => (
              <div key={item.id} className="flex items-center justify-between gap-3 rounded-lg border border-border px-3 py-2">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{itemLabel(item)}</p>
                  <p className="text-xs text-muted">{t('inventory.systemQty')}: {formatStockDisplay(item)}</p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  {voucherType === StockVoucherType.StockIn && hasLargeUnit(item) && (
                    <select
                      className="w-24 rounded-lg border border-border bg-surface-elevated px-2 py-2 text-xs"
                      value={voucherLineUnits[item.id] ?? InventoryUnitKind.Base}
                      onChange={(e) =>
                        setVoucherLineUnits((prev) => ({
                          ...prev,
                          [item.id]: Number(e.target.value),
                        }))
                      }
                    >
                      <option value={InventoryUnitKind.Base}>{item.baseUnitName}</option>
                      <option value={InventoryUnitKind.Large}>{item.largeUnitName}</option>
                    </select>
                  )}
                  <Input
                    type="number"
                    className="w-24"
                    placeholder={voucherType === StockVoucherType.Settlement ? String(item.currentQuantity) : '0'}
                    value={voucherLines[item.id] ?? ''}
                    onChange={(e) => setVoucherLines((prev) => ({ ...prev, [item.id]: e.target.value }))}
                  />
                </div>
              </div>
            ))}
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      </Modal>

      <Modal open={addItemOpen} onClose={() => setAddItemOpen(false)} title={t('inventory.addItem')}>
        <div className="space-y-3">
          <div className="space-y-2">
            <p className="text-sm font-medium text-text">{t('inventory.item')}</p>
            <Input label={t('inventory.itemNameAr')} value={itemNameAr} onChange={(e) => setItemNameAr(e.target.value)} />
            <Input label={t('inventory.itemName')} value={itemName} onChange={(e) => setItemName(e.target.value)} />
          </div>
          <Input label={t('inventory.sellPrice')} type="number" value={itemPrice} onChange={(e) => setItemPrice(e.target.value)} />
          <div>
            <label className="mb-1 block text-sm text-muted">
              {t('inventory.selectBaseUnit', { defaultValue: 'Select small unit' })}
            </label>
            <select
              className={selectClass}
              value={baseUnitId}
              onChange={(e) => {
                const next = e.target.value;
                setBaseUnitId(next);
                if (largeUnitId === next) setLargeUnitId('');
              }}
            >
              <option value="">{t('inventory.selectBaseUnit', { defaultValue: 'Select small unit' })}</option>
              {activeUnits.map((u) => (
                <option key={u.id} value={u.id}>{catalogUnitLabel(u)}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm text-muted">
              {t('inventory.selectLargeUnit', { defaultValue: 'Select large unit (optional)' })}
            </label>
            <select
              className={selectClass}
              value={largeUnitId}
              onChange={(e) => {
                setLargeUnitId(e.target.value);
                if (!e.target.value) setInitialStockUnit(InventoryUnitKind.Base);
              }}
            >
              <option value="">{t('inventory.none', { defaultValue: 'None' })}</option>
              {activeUnits
                .filter((u) => u.id !== baseUnitId)
                .map((u) => (
                  <option key={u.id} value={u.id}>{catalogUnitLabel(u)}</option>
                ))}
            </select>
            <p className="mt-1 text-xs text-muted">
              {t('inventory.largeOptional', { defaultValue: 'Large unit is optional' })}
            </p>
          </div>
          {largeUnitId && (
            <>
              <Input
                label={t('inventory.unitsPerLarge')}
                type="number"
                min={2}
                value={unitsPerLarge}
                onChange={(e) => setUnitsPerLarge(e.target.value)}
              />
              <p className="text-xs text-muted">{t('inventory.conversionHint')}</p>
              <div>
                <label className="mb-1 block text-sm text-muted">{t('inventory.initialStockUnit')}</label>
                <select
                  className={selectClass}
                  value={initialStockUnit}
                  onChange={(e) => setInitialStockUnit(Number(e.target.value) as InventoryUnitKind)}
                >
                  <option value={InventoryUnitKind.Base}>
                    {selectedBaseUnit ? catalogUnitLabel(selectedBaseUnit) : t('inventory.baseUnit')}
                  </option>
                  <option value={InventoryUnitKind.Large}>
                    {selectedLargeUnit ? catalogUnitLabel(selectedLargeUnit) : t('inventory.largeUnit')}
                  </option>
                </select>
              </div>
            </>
          )}
          <Input label={t('inventory.initialStock')} type="number" value={itemQty} onChange={(e) => setItemQty(e.target.value)} />
          <Input label={t('inventory.threshold')} type="number" value={itemThreshold} onChange={(e) => setItemThreshold(e.target.value)} />
          <p className="text-xs text-muted">
            {t('inventory.stockAlwaysBase', {
              defaultValue:
                'Stock is always stored in the small unit. You can enter stock-in using the large unit; it is converted automatically.',
            })}
          </p>
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={createItemMutation.isPending}
            disabled={
              !itemName.trim() ||
              !itemPrice ||
              !baseUnitId ||
              (!!largeUnitId && Number(unitsPerLarge) < 2)
            }
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
          <p className="font-medium">{voucher.voucherNumber} · {typeLabel}</p>
          <p className="text-sm text-muted">{voucher.createdByName} · {new Date(voucher.createdAt).toLocaleString()}</p>
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
            {l.variance != null && l.variance !== 0 && ` · ${t('inventory.variance')} ${l.variance > 0 ? '+' : ''}${l.variance}`}
          </li>
        ))}
      </ul>
      <div className="flex flex-wrap gap-2">
        {voucher.status === StockVoucherStatus.Draft && canAdjust && (
          <Button size="sm" loading={posting} onClick={onPost}>{t('inventory.postVoucher')}</Button>
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

function PoCard({
  order,
  canOrder,
  canReceive,
  onOrder,
  onReceive,
  ordering,
  receiving,
}: {
  order: PurchaseOrder;
  canOrder: boolean;
  canReceive: boolean;
  onOrder: () => void;
  onReceive: () => void;
  ordering: boolean;
  receiving: boolean;
}) {
  const { t } = useTranslation();
  const status = poStatusKey[order.status] ?? 'idle';

  return (
    <div className="rounded-xl border border-border bg-surface-elevated p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="font-medium">{order.supplierName ?? t('inventory.noSupplier')}</p>
          <p className="text-sm text-muted">{order.createdByName}</p>
        </div>
        <div className="flex items-center gap-3">
          <Badge status={status}>{t(`inventory.poStatus.${order.status}`, { defaultValue: String(order.status) })}</Badge>
          <span className="font-semibold text-accent">{formatCurrency(order.totalCost)}</span>
        </div>
      </div>
      <ul className="mb-3 space-y-1 text-sm text-muted">
        {order.lines.map((l) => (
          <li key={l.id}>{l.itemName} × {l.orderedQuantity} @ {formatCurrency(l.unitCost)}</li>
        ))}
      </ul>
      <div className="flex gap-2">
        {order.status === PurchaseOrderStatus.Draft && canOrder && (
          <Button size="sm" loading={ordering} onClick={onOrder}>{t('inventory.markOrdered')}</Button>
        )}
        {order.status === PurchaseOrderStatus.Ordered && canReceive && (
          <Button size="sm" loading={receiving} onClick={onReceive}>{t('inventory.receive')}</Button>
        )}
      </div>
    </div>
  );
}
