import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, cafeteriaApi, sessionsApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import type { CafeteriaAddOn, CafeteriaHold, CafeteriaItem, CafeteriaItemVariant, MissingIngredient } from '@/types';
import { CafeteriaItemKind, PaymentMethod } from '@/types';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHelpers';

interface CartAddOn {
  addOn: CafeteriaAddOn;
  quantity: number;
}

interface CartLine {
  item: CafeteriaItem;
  variant: CafeteriaItemVariant;
  quantity: number;
  stockDeduct: number;
  addOns: CartAddOn[];
}

type SaleMode = 'walkin' | 'session' | 'waiting';

function cartKey(itemId: string, variantId: string) {
  return `${itemId}:${variantId}`;
}

function variantHasRecipe(variant: CafeteriaItemVariant) {
  return (variant.recipeLines ?? []).length > 0;
}

function needsManualStockDeduct(item: CafeteriaItem, variant: CafeteriaItemVariant) {
  return item.kind === CafeteriaItemKind.SellAsIs && !variantHasRecipe(variant);
}

function parseMissingIngredients(err: unknown): MissingIngredient[] | null {
  if (!(err instanceof ApiError) || err.code !== 'MISSING_INGREDIENTS') return null;
  const body = err.data as { missing?: MissingIngredient[] } | undefined;
  return body?.missing ?? null;
}

function lineTotal(line: CartLine) {
  const base = line.variant.sellPrice * line.quantity;
  const addons = line.addOns.reduce((sum, a) => sum + a.addOn.sellPrice * a.quantity, 0);
  return base + addons;
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
  const [guestName, setGuestName] = useState('');
  const [holdConvertId, setHoldConvertId] = useState<string | null>(null);
  const [holdAttachId, setHoldAttachId] = useState<string | null>(null);
  const [holdAttachSessionId, setHoldAttachSessionId] = useState('');
  const [error, setError] = useState('');
  const [pickItem, setPickItem] = useState<CafeteriaItem | null>(null);
  const [pickVariantId, setPickVariantId] = useState('');
  const [stockDeduct, setStockDeduct] = useState('1');
  const [sellQty, setSellQty] = useState('1');
  const [pickAddOns, setPickAddOns] = useState<Record<string, number>>({});
  const [missingDialog, setMissingDialog] = useState<{
    missing: MissingIngredient[];
    retry: () => Promise<void>;
  } | null>(null);

  const { data: items = [], isLoading } = useQuery({
    queryKey: ['cafeteria-items', 'for-sale', user?.id, activeBranchId],
    queryFn: () => cafeteriaApi.getItems({ forSaleOnly: true }),
  });

  const { data: addOns = [] } = useQuery({
    queryKey: ['cafeteria-addons', 'active', user?.id, activeBranchId],
    queryFn: () => cafeteriaApi.getAddOns(true),
  });

  const { data: activeSessions = [] } = useQuery({
    queryKey: ['sessions', 'active', user?.id, activeBranchId],
    queryFn: sessionsApi.getActive,
    enabled: canSell,
    refetchInterval: 15000,
    meta: { silent: true },
  });

  const { data: openHolds = [] } = useQuery({
    queryKey: ['cafeteria-holds', 'open', user?.id, activeBranchId],
    queryFn: cafeteriaApi.getOpenHolds,
    enabled: canSell,
    refetchInterval: 15000,
    meta: { silent: true },
  });

  const activeItems = useMemo(() => items.filter((i) => i.isActive), [items]);
  const cartTotal = cart.reduce((sum, l) => sum + lineTotal(l), 0);

  function itemLabel(item: CafeteriaItem) {
    return i18n.language === 'ar' && item.nameAr ? item.nameAr : item.name;
  }

  function lineLabel(line: CartLine) {
    return `${itemLabel(line.item)} — ${line.variant.name}`;
  }

  function openPick(item: CafeteriaItem) {
    if (!canSell) return;
    const variants = (item.variants ?? []).filter((v) => v.isActive);
    if (variants.length === 0) {
      setError(t('inventory.noVariants'));
      return;
    }
    const outOfStock =
      item.kind === CafeteriaItemKind.SellAsIs &&
      !variants.some((v) => variantHasRecipe(v)) &&
      item.currentQuantity <= 0;
    if (outOfStock) return;

    setPickItem(item);
    setPickVariantId(variants[0].id);
    setStockDeduct('1');
    setSellQty('1');
    setPickAddOns({});
    setError('');
  }

  const pickVariant = (pickItem?.variants ?? []).find((v) => v.id === pickVariantId);
  const showStockDeduct = pickItem && pickVariant ? needsManualStockDeduct(pickItem, pickVariant) : false;

  function confirmPick() {
    if (!pickItem || !pickVariant) return;
    const qty = Math.max(1, Number(sellQty) || 1);
    const manual = needsManualStockDeduct(pickItem, pickVariant);
    const deduct = manual ? Math.max(1, Number(stockDeduct) || qty) : qty;
    if (manual && deduct > pickItem.currentQuantity) {
      setError(t('inventory.insufficientStock'));
      return;
    }

    const selectedAddOns: CartAddOn[] = Object.entries(pickAddOns)
      .filter(([, q]) => q > 0)
      .map(([id, quantity]) => {
        const addOn = addOns.find((a) => a.id === id)!;
        return { addOn, quantity };
      });

    const key = cartKey(pickItem.id, pickVariant.id);
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
                variant: pickVariant,
                addOns: mergeAddOns(existing.addOns, selectedAddOns),
              }
            : l
        );
      }
      return [
        ...prev,
        {
          item: pickItem,
          variant: pickVariant,
          quantity: qty,
          stockDeduct: deduct,
          addOns: selectedAddOns,
        },
      ];
    });
    setPickItem(null);
    setError('');
  }

  function mergeAddOns(existing: CartAddOn[], added: CartAddOn[]): CartAddOn[] {
    const map = new Map(existing.map((a) => [a.addOn.id, { ...a }]));
    for (const a of added) {
      const prev = map.get(a.addOn.id);
      if (prev) prev.quantity += a.quantity;
      else map.set(a.addOn.id, { ...a });
    }
    return [...map.values()];
  }

  function updateQty(itemId: string, variantId: string, delta: number) {
    setCart((prev) =>
      prev
        .map((l) => {
          if (l.item.id !== itemId || l.variant.id !== variantId) return l;
          const next = Math.max(0, l.quantity + delta);
          const manual = needsManualStockDeduct(l.item, l.variant);
          const deductPer = l.quantity > 0 ? l.stockDeduct / l.quantity : 1;
          return {
            ...l,
            quantity: next,
            stockDeduct: manual
              ? Math.max(next > 0 ? Math.round(deductPer * next) : 0, next > 0 ? 1 : 0)
              : next,
          };
        })
        .filter((l) => l.quantity > 0)
    );
  }

  async function executeSale(allowSkip = false) {
    if (saleMode === 'waiting') {
      await cafeteriaApi.createHold(
        cart.map((l) => ({
          cafeteriaItemId: l.item.id,
          variantId: l.variant.id,
          quantity: l.quantity,
          stockDeductQuantity: l.stockDeduct,
          addOns: l.addOns.map((a) => ({ addOnId: a.addOn.id, quantity: a.quantity })),
        })),
        { guestName: guestName.trim() || undefined, allowSkipMissingIngredients: allowSkip }
      );
      return;
    }
    if (saleMode === 'session') {
      if (!sessionId) throw new Error(t('cafeteria.selectSession'));
      for (const line of cart) {
        await sessionsApi.addCafeteria(
          sessionId,
          line.item.id,
          line.variant.id,
          line.quantity,
          line.stockDeduct,
          customerName.trim() || undefined,
          line.addOns.map((a) => ({ addOnId: a.addOn.id, quantity: a.quantity })),
          allowSkip
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
        addOns: l.addOns.map((a) => ({ addOnId: a.addOn.id, quantity: a.quantity })),
      })),
      {
        paymentMethod,
        debtorName: paymentMethod === PaymentMethod.Deferred ? debtorName : undefined,
      },
      customerName.trim() || undefined,
      allowSkip
    );
  }

  const saleMutation = useMutation({
    mutationFn: () => executeSale(false),
    onSuccess: () => {
      setCart([]);
      setCheckoutOpen(false);
      setDebtorName('');
      setCustomerName('');
      setGuestName('');
      setPaymentMethod(PaymentMethod.Cash);
      setMissingDialog(null);
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['cafeteria-holds'] });
    },
    onError: (e: unknown) => {
      const missing = parseMissingIngredients(e);
      if (missing) {
        setMissingDialog({
          missing,
          retry: async () => {
            await executeSale(true);
            setCart([]);
            setCheckoutOpen(false);
            setDebtorName('');
            setCustomerName('');
            setGuestName('');
            setPaymentMethod(PaymentMethod.Cash);
            setMissingDialog(null);
            queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
            queryClient.invalidateQueries({ queryKey: ['sessions'] });
            queryClient.invalidateQueries({ queryKey: ['cafeteria-holds'] });
          },
        });
        return;
      }
      setError(e instanceof Error ? e.message : t('common.error'));
    },
  });

  const convertHoldMutation = useMutation({
    mutationFn: (holdId: string) =>
      cafeteriaApi.convertHoldToSale(
        holdId,
        {
          paymentMethod,
          debtorName: paymentMethod === PaymentMethod.Deferred ? debtorName : undefined,
        },
        customerName.trim() || undefined
      ),
    onSuccess: () => {
      setHoldConvertId(null);
      setDebtorName('');
      setCustomerName('');
      setPaymentMethod(PaymentMethod.Cash);
      queryClient.invalidateQueries({ queryKey: ['cafeteria-holds'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const attachHoldMutation = useMutation({
    mutationFn: ({ holdId, sessionId }: { holdId: string; sessionId: string }) =>
      cafeteriaApi.attachHoldToSession(holdId, sessionId),
    onSuccess: () => {
      setHoldAttachId(null);
      setHoldAttachSessionId('');
      queryClient.invalidateQueries({ queryKey: ['cafeteria-holds'] });
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const cancelHoldMutation = useMutation({
    mutationFn: (holdId: string) => cafeteriaApi.cancelHold(holdId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['cafeteria-holds'] });
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
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
        <Button
          size="sm"
          variant={saleMode === 'waiting' ? 'primary' : 'secondary'}
          onClick={() => setSaleMode('waiting')}
        >
          {t('cafeteria.waitingList')}
        </Button>
      </div>

      {saleMode === 'waiting' && (
        <p className="mb-4 text-sm text-muted">{t('cafeteria.waitingHint')}</p>
      )}

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

      {saleMode === 'waiting' && (
        <Card className="mb-4 space-y-3">
          <p className="text-sm font-semibold">{t('cafeteria.openHolds')}</p>
          {openHolds.length === 0 ? (
            <p className="text-sm text-muted">{t('cafeteria.noOpenHolds')}</p>
          ) : (
            <div className="space-y-2">
              {openHolds.map((hold: CafeteriaHold) => (
                <div
                  key={hold.id}
                  className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border px-3 py-2"
                >
                  <div>
                    <p className="font-medium">
                      {hold.guestName || hold.customerName || t('cafeteria.guestNameOptional')}
                    </p>
                    <p className="text-xs text-muted">
                      {formatCurrency(hold.totalAmount)} · {new Date(hold.createdAt).toLocaleTimeString()}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-1">
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => {
                        setHoldAttachId(hold.id);
                        setHoldAttachSessionId(activeSessions[0]?.id ?? '');
                        setError('');
                      }}
                    >
                      {t('cafeteria.attachToSession')}
                    </Button>
                    <Button
                      size="sm"
                      onClick={() => {
                        setHoldConvertId(hold.id);
                        setCustomerName(hold.guestName || hold.customerName || '');
                        setPaymentMethod(PaymentMethod.Cash);
                        setDebtorName('');
                        setError('');
                      }}
                    >
                      {t('cafeteria.convertToWalkIn')}
                    </Button>
                    <Button
                      size="sm"
                      variant="danger"
                      loading={cancelHoldMutation.isPending}
                      onClick={() => {
                        if (window.confirm(t('common.confirmDelete'))) {
                          cancelHoldMutation.mutate(hold.id);
                        }
                      }}
                    >
                      {t('cafeteria.cancelHold')}
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}

      {error && <p className="mb-3 text-sm text-danger">{error}</p>}

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {activeItems.map((item) => {
            const variants = (item.variants ?? []).filter((v) => v.isActive);
            const sellAsIsNoRecipe =
              item.kind === CafeteriaItemKind.SellAsIs && variants.every((v) => !variantHasRecipe(v));
            const disabled = sellAsIsNoRecipe && item.currentQuantity <= 0;
            return (
              <Card
                key={item.id}
                className={disabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer'}
                onClick={() => !disabled && openPick(item)}
              >
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-medium">{itemLabel(item)}</p>
                    <p className="text-xs text-muted">
                      {variants.length} {t('inventory.variants')}
                    </p>
                  </div>
                  {item.isLowStock && <Badge status="inactive">{t('cafeteria.lowStock')}</Badge>}
                </div>
                {sellAsIsNoRecipe && (
                  <p className="mt-2 text-sm text-muted">
                    {t('inventory.qty')}: {item.currentQuantity}
                  </p>
                )}
              </Card>
            );
          })}
        </div>

        <Card className="h-fit">
          <p className="mb-3 font-medium">{t('cafeteria.cart')}</p>
          {cart.length === 0 ? (
            <p className="text-sm text-muted">
              {saleMode === 'waiting' ? t('cafeteria.waitingEmptyCart') : t('cafeteria.emptyCart')}
            </p>
          ) : (
            <div className="space-y-3">
              {cart.map((l) => (
                <div key={cartKey(l.item.id, l.variant.id)} className="space-y-1">
                  <div className="flex items-center justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{lineLabel(l)}</p>
                      <p className="text-xs text-muted">
                        {formatCurrency(l.variant.sellPrice)}
                        {needsManualStockDeduct(l.item, l.variant) && (
                          <>
                            {' · '}
                            {t('inventory.stockDeduct')} {l.stockDeduct}
                          </>
                        )}
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
                  {l.addOns.length > 0 && (
                    <ul className="ps-2 text-xs text-muted">
                      {l.addOns.map((a) => (
                        <li key={a.addOn.id}>
                          + {a.addOn.name} ×{a.quantity} ({formatCurrency(a.addOn.sellPrice * a.quantity)})
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              ))}
              <div className="flex items-center justify-between border-t border-border pt-3">
                <span className="font-medium">{t('common.total')}</span>
                <span className="font-semibold">{formatCurrency(cartTotal)}</span>
              </div>
              <Button className="w-full" disabled={!canSell} onClick={() => setCheckoutOpen(true)}>
                {saleMode === 'waiting' ? t('cafeteria.createHold') : t('cafeteria.checkout')}
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
        {pickItem && pickVariant && (
          <div className="space-y-3">
            <div>
              <label className="mb-1 block text-sm text-muted">{t('inventory.variant')}</label>
              <select
                className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                value={pickVariantId}
                onChange={(e) => {
                  setPickVariantId(e.target.value);
                  setStockDeduct(sellQty);
                }}
              >
                {pickVariants.map((v) => (
                  <option key={v.id} value={v.id}>
                    {v.name} — {formatCurrency(v.sellPrice)}
                  </option>
                ))}
              </select>
            </div>
            <Input
              label={t('inventory.sellQty')}
              type="number"
              min={1}
              value={sellQty}
              onChange={(e) => {
                setSellQty(e.target.value);
                if (showStockDeduct) setStockDeduct(e.target.value);
              }}
            />
            {showStockDeduct && (
              <>
                <Input
                  label={t('inventory.stockDeduct')}
                  type="number"
                  min={1}
                  value={stockDeduct}
                  onChange={(e) => setStockDeduct(e.target.value)}
                />
                <p className="text-xs text-muted">
                  {t('inventory.stockAvailable')}: {pickItem.currentQuantity}
                </p>
              </>
            )}
            {addOns.length > 0 && (
              <div className="space-y-2 border-t border-border pt-2">
                <p className="text-sm font-medium">{t('inventory.addOns')}</p>
                {addOns.map((addon) => (
                  <div key={addon.id} className="flex items-center justify-between gap-2">
                    <span className="text-sm">
                      {addon.name} (+{formatCurrency(addon.sellPrice)})
                    </span>
                    <Input
                      type="number"
                      min={0}
                      className="w-20"
                      value={pickAddOns[addon.id] ?? 0}
                      onChange={(e) =>
                        setPickAddOns((prev) => ({
                          ...prev,
                          [addon.id]: Math.max(0, Number(e.target.value) || 0),
                        }))
                      }
                    />
                  </div>
                ))}
              </div>
            )}
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

      <Modal open={checkoutOpen} onClose={() => setCheckoutOpen(false)} title={saleMode === 'waiting' ? t('cafeteria.createHold') : t('cafeteria.checkout')}>
        <div className="space-y-3">
          {saleMode === 'waiting' ? (
            <Input
              label={t('cafeteria.guestName')}
              value={guestName}
              onChange={(e) => setGuestName(e.target.value)}
              placeholder={t('cafeteria.guestNameOptional')}
            />
          ) : saleMode === 'walkin' ? (
            <Input
              label={t('cafeteria.customerName')}
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
              placeholder={t('cafeteria.customerNameOptional')}
            />
          ) : (
            // Session mode: only ask for a name when the open session has no guest yet
            (() => {
              const session = activeSessions.find((s) => s.id === sessionId);
              const hasGuest =
                !!session?.customerId || !!session?.customerName || !!session?.quickGuestName;
              if (hasGuest) return null;
              return (
                <Input
                  label={t('cafeteria.customerName')}
                  value={customerName}
                  onChange={(e) => setCustomerName(e.target.value)}
                  placeholder={t('cafeteria.customerNameOptional')}
                />
              );
            })()
          )}
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
              {saleMode === 'waiting' ? t('cafeteria.createHold') : t('common.confirm')}
            </Button>
          </div>
        </div>
      </Modal>

      <Modal
        open={!!holdConvertId}
        onClose={() => setHoldConvertId(null)}
        title={t('cafeteria.convertToWalkIn')}
      >
        <div className="space-y-3">
          <Input
            label={t('cafeteria.customerName')}
            value={customerName}
            onChange={(e) => setCustomerName(e.target.value)}
          />
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
          {error && <p className="text-sm text-danger">{error}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setHoldConvertId(null)}>
              {t('common.cancel')}
            </Button>
            <Button
              loading={convertHoldMutation.isPending}
              onClick={() => holdConvertId && convertHoldMutation.mutate(holdConvertId)}
            >
              {t('common.confirm')}
            </Button>
          </div>
        </div>
      </Modal>

      <Modal
        open={!!holdAttachId}
        onClose={() => setHoldAttachId(null)}
        title={t('cafeteria.attachToSession')}
      >
        <div className="space-y-3">
          <label className="mb-1 block text-sm text-muted">{t('cafeteria.openSession')}</label>
          <select
            className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
            value={holdAttachSessionId}
            onChange={(e) => setHoldAttachSessionId(e.target.value)}
          >
            <option value="">{t('cafeteria.selectSession')}</option>
            {activeSessions.map((s) => (
              <option key={s.id} value={s.id}>
                {s.deviceName} · {s.roomName} ({formatCurrency(s.totalCost)})
              </option>
            ))}
          </select>
          {error && <p className="text-sm text-danger">{error}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setHoldAttachId(null)}>
              {t('common.cancel')}
            </Button>
            <Button
              loading={attachHoldMutation.isPending}
              disabled={!holdAttachSessionId}
              onClick={() =>
                holdAttachId &&
                attachHoldMutation.mutate({ holdId: holdAttachId, sessionId: holdAttachSessionId })
              }
            >
              {t('common.confirm')}
            </Button>
          </div>
        </div>
      </Modal>

      <Modal
        open={!!missingDialog}
        onClose={() => setMissingDialog(null)}
        title={t('inventory.missingIngredients')}
      >
        {missingDialog && (
          <div className="space-y-3">
            <p className="text-sm text-muted">{t('inventory.skipMissingConfirm')}</p>
            <ul className="space-y-1 text-sm">
              {missingDialog.missing.map((m) => (
                <li key={m.warehouseItemId} className="rounded-lg border border-border px-3 py-2">
                  <span className="font-medium">{m.name}</span>
                  <span className="text-muted">
                    {' '}
                    — {t('inventory.required')}: {m.required}, {t('inventory.available')}: {m.available}
                  </span>
                </li>
              ))}
            </ul>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setMissingDialog(null)}>
                {t('common.cancel')}
              </Button>
              <Button
                loading={saleMutation.isPending}
                onClick={async () => {
                  try {
                    await missingDialog.retry();
                  } catch (e) {
                    setError(e instanceof Error ? e.message : t('common.error'));
                  }
                }}
              >
                {t('inventory.skipAndContinue')}
              </Button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
