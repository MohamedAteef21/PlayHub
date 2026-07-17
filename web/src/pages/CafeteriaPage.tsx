import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cafeteriaApi, sessionsApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import {
  formatStockDisplay,
  hasLargeUnit,
  lineUnitPrice,
  maxSellQuantity,
  unitLabel,
} from '@/lib/itemUnits';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import type { CafeteriaItem } from '@/types';
import { InventoryUnitKind, PaymentMethod } from '@/types';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHelpers';

interface CartLine {
  item: CafeteriaItem;
  quantity: number;
  unit: InventoryUnitKind;
}

type SaleMode = 'walkin' | 'session';

function cartKey(itemId: string, unit: InventoryUnitKind) {
  return `${itemId}:${unit}`;
}

export function CafeteriaPage() {
  const { t, i18n } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);
  const queryClient = useQueryClient();
  const canSell = hasPermission(user, Permissions.CafeteriaSell);

  const [cart, setCart] = useState<CartLine[]>([]);
  const [saleMode, setSaleMode] = useState<SaleMode>('walkin');
  const [sessionId, setSessionId] = useState('');
  const [checkoutOpen, setCheckoutOpen] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState<number>(PaymentMethod.Cash);
  const [debtorName, setDebtorName] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [error, setError] = useState('');
  const [pickUnitItem, setPickUnitItem] = useState<CafeteriaItem | null>(null);

  const { data: items = [], isLoading } = useQuery({
    queryKey: ['cafeteria-items', user?.id, activeBranchId],
    queryFn: cafeteriaApi.getItems,
  });

  const { data: activeSessions = [] } = useQuery({
    queryKey: ['sessions', 'active', user?.id, activeBranchId],
    queryFn: sessionsApi.getActive,
    enabled: canSell,
    refetchInterval: 15000,
    meta: { silent: true },
  });

  const activeItems = useMemo(() => items.filter((i) => i.isActive), [items]);
  const cartTotal = cart.reduce((sum, l) => sum + lineUnitPrice(l.item, l.unit) * l.quantity, 0);

  function itemLabel(item: CafeteriaItem) {
    return i18n.language === 'ar' && item.nameAr ? item.nameAr : item.name;
  }

  function addToCart(item: CafeteriaItem, unit: InventoryUnitKind) {
    if (!canSell) return;
    const max = maxSellQuantity(item, unit);
    if (max <= 0) return;
    const key = cartKey(item.id, unit);
    setCart((prev) => {
      const existing = prev.find((l) => cartKey(l.item.id, l.unit) === key);
      if (existing) {
        const nextQty = Math.min(existing.quantity + 1, max);
        return prev.map((l) =>
          cartKey(l.item.id, l.unit) === key ? { ...l, quantity: nextQty, item } : l
        );
      }
      return [...prev, { item, quantity: 1, unit }];
    });
    setPickUnitItem(null);
  }

  function handleAddClick(item: CafeteriaItem) {
    if (!canSell || item.currentQuantity <= 0) return;
    if (hasLargeUnit(item)) {
      setPickUnitItem(item);
      return;
    }
    addToCart(item, InventoryUnitKind.Base);
  }

  function updateQty(itemId: string, unit: InventoryUnitKind, delta: number) {
    setCart((prev) =>
      prev
        .map((l) => {
          if (l.item.id !== itemId || l.unit !== unit) return l;
          const max = maxSellQuantity(l.item, l.unit);
          const next = Math.max(0, Math.min(l.quantity + delta, max));
          return { ...l, quantity: next };
        })
        .filter((l) => l.quantity > 0)
    );
  }

  const saleMutation = useMutation({
    mutationFn: async () => {
      if (saleMode === 'session') {
        if (!sessionId) throw new Error(t('cafeteria.selectSession'));
        for (const line of cart) {
          await sessionsApi.addCafeteria(
            sessionId,
            line.item.id,
            line.quantity,
            customerName.trim() || undefined,
            line.unit
          );
        }
        return;
      }
      await cafeteriaApi.createSale(
        cart.map((l) => ({ cafeteriaItemId: l.item.id, quantity: l.quantity, unit: l.unit })),
        {
          paymentMethod,
          debtorName: paymentMethod === PaymentMethod.Deferred ? debtorName : undefined,
        },
        customerName.trim() || undefined
      );
    },
    onSuccess: () => {
      setCart([]);
      setCheckoutOpen(false);
      setDebtorName('');
      setCustomerName('');
      setPaymentMethod(PaymentMethod.Cash);
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  if (isLoading) {
    return <p className="text-muted">{t('common.loading')}</p>;
  }

  return (
    <div>
      <PageHeader title={t('cafeteria.title')} />

      <p className="mb-4 text-sm text-muted">{t('cafeteria.sellHint')}</p>

      <div className="mb-4 flex flex-wrap gap-2">
        <Button
          size="sm"
          variant={saleMode === 'walkin' ? 'primary' : 'secondary'}
          onClick={() => setSaleMode('walkin')}
        >
          {t('cafeteria.walkIn')}
        </Button>
        <Button
          size="sm"
          variant={saleMode === 'session' ? 'primary' : 'secondary'}
          onClick={() => setSaleMode('session')}
        >
          <Icon name="gaming" className="h-3.5 w-3.5" />
          {t('cafeteria.chargeToSession')}
        </Button>
      </div>

      {saleMode === 'session' && (
        <div className="mb-4 max-w-md">
          <label className="mb-1 block text-sm text-muted">{t('cafeteria.openSession')}</label>
          <select
            className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
            value={sessionId}
            onChange={(e) => setSessionId(e.target.value)}
          >
            <option value="">{t('cafeteria.selectSession')}</option>
            {activeSessions.map((s) => (
              <option key={s.id} value={s.id}>
                {s.deviceName} · {s.roomName} ({formatCurrency(s.totalCost)})
              </option>
            ))}
          </select>
          {activeSessions.length === 0 && (
            <p className="mt-1 text-xs text-warning">{t('cafeteria.noOpenSessions')}</p>
          )}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {activeItems.length === 0 ? (
            <p className="col-span-full text-muted">{t('cafeteria.noItems')}</p>
          ) : (
            activeItems.map((item) => (
              <Card key={item.id} className="flex flex-col gap-3">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-medium">{itemLabel(item)}</p>
                    <p className="text-lg font-bold text-accent">
                      {formatCurrency(item.sellPrice)}
                      <span className="ms-1 text-xs font-normal text-muted">/ {item.baseUnitName}</span>
                    </p>
                    {hasLargeUnit(item) && (
                      <p className="text-xs text-muted">
                        {formatCurrency(item.sellPrice * item.unitsPerLarge)} / {item.largeUnitName}
                      </p>
                    )}
                  </div>
                  {item.isLowStock && <Badge status="paused">{t('cafeteria.lowStock')}</Badge>}
                </div>
                <p className="text-sm text-muted">
                  {t('cafeteria.stock')}: {formatStockDisplay(item)}
                </p>
                {canSell && (
                  <Button
                    size="sm"
                    disabled={item.currentQuantity <= 0}
                    onClick={() => handleAddClick(item)}
                  >
                    {t('cafeteria.addToCart')}
                  </Button>
                )}
              </Card>
            ))
          )}
        </div>

        <Card className="sticky top-4 h-fit space-y-4">
          <h2 className="text-lg font-semibold">{t('cafeteria.cart')}</h2>
          <p className="text-xs text-muted">
            {saleMode === 'session' ? t('cafeteria.willChargeSession') : t('cafeteria.willPayNow')}
          </p>
          {cart.length === 0 ? (
            <p className="text-sm text-muted">{t('cafeteria.emptyCart')}</p>
          ) : (
            <ul className="space-y-3">
              {cart.map((line) => (
                <li key={cartKey(line.item.id, line.unit)} className="flex items-center justify-between gap-2 text-sm">
                  <span className="flex-1 truncate">
                    {itemLabel(line.item)}
                    <span className="ms-1 text-xs text-muted">({unitLabel(line.item, line.unit)})</span>
                  </span>
                  <div className="flex items-center gap-1">
                    <Button variant="ghost" size="sm" onClick={() => updateQty(line.item.id, line.unit, -1)}>−</Button>
                    <span className="w-6 text-center">{line.quantity}</span>
                    <Button variant="ghost" size="sm" onClick={() => updateQty(line.item.id, line.unit, 1)}>+</Button>
                  </div>
                  <span className="w-16 text-end font-medium">
                    {formatCurrency(lineUnitPrice(line.item, line.unit) * line.quantity)}
                  </span>
                </li>
              ))}
            </ul>
          )}
          <div className="border-t border-border pt-3">
            <Input
              label={t('cafeteria.customerName')}
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
              placeholder={t('cafeteria.customerNameOptional')}
            />
            <div className="mt-3 flex justify-between font-semibold">
              <span>{t('cafeteria.total')}</span>
              <span className="text-success">{formatCurrency(cartTotal)}</span>
            </div>
            {canSell && (
              <Button
                className="mt-3 w-full"
                disabled={cart.length === 0 || (saleMode === 'session' && !sessionId)}
                onClick={() => {
                  setError('');
                  if (saleMode === 'session') {
                    saleMutation.mutate();
                  } else {
                    setCheckoutOpen(true);
                  }
                }}
                loading={saleMode === 'session' && saleMutation.isPending}
              >
                {saleMode === 'session' ? t('cafeteria.addToSessionBill') : t('cafeteria.checkout')}
              </Button>
            )}
            {error && saleMode === 'session' && <p className="mt-2 text-sm text-danger">{error}</p>}
          </div>
        </Card>
      </div>

      <Modal open={checkoutOpen} onClose={() => setCheckoutOpen(false)} title={t('cafeteria.checkout')}>
        <div className="space-y-4">
          <p className="text-2xl font-bold text-success">{formatCurrency(cartTotal)}</p>
          <Input
            label={t('cafeteria.customerName')}
            value={customerName}
            onChange={(e) => setCustomerName(e.target.value)}
            placeholder={t('cafeteria.customerNameOptional')}
          />
          <div>
            <label className="mb-1 block text-sm text-muted">{t('session.paymentMethod')}</label>
            <select
              value={paymentMethod}
              onChange={(e) => setPaymentMethod(Number(e.target.value))}
              className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
            >
              <option value={PaymentMethod.Cash}>{t('session.cash')}</option>
              <option value={PaymentMethod.Deferred}>{t('session.deferred')}</option>
            </select>
          </div>
          {paymentMethod === PaymentMethod.Deferred && (
            <Input
              label={t('session.debtorName')}
              value={debtorName}
              onChange={(e) => setDebtorName(e.target.value)}
            />
          )}
          {error && <p className="text-sm text-danger">{error}</p>}
          <div className="flex gap-2">
            <Button variant="secondary" className="flex-1" onClick={() => setCheckoutOpen(false)}>
              {t('session.cancel')}
            </Button>
            <Button
              className="flex-1"
              loading={saleMutation.isPending}
              onClick={() => saleMutation.mutate()}
            >
              {t('session.confirm')}
            </Button>
          </div>
        </div>
      </Modal>

      <Modal
        open={!!pickUnitItem}
        onClose={() => setPickUnitItem(null)}
        title={pickUnitItem ? itemLabel(pickUnitItem) : t('cafeteria.sellUnit')}
      >
        {pickUnitItem && (
          <div className="space-y-3">
            <p className="text-sm text-muted">{t('cafeteria.sellUnit')}</p>
            <Button
              className="w-full"
              disabled={maxSellQuantity(pickUnitItem, InventoryUnitKind.Base) <= 0}
              onClick={() => addToCart(pickUnitItem, InventoryUnitKind.Base)}
            >
              {pickUnitItem.baseUnitName}
              {' — '}
              {formatCurrency(pickUnitItem.sellPrice)}
            </Button>
            <Button
              className="w-full"
              variant="secondary"
              disabled={maxSellQuantity(pickUnitItem, InventoryUnitKind.Large) <= 0}
              onClick={() => addToCart(pickUnitItem, InventoryUnitKind.Large)}
            >
              {pickUnitItem.largeUnitName}
              {' — '}
              {formatCurrency(pickUnitItem.sellPrice * pickUnitItem.unitsPerLarge)}
            </Button>
          </div>
        )}
      </Modal>
    </div>
  );
}
