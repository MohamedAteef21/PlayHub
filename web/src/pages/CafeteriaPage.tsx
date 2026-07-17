import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cafeteriaApi, sessionsApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import type { CafeteriaItem, CafeteriaItemVariant } from '@/types';
import { PaymentMethod } from '@/types';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHelpers';

interface CartLine {
  item: CafeteriaItem;
  variant: CafeteriaItemVariant;
  quantity: number;
  stockDeduct: number;
}

type SaleMode = 'walkin' | 'session';

function cartKey(itemId: string, variantId: string) {
  return `${itemId}:${variantId}`;
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
  const [pickItem, setPickItem] = useState<CafeteriaItem | null>(null);
  const [pickVariantId, setPickVariantId] = useState('');
  const [stockDeduct, setStockDeduct] = useState('1');
  const [sellQty, setSellQty] = useState('1');

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
  const cartTotal = cart.reduce((sum, l) => sum + l.variant.sellPrice * l.quantity, 0);

  function itemLabel(item: CafeteriaItem) {
    return i18n.language === 'ar' && item.nameAr ? item.nameAr : item.name;
  }

  function lineLabel(line: CartLine) {
    return `${itemLabel(line.item)} — ${line.variant.name}`;
  }

  function openPick(item: CafeteriaItem) {
    if (!canSell || item.currentQuantity <= 0) return;
    const variants = (item.variants ?? []).filter((v) => v.isActive);
    if (variants.length === 0) {
      setError(t('inventory.noVariants', { defaultValue: 'No variants for this item.' }));
      return;
    }
    setPickItem(item);
    setPickVariantId(variants[0].id);
    setStockDeduct('1');
    setSellQty('1');
    setError('');
  }

  function confirmPick() {
    if (!pickItem) return;
    const variant = (pickItem.variants ?? []).find((v) => v.id === pickVariantId);
    if (!variant) return;
    const qty = Math.max(1, Number(sellQty) || 1);
    const deduct = Math.max(1, Number(stockDeduct) || 1);
    if (deduct > pickItem.currentQuantity) {
      setError(t('inventory.insufficientStock', { defaultValue: 'Insufficient stock.' }));
      return;
    }
    const key = cartKey(pickItem.id, variant.id);
    setCart((prev) => {
      const existing = prev.find((l) => cartKey(l.item.id, l.variant.id) === key);
      if (existing) {
        return prev.map((l) =>
          cartKey(l.item.id, l.variant.id) === key
            ? {
                ...l,
                quantity: l.quantity + qty,
                stockDeduct: l.stockDeduct + deduct,
                item: pickItem,
                variant,
              }
            : l
        );
      }
      return [...prev, { item: pickItem, variant, quantity: qty, stockDeduct: deduct }];
    });
    setPickItem(null);
    setError('');
  }

  function updateQty(itemId: string, variantId: string, delta: number) {
    setCart((prev) =>
      prev
        .map((l) => {
          if (l.item.id !== itemId || l.variant.id !== variantId) return l;
          const next = Math.max(0, l.quantity + delta);
          const deductPer = l.quantity > 0 ? l.stockDeduct / l.quantity : 1;
          return {
            ...l,
            quantity: next,
            stockDeduct: Math.max(next > 0 ? Math.round(deductPer * next) : 0, next > 0 ? 1 : 0),
          };
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
            line.variant.id,
            line.quantity,
            line.stockDeduct,
            customerName.trim() || undefined
          );
        }
        return;
      }
      await cafeteriaApi.createSale(
        cart.map((l) => ({
          cafeteriaItemId: l.item.id,
          variantId: l.variant.id,
          quantity: l.quantity,
          stockDeductQuantity: l.stockDeduct,
        })),
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

  const pickVariants = (pickItem?.variants ?? []).filter((v) => v.isActive);

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

      {error && <p className="mb-3 text-sm text-danger">{error}</p>}

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {activeItems.map((item) => (
            <Card key={item.id} className="cursor-pointer" onClick={() => openPick(item)}>
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="font-medium">{itemLabel(item)}</p>
                  <p className="text-xs text-muted">
                    {(item.variants ?? []).filter((v) => v.isActive).length}{' '}
                    {t('inventory.variants', { defaultValue: 'variants' })}
                  </p>
                </div>
                {item.isLowStock && <Badge status="inactive">{t('inventory.lowStock')}</Badge>}
              </div>
              <p className="mt-2 text-sm text-muted">
                {t('inventory.qty')}: {item.currentQuantity}
              </p>
            </Card>
          ))}
        </div>

        <Card className="h-fit">
          <p className="mb-3 font-medium">{t('cafeteria.cart')}</p>
          {cart.length === 0 ? (
            <p className="text-sm text-muted">{t('cafeteria.cartEmpty')}</p>
          ) : (
            <div className="space-y-3">
              {cart.map((l) => (
                <div key={cartKey(l.item.id, l.variant.id)} className="flex items-center justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{lineLabel(l)}</p>
                    <p className="text-xs text-muted">
                      {formatCurrency(l.variant.sellPrice)} · {t('inventory.stockDeduct', { defaultValue: 'Stock' })}{' '}
                      {l.stockDeduct}
                    </p>
                  </div>
                  <div className="flex items-center gap-1">
                    <Button size="sm" variant="secondary" onClick={() => updateQty(l.item.id, l.variant.id, -1)}>
                      -
                    </Button>
                    <span className="w-6 text-center text-sm">{l.quantity}</span>
                    <Button size="sm" variant="secondary" onClick={() => updateQty(l.item.id, l.variant.id, 1)}>
                      +
                    </Button>
                  </div>
                </div>
              ))}
              <div className="flex items-center justify-between border-t border-border pt-3">
                <span className="font-medium">{t('common.total')}</span>
                <span className="font-semibold">{formatCurrency(cartTotal)}</span>
              </div>
              <Button className="w-full" disabled={!canSell} onClick={() => setCheckoutOpen(true)}>
                {t('cafeteria.checkout')}
              </Button>
            </div>
          )}
        </Card>
      </div>

      <Modal
        open={!!pickItem}
        onClose={() => setPickItem(null)}
        title={pickItem ? itemLabel(pickItem) : ''}
      >
        {pickItem && (
          <div className="space-y-3">
            <div>
              <label className="mb-1 block text-sm text-muted">
                {t('inventory.variant', { defaultValue: 'Variant' })}
              </label>
              <select
                className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                value={pickVariantId}
                onChange={(e) => setPickVariantId(e.target.value)}
              >
                {pickVariants.map((v) => (
                  <option key={v.id} value={v.id}>
                    {v.name} — {formatCurrency(v.sellPrice)}
                  </option>
                ))}
              </select>
            </div>
            <Input
              label={t('inventory.sellQty', { defaultValue: 'Sell quantity' })}
              type="number"
              min={1}
              value={sellQty}
              onChange={(e) => setSellQty(e.target.value)}
            />
            <Input
              label={t('inventory.askStockDeduct', {
                defaultValue: 'How much stock to deduct?',
              })}
              type="number"
              min={1}
              value={stockDeduct}
              onChange={(e) => setStockDeduct(e.target.value)}
            />
            <p className="text-xs text-muted">
              {t('inventory.stockAvailable', { defaultValue: 'Available' })}: {pickItem.currentQuantity}
            </p>
            {error && <p className="text-sm text-danger">{error}</p>}
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setPickItem(null)}>
                {t('common.cancel')}
              </Button>
              <Button onClick={confirmPick}>{t('common.add')}</Button>
            </div>
          </div>
        )}
      </Modal>

      <Modal open={checkoutOpen} onClose={() => setCheckoutOpen(false)} title={t('cafeteria.checkout')}>
        <div className="space-y-3">
          <Input
            label={t('cafeteria.customerName')}
            value={customerName}
            onChange={(e) => setCustomerName(e.target.value)}
          />
          {saleMode === 'walkin' && (
            <>
              <label className="mb-1 block text-sm text-muted">{t('common.paymentMethod')}</label>
              <select
                className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(Number(e.target.value))}
              >
                <option value={PaymentMethod.Cash}>{t('payment.cash')}</option>
                <option value={PaymentMethod.BankTransfer}>{t('payment.transfer')}</option>
                <option value={PaymentMethod.DigitalWallet}>{t('payment.wallet')}</option>
                <option value={PaymentMethod.Deferred}>{t('payment.deferred')}</option>
              </select>
              {paymentMethod === PaymentMethod.Deferred && (
                <Input
                  label={t('cafeteria.debtorName')}
                  value={debtorName}
                  onChange={(e) => setDebtorName(e.target.value)}
                />
              )}
            </>
          )}
          <p className="text-lg font-semibold">{formatCurrency(cartTotal)}</p>
          {error && <p className="text-sm text-danger">{error}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setCheckoutOpen(false)}>
              {t('common.cancel')}
            </Button>
            <Button loading={saleMutation.isPending} onClick={() => saleMutation.mutate()}>
              {t('common.confirm')}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
