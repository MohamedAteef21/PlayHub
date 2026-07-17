import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { alertsApi, assetsApi, branchesApi, cafeteriaApi, customersApi, pricingApi, sessionsApi, uploadsApi, whatsappApi } from '@/api/client';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card, CardHeader, CardTitle } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { formatCurrency, formatDuration, parseServerUtc, useLiveTimer, useSessionHub } from '@/hooks/useSessions';
import { playTimeUpSound } from '@/lib/timeUpSound';
import { formatStockDisplay, hasLargeUnit, maxSellQuantity } from '@/lib/itemUnits';
import { hasPermission, Permissions } from '@/lib/permissions';
import { printSessionInvoice } from '@/lib/printSessionInvoice';
import { useAuthStore, useUiStore } from '@/store';
import type { AssetDashboardDevice, CafeteriaItem, Customer, PricingPlan, SessionDetail, SessionLive } from '@/types';
import { InventoryUnitKind, PaymentMethod, SessionMode, SessionStatus, TimeUnit, WatchingBilling, PaymentAccountType } from '@/types';

type GuestType = 'none' | 'registered' | 'quick';

function statusKey(status: string) {
  return status.toLowerCase() as 'idle' | 'gaming' | 'watching' | 'paused';
}

function DeviceCard({
  device,
  roomName,
  session,
  onOpen,
  onPause,
  onResume,
  onClose,
  onConvert,
  onExtend,
  onWatchersChange,
  onAddCafeteria,
  canAddCafeteria,
}: {
  device: AssetDashboardDevice;
  roomName: string;
  session?: SessionLive;
  onOpen: () => void;
  onPause: () => void;
  onResume: () => void;
  onClose: () => void;
  onConvert: () => void;
  onExtend: (additionalMinutes: number | null) => void;
  onWatchersChange: (watcherCount: number) => void;
  onAddCafeteria: () => void;
  canAddCafeteria: boolean;
}) {
  const { t } = useTranslation();
  const elapsed = useLiveTimer(session ?? null);
  const isActive = !!session && session.status !== SessionStatus.Closed;
  const liveStatus = session?.status === SessionStatus.Paused ? 'Paused' : device.liveStatus;

  return (
    <Card
      className={`relative overflow-hidden transition-all ${isActive ? 'border-primary/50 shadow-lg shadow-primary/10' : ''}`}
    >
      {isActive && (
        <div className="absolute inset-x-0 top-0 h-0.5 bg-gradient-to-r from-primary to-accent" />
      )}
      <CardHeader>
        <div>
          <CardTitle className="text-base">{device.name}</CardTitle>
          <p className="text-xs text-muted">{roomName}</p>
        </div>
        <Badge status={statusKey(liveStatus)} pulse={liveStatus === 'Gaming' || liveStatus === 'Watching'}>
          {t(`dashboard.${statusKey(liveStatus)}`, { defaultValue: liveStatus })}
        </Badge>
      </CardHeader>

      {isActive && session ? (
        <div className="space-y-3">
          <div className="flex items-baseline justify-between">
            <span className="font-mono text-3xl font-bold tracking-tight text-accent">
              {formatDuration(elapsed)}
            </span>
            <span className="text-lg font-semibold text-success">
              {formatCurrency(session.totalCost)}
            </span>
          </div>
          {session.plannedDurationMinutes != null && (
            <div className={`rounded-lg px-2 py-1.5 text-xs ${session.timeExpired || (session.plannedDurationMinutes * 60 - elapsed) <= 0 ? 'animate-pulse bg-danger/15 font-semibold text-danger' : 'bg-primary/10 text-primary'}`}>
              {session.timeExpired || elapsed >= session.plannedDurationMinutes * 60
                ? t('dashboard.timeUp')
                : t('dashboard.remaining', {
                    time: formatDuration(Math.max(0, session.plannedDurationMinutes * 60 - elapsed)),
                  })}
              {' · '}
              {t('dashboard.bookedFor', { hours: (session.plannedDurationMinutes / 60).toFixed(session.plannedDurationMinutes % 60 === 0 ? 0 : 1) })}
            </div>
          )}
          {session.plannedDurationMinutes != null && session.status === SessionStatus.Open && (
            <div className="flex gap-1">
              <Button variant="secondary" size="sm" className="flex-1" onClick={() => onExtend(30)}>
                {t('dashboard.extendHalfHour')}
              </Button>
              <Button variant="secondary" size="sm" className="flex-1" onClick={() => onExtend(60)}>
                {t('dashboard.extendHour')}
              </Button>
              <Button variant="secondary" size="sm" className="flex-1" onClick={() => onExtend(null)}>
                {t('dashboard.makeOpenTime')}
              </Button>
            </div>
          )}
          {session.sessionMode === SessionMode.Gaming ? (
            <p className="text-xs text-muted">
              {session.controllerCount} {t('dashboard.controllers')}
              {' · '}{session.pricingPlanName}
              {session.plannedDurationMinutes == null ? ` · ${t('dashboard.openTimer')}` : ''}
            </p>
          ) : (
            <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted">
              <span>{t('dashboard.watchers')}:</span>
              <button
                type="button"
                className="flex h-6 w-6 items-center justify-center rounded-md border border-border bg-surface font-bold hover:bg-surface-hover disabled:opacity-40"
                disabled={(session.watcherCount ?? 1) <= 1}
                onClick={() => onWatchersChange((session.watcherCount ?? 2) - 1)}
              >
                −
              </button>
              <span className="min-w-5 text-center text-sm font-semibold text-text">{session.watcherCount}</span>
              <button
                type="button"
                className="flex h-6 w-6 items-center justify-center rounded-md border border-border bg-surface font-bold hover:bg-surface-hover disabled:opacity-40"
                disabled={(session.watcherCount ?? 0) >= device.maxWatchingCapacity}
                onClick={() => onWatchersChange((session.watcherCount ?? 0) + 1)}
              >
                +
              </button>
              <span>
                · {session.pricingPlanName}
                {session.plannedDurationMinutes == null ? ` · ${t('dashboard.openTimer')}` : ''}
              </span>
            </div>
          )}
          {(session.cafeteriaCost > 0 || session.currentTimeCost > 0) && (
            <p className="text-xs text-muted">
              {t('dashboard.timeCost')}: {formatCurrency(session.currentTimeCost)}
              {session.accruedTimeCost > 0
                ? ` (${t('dashboard.accrued')}: ${formatCurrency(session.accruedTimeCost)})`
                : ''}
              {' · '}
              {t('dashboard.cafeteriaCost')}: {formatCurrency(session.cafeteriaCost)}
            </p>
          )}
          <div className="flex gap-2">
            {session.status === SessionStatus.Open ? (
              <Button variant="secondary" size="sm" className="flex-1" onClick={onPause}>{t('session.pause')}</Button>
            ) : (
              <Button variant="secondary" size="sm" className="flex-1" onClick={onResume}>{t('session.resume')}</Button>
            )}
            <Button variant="danger" size="sm" className="flex-1" onClick={onClose}>{t('session.close')}</Button>
          </div>
          {session.canConvertToGaming && (
            <Button variant="secondary" size="sm" className="w-full" onClick={onConvert}>
              <Icon name="play" className="h-3.5 w-3.5" />
              {t('dashboard.convertToGaming')}
            </Button>
          )}
          {canAddCafeteria && (
            <Button variant="primary" size="sm" className="w-full" onClick={onAddCafeteria}>
              <Icon name="cafeteria" className="h-3.5 w-3.5" />
              {t('dashboard.addCafeteria')}
              {session.cafeteriaCost > 0 ? ` · ${formatCurrency(session.cafeteriaCost)}` : ''}
            </Button>
          )}
        </div>
      ) : (
        <div className="flex items-center justify-between pt-1">
          <span className="text-sm text-muted">
            {device.workingControllers} ctrl · {device.maxWatchingCapacity} watch
          </span>
          <Button size="sm" onClick={onOpen}>
            <Icon name="play" className="h-3.5 w-3.5" />
            {t('dashboard.openSession')}
          </Button>
        </div>
      )}
    </Card>
  );
}

export function DashboardPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [sessions, setSessions] = useState<SessionLive[]>([]);
  const [openModal, setOpenModal] = useState<AssetDashboardDevice | null>(null);
  const [closeModal, setCloseModal] = useState<SessionLive | null>(null);
  const [convertModal, setConvertModal] = useState<SessionLive | null>(null);
  const [convertPlanId, setConvertPlanId] = useState('');
  const [convertControllers, setConvertControllers] = useState(2);
  const [convertError, setConvertError] = useState('');
  const [invoiceResult, setInvoiceResult] = useState<SessionDetail | null>(null);
  const [cafSession, setCafSession] = useState<SessionLive | null>(null);
  const [cafQty, setCafQty] = useState<Record<string, number>>({});
  const [cafUnit, setCafUnit] = useState<Record<string, InventoryUnitKind>>({});
  const [cafCustomerName, setCafCustomerName] = useState('');
  const [cafSearch, setCafSearch] = useState('');
  const [returnLineId, setReturnLineId] = useState('');
  const [returnQty, setReturnQty] = useState('1');
  const [returnReason, setReturnReason] = useState('');
  const [cafError, setCafError] = useState('');
  const [openError, setOpenError] = useState('');
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);
  const language = useUiStore((s) => s.language);
  const canReturn = hasPermission(user, Permissions.CafeteriaReturn);
  const canSellCafeteria = hasPermission(user, Permissions.CafeteriaSell) || !!user?.isMaster;
  const [mode, setMode] = useState<number>(SessionMode.Gaming);
  const [planId, setPlanId] = useState('');
  const [bookingMode, setBookingMode] = useState<'open' | 'fixed'>('open');
  const [durationHours, setDurationHours] = useState(2);
  const [controllerCount, setControllerCount] = useState(2);
  const [watcherCount, setWatcherCount] = useState(2);
  const [debtorName, setDebtorName] = useState('');
  const [discountAmount, setDiscountAmount] = useState('');
  const [discountReason, setDiscountReason] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<number>(PaymentMethod.Cash);
  const [walletPayAmount, setWalletPayAmount] = useState('');
  const [proofFile, setProofFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [guestType, setGuestType] = useState<GuestType>('none');
  const [customerSearch, setCustomerSearch] = useState('');
  const [debouncedCustomerQ, setDebouncedCustomerQ] = useState('');
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [quickGuestName, setQuickGuestName] = useState('');
  const [waInvoiceMsg, setWaInvoiceMsg] = useState('');
  const [waInvoiceError, setWaInvoiceError] = useState('');
  const [waInvoiceLoading, setWaInvoiceLoading] = useState(false);
  const [pdfLoading, setPdfLoading] = useState(false);
  const canSendWhatsApp = hasPermission(user, Permissions.CustomersManage);

  useEffect(() => {
    const id = window.setTimeout(() => setDebouncedCustomerQ(customerSearch.trim()), 300);
    return () => window.clearTimeout(id);
  }, [customerSearch]);

  const { data: dashboard, isLoading } = useQuery({
    queryKey: ['dashboard', user?.id, activeBranchId],
    queryFn: assetsApi.getDashboard,
    refetchInterval: 30000,
    meta: { silent: true },
  });

  const { data: closeBranch } = useQuery({
    queryKey: ['branch', activeBranchId],
    queryFn: () => branchesApi.getById(activeBranchId!),
    enabled: !!activeBranchId && !!closeModal,
  });

  const { data: closeCustomer } = useQuery({
    queryKey: ['customer', closeModal?.customerId],
    queryFn: () => customersApi.getById(closeModal!.customerId!),
    enabled: !!closeModal?.customerId,
  });

  function transferAccountsFor(method: number) {
    const accounts = closeBranch?.paymentAccounts ?? [];
    if (method === PaymentMethod.BankTransfer) {
      return accounts.filter((a) => a.accountType === PaymentAccountType.BankTransfer);
    }
    if (method === PaymentMethod.DigitalWallet) {
      return accounts.filter((a) => a.accountType === PaymentAccountType.DigitalWallet);
    }
    return [];
  }

  const { data: activeSessions } = useQuery({
    queryKey: ['sessions', 'active'],
    queryFn: sessionsApi.getActive,
    refetchInterval: 10000,
    meta: { silent: true },
  });

  const { data: gamingPlans } = useQuery({
    queryKey: ['plans', SessionMode.Gaming],
    queryFn: () => pricingApi.getPlans(SessionMode.Gaming),
    enabled: !!openModal,
  });

  const { data: watchingPlans } = useQuery({
    queryKey: ['plans', SessionMode.Watching],
    queryFn: () => pricingApi.getPlans(SessionMode.Watching),
    enabled: !!openModal,
  });

  const { data: customerSearchResults } = useQuery({
    queryKey: ['customers', 'open-session', debouncedCustomerQ],
    queryFn: () => customersApi.getAll(debouncedCustomerQ || undefined, 1, 10),
    enabled: !!openModal && guestType === 'registered',
  });

  const { data: cafItems = [] } = useQuery({
    queryKey: ['cafeteria-items'],
    queryFn: cafeteriaApi.getItems,
    enabled: !!cafSession,
  });

  useEffect(() => {
    if (activeSessions) setSessions(activeSessions);
  }, [activeSessions]);

  useEffect(() => {
    if (!cafSession) return;
    const live = sessions.find((s) => s.id === cafSession.id);
    if (!live) {
      setCafSession(null);
      return;
    }
    if (live !== cafSession) setCafSession(live);
  }, [sessions, cafSession]);

  const onUpdate = useCallback((session: SessionLive) => {
    setSessions((prev) => {
      const idx = prev.findIndex((s) => s.id === session.id);
      if (idx >= 0) {
        const next = [...prev];
        next[idx] = session;
        return next;
      }
      return [...prev, session];
    });
    queryClient.invalidateQueries({ queryKey: ['dashboard'] });
  }, [queryClient]);

  // Sound the alarm once when a booked session's time runs out (re-arms if time is extended).
  const timeUpAlertedRef = useRef<Set<string>>(new Set());
  useEffect(() => {
    const check = () => {
      for (const s of sessions) {
        if (s.status !== SessionStatus.Open || s.plannedDurationMinutes == null) continue;
        const elapsed = Math.floor((Date.now() - parseServerUtc(s.startedAt)) / 1000) - s.totalPausedSeconds;
        const expired = elapsed >= s.plannedDurationMinutes * 60;
        if (expired && !timeUpAlertedRef.current.has(s.id)) {
          timeUpAlertedRef.current.add(s.id);
          playTimeUpSound();
        } else if (!expired && timeUpAlertedRef.current.has(s.id)) {
          timeUpAlertedRef.current.delete(s.id);
        }
      }
    };
    check();
    const id = setInterval(check, 1000);
    return () => clearInterval(id);
  }, [sessions]);

  const onClosed = useCallback((sessionId: string) => {
    setSessions((prev) => prev.filter((s) => s.id !== sessionId));
    queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    queryClient.invalidateQueries({ queryKey: ['sessions'] });
  }, [queryClient]);

  useSessionHub(onUpdate, onClosed);

  const sessionMap = new Map(sessions.map((s) => [s.deviceId, s]));
  const plans = mode === SessionMode.Gaming ? gamingPlans : watchingPlans;
  const selectedPlan = plans?.find((p) => p.id === planId);
  const isPerGamePlan = selectedPlan?.timeUnit === TimeUnit.PerGame;
  const isFlatWatchingPlan =
    selectedPlan?.sessionMode === SessionMode.Watching &&
    selectedPlan.watchingBilling === WatchingBilling.PerPerson;
  const hideTimerOptions = isPerGamePlan || isFlatWatchingPlan;

  function planOptionLabel(p: PricingPlan) {
    const unit =
      p.sessionMode === SessionMode.Watching && p.watchingBilling === WatchingBilling.PerPerson
        ? t('settings.flatSession')
        : p.timeUnit === TimeUnit.PerGame
          ? t('settings.perGame')
          : p.timeUnit === TimeUnit.PerMinute
            ? t('settings.perMinute')
            : t('settings.perHour');
    const watching =
      p.sessionMode === SessionMode.Watching
        ? ` · ${p.watchingBilling === WatchingBilling.PerScreen ? t('settings.perScreen') : t('settings.perPerson')}`
        : '';
    const rate =
      p.sessionMode === SessionMode.Gaming
        ? p.gamingRates[0]?.rate
        : p.watchingRates[0]?.ratePerPerson;
    const pkg =
      p.packagePrice != null && p.packageDurationMinutes != null
        ? ` · ${t('settings.packageBadge')} ${Math.round((p.packageDurationMinutes / 60) * 10) / 10}${t('session.hoursShort')} = ${formatCurrency(p.packagePrice)}`
        : '';
    return `${p.name} (${unit}${watching}${rate != null ? ` · ${formatCurrency(rate)}` : ''}${pkg})`;
  }

  async function handleOpen() {
    if (!openModal || !planId) return;
    setLoading(true);
    setOpenError('');
    try {
      const plan = plans?.find((p) => p.id === planId);
      const useFixed =
        bookingMode === 'fixed' &&
        plan?.timeUnit !== TimeUnit.PerGame &&
        !(plan?.sessionMode === SessionMode.Watching && plan.watchingBilling === WatchingBilling.PerPerson);
      await sessionsApi.open({
        deviceId: openModal.id,
        pricingPlanId: planId,
        sessionMode: mode,
        controllerCount: mode === SessionMode.Gaming ? controllerCount : undefined,
        watcherCount: mode === SessionMode.Watching ? watcherCount : undefined,
        plannedDurationMinutes:
          useFixed ? Math.round(durationHours * 60) : undefined,
        customerId: guestType === 'registered' ? selectedCustomer?.id : undefined,
        isQuickGuest: guestType === 'quick',
        quickGuestName:
          guestType === 'quick' ? quickGuestName.trim() || undefined : undefined,
      });
      setOpenModal(null);
      setBookingMode('open');
      setDurationHours(2);
      setGuestType('none');
      setCustomerSearch('');
      setSelectedCustomer(null);
      setQuickGuestName('');
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    } catch (err) {
      setOpenError(err instanceof Error ? err.message : t('common.error'));
      // Refresh floor state — the device may actually be occupied already.
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    } finally {
      setLoading(false);
    }
  }

  async function handleClose() {
    if (!closeModal) return;
    const discount = Number(discountAmount) || 0;
    if (discount < 0) {
      setCafError(t('session.discountInvalid'));
      return;
    }
    if (discount > closeModal.totalCost) {
      setCafError(t('session.discountTooHigh'));
      return;
    }
    const payable = Math.max(0, closeModal.totalCost - discount);
    const walletPart = Number(walletPayAmount) || 0;
    if (walletPart > 0) {
      if (walletPart > (closeCustomer?.walletBalance ?? 0)) {
        setCafError(t('session.walletExceedsBalance'));
        return;
      }
      if (walletPart > payable) {
        setCafError(t('session.walletExceedsBill'));
        return;
      }
    }
    setLoading(true);
    setCafError('');
    try {
      let proofFileUrl: string | undefined;
      if (
        (paymentMethod === PaymentMethod.BankTransfer || paymentMethod === PaymentMethod.DigitalWallet) &&
        proofFile
      ) {
        const uploaded = await uploadsApi.paymentProof(proofFile);
        proofFileUrl = uploaded.url;
      }

      const detail = await sessionsApi.close(closeModal.id, {
        payment: {
          paymentMethod,
          debtorName: paymentMethod === PaymentMethod.Deferred ? debtorName : undefined,
          proofFileUrl,
          walletAmount: walletPart > 0 ? walletPart : undefined,
        },
        discountAmount: discount > 0 ? discount : 0,
        discountReason: discount > 0 ? discountReason.trim() || undefined : undefined,
      });
      setCloseModal(null);
      setDebtorName('');
      setDiscountAmount('');
      setDiscountReason('');
      setPaymentMethod(PaymentMethod.Cash);
      setWalletPayAmount('');
      setProofFile(null);
      setWaInvoiceMsg('');
      setWaInvoiceError('');
      setInvoiceResult(detail);
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    } catch (e) {
      setCafError(e instanceof Error ? e.message : t('common.error'));
    } finally {
      setLoading(false);
    }
  }

  function handlePrintInvoice() {
    if (!invoiceResult) return;
    const branchName =
      dashboard?.branchName ||
      user?.branches.find((b) => b.id === activeBranchId)?.name ||
      t('session.branch');
    printSessionInvoice(
      invoiceResult,
      branchName,
      {
        title: t('session.invoiceTitle'),
        branch: t('session.branch'),
        invoiceNumber: t('session.invoiceNumber'),
        date: t('session.date'),
        device: t('session.device'),
        room: t('session.room'),
        mode: t('session.mode'),
        gaming: t('session.gaming'),
        watching: t('session.watching'),
        plan: t('session.pricingPlan'),
        started: t('session.started'),
        closed: t('session.closedAt'),
        timeCost: t('session.timeCost'),
        roomSurcharge: t('session.roomSurcharge'),
        cafeteria: t('session.cafeteria'),
        discount: t('session.discount'),
        customerWallet: t('session.customerWallet'),
        total: t('session.total'),
        payment: t('session.payment'),
        cash: t('session.cash'),
        deferred: t('session.deferred'),
        bankTransfer: t('session.bankTransfer'),
        digitalWallet: t('session.digitalWallet'),
        openedBy: t('session.openedBy'),
        closedBy: t('session.closedBy'),
        qty: t('session.qty'),
        item: t('session.item'),
        thankYou: t('session.thankYou'),
        print: t('session.printInvoice'),
      },
      language === 'ar' ? 'rtl' : 'ltr'
    );
  }

  async function handleSendInvoiceWhatsApp() {
    if (!invoiceResult) return;
    setWaInvoiceLoading(true);
    setWaInvoiceMsg('');
    setWaInvoiceError('');
    try {
      const res = await whatsappApi.sendInvoice(invoiceResult.id);
      if (res.success) {
        setWaInvoiceMsg(t('whatsapp.invoiceSent'));
      } else {
        setWaInvoiceError(res.error || t('common.error'));
      }
    } catch (e) {
      setWaInvoiceError(e instanceof Error ? e.message : t('common.error'));
    } finally {
      setWaInvoiceLoading(false);
    }
  }

  async function handleDownloadInvoicePdf() {
    if (!invoiceResult) return;
    setPdfLoading(true);
    setWaInvoiceError('');
    try {
      const blob = await alertsApi.downloadInvoicePdf(invoiceResult.id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${invoiceResult.invoice?.invoiceNumber ?? invoiceResult.invoiceNumber ?? 'invoice'}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setWaInvoiceError(e instanceof Error ? e.message : t('common.error'));
    } finally {
      setPdfLoading(false);
    }
  }

  async function handleAddCafeteriaToSession() {
    if (!cafSession) return;
    const lines = Object.entries(cafQty).filter(([, q]) => q > 0);
    if (lines.length === 0) {
      setCafError(t('cafeteria.emptyCart'));
      return;
    }
    setLoading(true);
    setCafError('');
    try {
      let last: SessionLive | null = null;
      for (const [itemId, quantity] of lines) {
        last = await sessionsApi.addCafeteria(
          cafSession.id,
          itemId,
          quantity,
          cafCustomerName.trim() || undefined,
          cafUnit[itemId] ?? InventoryUnitKind.Base
        );
      }
      if (last) onUpdate(last);
      setCafSession(null);
      setCafQty({});
      setCafUnit({});
      setCafCustomerName('');
      setCafSearch('');
      setReturnLineId('');
      setReturnQty('1');
      setReturnReason('');
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
    } catch (e) {
      setCafError(e instanceof Error ? e.message : t('common.error'));
    } finally {
      setLoading(false);
    }
  }

  async function handleReturnCafeteria() {
    if (!cafSession || !returnLineId) return;
    const qty = Number(returnQty);
    if (!qty || qty <= 0 || !returnReason.trim()) {
      setCafError(t('cafeteria.returnRequired'));
      return;
    }
    setLoading(true);
    setCafError('');
    try {
      const updated = await sessionsApi.returnCafeteria(
        cafSession.id,
        returnLineId,
        qty,
        returnReason.trim()
      );
      onUpdate(updated);
      setCafSession(updated);
      setReturnLineId('');
      setReturnQty('1');
      setReturnReason('');
      queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
    } catch (e) {
      setCafError(e instanceof Error ? e.message : t('common.error'));
    } finally {
      setLoading(false);
    }
  }

  if (isLoading) {
    return <div className="flex h-64 items-center justify-center text-muted">{t('common.loading')}</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('dashboard.title')}</h1>
          {dashboard && <p className="text-muted">{dashboard.branchName}</p>}
        </div>
        <div className="flex gap-2">
          {['idle', 'gaming', 'watching', 'paused'].map((s) => (
            <Badge key={s} status={s}>{t(`dashboard.${s}`)}</Badge>
          ))}
        </div>
      </div>

      {canSellCafeteria && sessions.length > 0 && (
        <Card className="space-y-3">
          <div>
            <p className="text-sm font-semibold">{t('dashboard.pickOpenSession')}</p>
            <p className="text-xs text-muted">{t('dashboard.addCafeteriaHint')}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            {sessions.map((s) => (
              <Button
                key={s.id}
                size="sm"
                variant="secondary"
                onClick={() => {
                  setCafSession(s);
                  setCafQty({});
                  setCafUnit({});
                  setCafCustomerName('');
                  setCafSearch('');
                  setReturnLineId('');
                  setReturnQty('1');
                  setReturnReason('');
                  setCafError('');
                }}
              >
                <Icon name="cafeteria" className="h-3.5 w-3.5" />
                {s.deviceName}
                {s.cafeteriaCost > 0 ? ` · ${formatCurrency(s.cafeteriaCost)}` : ''}
              </Button>
            ))}
          </div>
        </Card>
      )}

      {!dashboard?.rooms.length && !(dashboard?.unassignedDevices?.length) ? (
        <Card className="space-y-4 py-12 text-center">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/15 text-primary">
            <Icon name="clock" className="h-7 w-7" />
          </div>
          <p className="text-muted">{t('dashboard.noRooms')}</p>
          <p className="mx-auto max-w-md text-sm text-muted">{t('dashboard.setupHint')}</p>
          <Link to="/settings">
            <Button>
              <Icon name="settings" className="h-4 w-4" />
              {t('dashboard.goToSettings')}
            </Button>
          </Link>
        </Card>
      ) : (
        <>
        {dashboard.rooms.map((room) => (
          <section key={room.id}>
            <h2 className="mb-3 text-lg font-semibold text-muted">
              {room.name}{room.roomNumber ? ` · ${room.roomNumber}` : ''}
            </h2>
            {(room.assets?.length ?? 0) > 0 && (
              <p className="mb-3 text-xs text-muted">
                {room.assets.map((a) => `${a.assetTypeName}: ${a.workingCount}/${a.quantity}`).join(' · ')}
              </p>
            )}
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {room.devices.map((device) => {
                const session = sessionMap.get(device.id);
                return (
                  <DeviceCard
                    key={device.id}
                    device={device}
                    roomName={room.name}
                    session={session}
                    onOpen={() => {
                      setOpenModal(device);
                      setPlanId('');
                      setBookingMode('open');
                      setDurationHours(2);
                      setControllerCount(2);
                      setOpenError('');
                    }}
                    onPause={() => session && sessionsApi.pause(session.id).then(onUpdate)}
                    onResume={() => session && sessionsApi.resume(session.id).then(onUpdate)}
                    onExtend={(mins) => session && sessionsApi.extend(session.id, mins).then(onUpdate)}
                    onWatchersChange={(count) =>
                      session &&
                      sessionsApi
                        .updateWatchers(session.id, count)
                        .then(onUpdate)
                        .catch((e: Error) => window.alert(e.message))
                    }
                    onConvert={() => {
                      if (!session) return;
                      setConvertModal(session);
                      setConvertPlanId('');
                      setConvertControllers(2);
                      setConvertError('');
                    }}
                    onClose={() => {
                      if (!session) return;
                      setCloseModal(session);
                      setDiscountAmount('');
                      setDiscountReason('');
                      setDebtorName('');
                      setPaymentMethod(PaymentMethod.Cash);
                      setWalletPayAmount('');
                      setProofFile(null);
                      setCafError('');
                    }}
                    onAddCafeteria={() => {
                      if (!session) return;
                      setCafSession(session);
                      setCafQty({});
                      setCafUnit({});
                      setCafCustomerName('');
                      setCafSearch('');
                      setReturnLineId('');
                      setReturnQty('1');
                      setReturnReason('');
                      setCafError('');
                    }}
                    canAddCafeteria={canSellCafeteria}
                  />
                );
              })}
            </div>
          </section>
        ))}
        {(dashboard.unassignedDevices?.length ?? 0) > 0 && (
          <section>
            <h2 className="mb-3 text-lg font-semibold text-muted">{t('dashboard.unassignedDevices')}</h2>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {dashboard.unassignedDevices.map((device) => {
                const session = sessionMap.get(device.id);
                return (
                  <DeviceCard
                    key={device.id}
                    device={device}
                    roomName={t('settings.noRoom')}
                    session={session}
                    onOpen={() => {
                      setOpenModal(device);
                      setPlanId('');
                      setBookingMode('open');
                      setDurationHours(2);
                      setControllerCount(2);
                      setOpenError('');
                    }}
                    onPause={() => session && sessionsApi.pause(session.id).then(onUpdate)}
                    onResume={() => session && sessionsApi.resume(session.id).then(onUpdate)}
                    onExtend={(mins) => session && sessionsApi.extend(session.id, mins).then(onUpdate)}
                    onWatchersChange={(count) =>
                      session &&
                      sessionsApi
                        .updateWatchers(session.id, count)
                        .then(onUpdate)
                        .catch((e: Error) => window.alert(e.message))
                    }
                    onConvert={() => {
                      if (!session) return;
                      setConvertModal(session);
                      setConvertPlanId('');
                      setConvertControllers(2);
                      setConvertError('');
                    }}
                    onClose={() => {
                      if (!session) return;
                      setCloseModal(session);
                      setDiscountAmount('');
                      setDiscountReason('');
                      setDebtorName('');
                      setPaymentMethod(PaymentMethod.Cash);
                      setWalletPayAmount('');
                      setProofFile(null);
                      setCafError('');
                    }}
                    onAddCafeteria={() => {
                      if (!session) return;
                      setCafSession(session);
                      setCafQty({});
                      setCafUnit({});
                      setCafCustomerName('');
                      setCafSearch('');
                      setReturnLineId('');
                      setReturnQty('1');
                      setReturnReason('');
                      setCafError('');
                    }}
                    canAddCafeteria={canSellCafeteria}
                  />
                );
              })}
            </div>
          </section>
        )}
        </>
      )}

      <Modal
        open={!!invoiceResult}
        onClose={() => {
          setInvoiceResult(null);
          setWaInvoiceMsg('');
          setWaInvoiceError('');
        }}
        title={t('session.closedSuccess')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setInvoiceResult(null);
                setWaInvoiceMsg('');
                setWaInvoiceError('');
              }}
            >
              {t('session.done')}
            </Button>
            {canSendWhatsApp &&
              invoiceResult?.customerId &&
              invoiceResult.customerPhone && (
                <Button
                  variant="secondary"
                  loading={waInvoiceLoading}
                  onClick={handleSendInvoiceWhatsApp}
                >
                  {t('whatsapp.sendInvoice')}
                </Button>
              )}
            <Button variant="secondary" loading={pdfLoading} onClick={handleDownloadInvoicePdf}>
              <Icon name="download" className="h-4 w-4" />
              {t('session.downloadPdf')}
            </Button>
            <Button onClick={handlePrintInvoice}>
              <Icon name="print" className="h-4 w-4" />
              {t('session.printInvoice')}
            </Button>
          </>
        }
      >
        {invoiceResult && (
          <div className="space-y-3">
            <p className="text-sm text-muted">{t('session.invoiceReady')}</p>
            <div className="rounded-xl border border-border bg-surface p-3 space-y-1.5">
              <div className="flex justify-between text-sm">
                <span className="text-muted">{t('session.invoiceNumber')}</span>
                <span className="font-semibold">
                  {invoiceResult.invoice?.invoiceNumber ?? invoiceResult.invoiceNumber ?? '—'}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted">{t('session.device')}</span>
                <span>{invoiceResult.deviceName}</span>
              </div>
              {(invoiceResult.customerName || invoiceResult.quickGuestName) && (
                <div className="flex justify-between text-sm">
                  <span className="text-muted">{t('session.guest')}</span>
                  <span>
                    {invoiceResult.customerName ||
                      invoiceResult.quickGuestName ||
                      '—'}
                  </span>
                </div>
              )}
              <div className="flex justify-between text-sm">
                <span className="text-muted">{t('session.timeCost')}</span>
                <span>{formatCurrency(invoiceResult.timeCost)}</span>
              </div>
              {invoiceResult.roomSurchargeCost > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-muted">{t('session.roomSurcharge')}</span>
                  <span>{formatCurrency(invoiceResult.roomSurchargeCost)}</span>
                </div>
              )}
              <div className="flex justify-between text-sm">
                <span className="text-muted">{t('session.cafeteria')}</span>
                <span>{formatCurrency(invoiceResult.cafeteriaCost)}</span>
              </div>
              {invoiceResult.discountAmount > 0 && (
                <div className="flex justify-between text-sm text-danger">
                  <span>{t('session.discount')}</span>
                  <span>-{formatCurrency(invoiceResult.discountAmount)}</span>
                </div>
              )}
              <div className="flex justify-between border-t border-border pt-2 text-lg font-bold text-success">
                <span>{t('session.total')}</span>
                <span>{formatCurrency(invoiceResult.totalCost)}</span>
              </div>
            </div>
            {waInvoiceMsg && <p className="text-sm text-success">{waInvoiceMsg}</p>}
            {waInvoiceError && <p className="text-sm text-danger">{waInvoiceError}</p>}
          </div>
        )}
      </Modal>

      <Modal
        open={!!convertModal}
        onClose={() => setConvertModal(null)}
        title={t('dashboard.convertToGaming')}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConvertModal(null)}>{t('session.cancel')}</Button>
            <Button
              loading={loading}
              disabled={!convertPlanId}
              onClick={async () => {
                if (!convertModal || !convertPlanId) return;
                setLoading(true);
                setConvertError('');
                try {
                  const updated = await sessionsApi.convert(convertModal.id, {
                    pricingPlanId: convertPlanId,
                    controllerCount: convertControllers,
                  });
                  onUpdate(updated);
                  setConvertModal(null);
                } catch (e) {
                  setConvertError(e instanceof Error ? e.message : String(e));
                } finally {
                  setLoading(false);
                }
              }}
            >
              {t('dashboard.confirmConvert')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <p className="text-sm text-muted">{t('dashboard.convertHint')}</p>
          {convertModal && convertModal.accruedTimeCost == null && (
            <p className="text-xs text-muted">
              {t('dashboard.convertWatchingNote', {
                count: convertModal.watcherCount ?? 0,
                cost: formatCurrency(convertModal.currentTimeCost),
              })}
            </p>
          )}
          <div>
            <label className="mb-1 block text-sm text-muted">{t('session.pricingPlan')}</label>
            <select
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm"
              value={convertPlanId}
              onChange={(e) => setConvertPlanId(e.target.value)}
            >
              <option value="">{t('session.pricingPlan')}</option>
              {(gamingPlans ?? []).map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                  {p.gamingRates[0] ? ` · ${formatCurrency(p.gamingRates[0].rate)}` : ''}
                </option>
              ))}
            </select>
          </div>
          <div>
            <p className="mb-2 text-sm text-muted">{t('dashboard.playMode')}</p>
            <div className="flex gap-2">
              <Button
                size="sm"
                variant={convertControllers <= 2 ? 'primary' : 'secondary'}
                onClick={() => setConvertControllers(2)}
              >
                {t('settings.individual')}
                {(() => {
                  const plan = (gamingPlans ?? []).find((p) => p.id === convertPlanId);
                  const rate = plan?.gamingRates.find((r) => r.controllerCount === 1)?.rate;
                  return rate != null ? ` · ${formatCurrency(rate)}` : '';
                })()}
              </Button>
              <Button
                size="sm"
                variant={convertControllers >= 3 ? 'primary' : 'secondary'}
                onClick={() => setConvertControllers(4)}
              >
                {t('settings.couple')}
                {(() => {
                  const plan = (gamingPlans ?? []).find((p) => p.id === convertPlanId);
                  const rate = plan?.gamingRates.find((r) => r.controllerCount === 2)?.rate;
                  return rate != null ? ` · ${formatCurrency(rate)}` : '';
                })()}
              </Button>
            </div>
            <p className="mt-2 mb-1 text-xs text-muted">{t('dashboard.controllers')}</p>
            <div className="flex gap-2">
              {(convertControllers <= 2 ? [1, 2] : [3, 4]).map((n) => (
                <Button
                  key={n}
                  size="sm"
                  variant={convertControllers === n ? 'primary' : 'secondary'}
                  onClick={() => setConvertControllers(n)}
                >
                  {n}
                </Button>
              ))}
            </div>
          </div>
          {convertError && <p className="text-sm text-danger">{convertError}</p>}
        </div>
      </Modal>

      <Modal
        open={!!openModal}
        onClose={() => {
          setOpenModal(null);
          setGuestType('none');
          setCustomerSearch('');
          setSelectedCustomer(null);
          setQuickGuestName('');
          setOpenError('');
        }}
        title={t('session.open')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setOpenModal(null);
                setGuestType('none');
                setCustomerSearch('');
                setSelectedCustomer(null);
                setQuickGuestName('');
              }}
            >
              {t('session.cancel')}
            </Button>
            <Button
              loading={loading}
              onClick={handleOpen}
              disabled={guestType === 'registered' && !selectedCustomer}
            >
              {t('session.confirm')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <div className="flex gap-2">
            {[SessionMode.Gaming, SessionMode.Watching].map((m) => (
              <Button key={m} variant={mode === m ? 'primary' : 'secondary'} size="sm"
                onClick={() => { setMode(m); setPlanId(''); }}>
                {m === SessionMode.Gaming ? t('session.gaming') : t('session.watching')}
              </Button>
            ))}
          </div>
          <div>
            <label className="mb-1 block text-sm text-muted">{t('session.guestType')}</label>
            <div className="flex flex-wrap gap-2">
              {(
                [
                  ['none', t('session.guestNone')],
                  ['registered', t('session.guestRegistered')],
                  ['quick', t('session.guestQuick')],
                ] as const
              ).map(([value, label]) => (
                <Button
                  key={value}
                  size="sm"
                  variant={guestType === value ? 'primary' : 'secondary'}
                  onClick={() => {
                    setGuestType(value);
                    if (value !== 'registered') {
                      setSelectedCustomer(null);
                      setCustomerSearch('');
                    }
                    if (value !== 'quick') setQuickGuestName('');
                  }}
                >
                  {label}
                </Button>
              ))}
            </div>
          </div>
          {guestType === 'registered' && (
            <div className="space-y-2">
              <Input
                label={t('session.searchCustomer')}
                value={customerSearch}
                onChange={(e) => {
                  setCustomerSearch(e.target.value);
                  setSelectedCustomer(null);
                }}
                placeholder={t('session.searchCustomerPlaceholder')}
              />
              {selectedCustomer ? (
                <div className="rounded-lg border border-primary/30 bg-primary/10 px-3 py-2 text-sm">
                  <span className="font-medium">{selectedCustomer.name}</span>
                  <span className="ms-2 text-muted" dir="ltr">
                    {selectedCustomer.phone}
                  </span>
                  <span className="ms-2 font-mono text-xs text-muted">{selectedCustomer.code}</span>
                </div>
              ) : (
                <div className="max-h-40 overflow-y-auto rounded-lg border border-border">
                  {(customerSearchResults?.items ?? []).length === 0 ? (
                    <p className="px-3 py-2 text-sm text-muted">{t('session.noCustomersFound')}</p>
                  ) : (
                    (customerSearchResults?.items ?? []).map((c) => (
                      <button
                        key={c.id}
                        type="button"
                        className="flex w-full items-center justify-between gap-2 border-b border-border px-3 py-2 text-start text-sm last:border-0 hover:bg-surface-hover"
                        onClick={() => {
                          setSelectedCustomer(c);
                          setCustomerSearch(c.name);
                        }}
                      >
                        <span className="font-medium">{c.name}</span>
                        <span className="text-muted" dir="ltr">
                          {c.phone}
                        </span>
                      </button>
                    ))
                  )}
                </div>
              )}
            </div>
          )}
          {guestType === 'quick' && (
            <Input
              label={t('session.quickGuestName')}
              value={quickGuestName}
              onChange={(e) => setQuickGuestName(e.target.value)}
              placeholder={t('session.quickGuestNameOptional')}
            />
          )}
          <div>
            <label className="mb-1 block text-sm text-muted">{t('session.pricingPlan')}</label>
            <select
              className="w-full rounded-lg border border-border bg-surface px-3 py-2"
              value={planId}
              onChange={(e) => {
                const nextId = e.target.value;
                setPlanId(nextId);
                const plan = plans?.find((p) => p.id === nextId);
                if (plan?.timeUnit === TimeUnit.PerGame) setBookingMode('open');
                if (
                  plan?.sessionMode === SessionMode.Watching &&
                  plan.watchingBilling === WatchingBilling.PerPerson
                ) {
                  setBookingMode('open');
                }
              }}
            >
              <option value="">—</option>
              {plans?.map((p: PricingPlan) => (
                <option key={p.id} value={p.id}>{planOptionLabel(p)}</option>
              ))}
            </select>
          </div>
          {!hideTimerOptions && (
          <div className="flex gap-2">
            <Button
              variant={bookingMode === 'open' ? 'primary' : 'secondary'}
              size="sm"
              onClick={() => setBookingMode('open')}
            >
              {t('session.openTimer')}
            </Button>
            <Button
              variant={bookingMode === 'fixed' ? 'primary' : 'secondary'}
              size="sm"
              onClick={() => setBookingMode('fixed')}
            >
              {t('session.fixedBooking')}
            </Button>
          </div>
          )}
          {isPerGamePlan && (
            <p className="text-xs text-muted">{t('settings.perGame')}</p>
          )}
          {isFlatWatchingPlan && (
            <p className="rounded-lg bg-primary/10 px-3 py-2 text-xs text-primary">
              {t('settings.watchingFlatHint')}
            </p>
          )}
          {selectedPlan?.sessionMode === SessionMode.Watching &&
            selectedPlan.watchingBilling === WatchingBilling.PerScreen &&
            bookingMode === 'open' && (
              <p className="rounded-lg bg-primary/10 px-3 py-2 text-xs text-primary">
                {t('settings.watchingOpenTimerHint')}
              </p>
            )}
          {!hideTimerOptions && bookingMode === 'fixed' && (
            <div className="space-y-2">
              <Input
                label={t('session.durationHours')}
                type="number"
                min={0.5}
                step={0.5}
                value={durationHours}
                onChange={(e) => setDurationHours(Math.max(0.5, Number(e.target.value) || 0.5))}
              />
              <div className="flex flex-wrap gap-2">
                {[1, 2, 3, 4].map((h) => (
                  <Button
                    key={h}
                    size="sm"
                    variant={durationHours === h ? 'primary' : 'secondary'}
                    onClick={() => setDurationHours(h)}
                  >
                    {h}{t('session.hoursShort')}
                  </Button>
                ))}
              </div>
              <p className="text-xs text-muted">{t('session.fixedBookingHint')}</p>
            </div>
          )}
          {mode === SessionMode.Gaming ? (
            <div>
              <p className="mb-2 text-sm text-muted">{t('dashboard.playMode')}</p>
              <div className="flex flex-wrap gap-2">
                <Button
                  size="sm"
                  variant={controllerCount <= 2 ? 'primary' : 'secondary'}
                  onClick={() => setControllerCount(2)}
                >
                  {t('settings.individual')}
                </Button>
                <Button
                  size="sm"
                  variant={controllerCount >= 3 ? 'primary' : 'secondary'}
                  onClick={() => setControllerCount(4)}
                >
                  {t('settings.couple')}
                </Button>
              </div>
              <p className="mt-2 mb-1 text-xs text-muted">{t('dashboard.controllers')}</p>
              <div className="flex gap-2">
                {(controllerCount <= 2 ? [1, 2] : [3, 4]).map((n) => (
                  <Button
                    key={n}
                    size="sm"
                    variant={controllerCount === n ? 'primary' : 'secondary'}
                    onClick={() => setControllerCount(n)}
                  >
                    {n}
                  </Button>
                ))}
              </div>
            </div>
          ) : (
            <Input
              label={t('dashboard.watchers')}
              type="number" min={1}
              value={watcherCount}
              onChange={(e) => setWatcherCount(+e.target.value)}
            />
          )}
          {openError && (
            <p className="rounded-lg bg-danger/10 px-3 py-2 text-sm text-danger">{openError}</p>
          )}
        </div>
      </Modal>

      <Modal
        open={!!closeModal}
        onClose={() => setCloseModal(null)}
        title={t('session.close')}
        footer={
          <>
            <Button variant="secondary" onClick={() => setCloseModal(null)}>{t('session.cancel')}</Button>
            <Button loading={loading} onClick={handleClose}>{t('session.confirm')}</Button>
          </>
        }
      >
        <div className="space-y-4">
          {closeModal && (
            <div className="space-y-1 rounded-xl border border-border bg-surface p-3">
              <div className="flex justify-between text-sm text-muted">
                <span>{t('session.timeCost')}</span>
                <span>{formatCurrency(closeModal.currentTimeCost)}</span>
              </div>
              {closeModal.roomSurchargeCost > 0 && (
                <div className="flex justify-between text-sm text-muted">
                  <span>{t('session.roomSurcharge')}</span>
                  <span>{formatCurrency(closeModal.roomSurchargeCost)}</span>
                </div>
              )}
              <div className="flex justify-between text-sm text-muted">
                <span>{t('session.cafeteria')}</span>
                <span>{formatCurrency(closeModal.cafeteriaCost)}</span>
              </div>
              <div className="flex justify-between text-sm font-medium">
                <span>{t('session.subtotal')}</span>
                <span>{formatCurrency(closeModal.totalCost)}</span>
              </div>
              {(Number(discountAmount) || 0) > 0 && (
                <div className="flex justify-between text-sm text-danger">
                  <span>{t('session.discount')}</span>
                  <span>-{formatCurrency(Math.min(Number(discountAmount) || 0, closeModal.totalCost))}</span>
                </div>
              )}
              {(Number(walletPayAmount) || 0) > 0 && (
                <div className="flex justify-between text-sm text-accent">
                  <span>{t('session.customerWallet')}</span>
                  <span>-{formatCurrency(Number(walletPayAmount) || 0)}</span>
                </div>
              )}
              <div className="flex justify-between border-t border-border pt-2 text-xl font-bold text-success">
                <span>{t('session.payable')}</span>
                <span>
                  {formatCurrency(
                    Math.max(
                      0,
                      closeModal.totalCost -
                        Math.min(Number(discountAmount) || 0, closeModal.totalCost) -
                        (Number(walletPayAmount) || 0)
                    )
                  )}
                </span>
              </div>
            </div>
          )}
          <Input
            label={t('session.discount')}
            type="number"
            min={0}
            step="1"
            value={discountAmount}
            onChange={(e) => setDiscountAmount(e.target.value)}
            placeholder="0"
          />
          {(Number(discountAmount) || 0) > 0 && (
            <Input
              label={t('session.discountReason')}
              value={discountReason}
              onChange={(e) => setDiscountReason(e.target.value)}
              placeholder={t('session.discountReasonOptional')}
            />
          )}
          {closeCustomer && closeCustomer.walletBalance > 0 && (
            <div className="space-y-2 rounded-xl border border-accent/40 bg-accent/5 p-3">
              <div className="flex items-center justify-between text-sm">
                <span className="font-medium">{t('session.payFromWallet')}</span>
                <span className="text-accent">
                  {t('session.walletBalanceLabel')}: {formatCurrency(closeCustomer.walletBalance)}
                </span>
              </div>
              <Input
                label={t('session.walletAmountLabel')}
                type="number"
                min={0}
                value={walletPayAmount}
                onChange={(e) => setWalletPayAmount(e.target.value)}
                placeholder="0"
              />
              {closeModal &&
                (Number(walletPayAmount) || 0) > 0 &&
                (Number(walletPayAmount) || 0) >=
                  Math.max(0, closeModal.totalCost - Math.min(Number(discountAmount) || 0, closeModal.totalCost)) && (
                  <p className="text-xs text-success">{t('session.walletCoversAll')}</p>
                )}
            </div>
          )}
          <div className="flex flex-wrap gap-2">
            <Button variant={paymentMethod === PaymentMethod.Cash ? 'primary' : 'secondary'} size="sm"
              onClick={() => { setPaymentMethod(PaymentMethod.Cash); setProofFile(null); }}>{t('session.cash')}</Button>
            <Button variant={paymentMethod === PaymentMethod.BankTransfer ? 'primary' : 'secondary'} size="sm"
              onClick={() => setPaymentMethod(PaymentMethod.BankTransfer)}>{t('session.bankTransfer')}</Button>
            <Button variant={paymentMethod === PaymentMethod.DigitalWallet ? 'primary' : 'secondary'} size="sm"
              onClick={() => setPaymentMethod(PaymentMethod.DigitalWallet)}>{t('session.digitalWallet')}</Button>
            <Button variant={paymentMethod === PaymentMethod.Deferred ? 'primary' : 'secondary'} size="sm"
              onClick={() => { setPaymentMethod(PaymentMethod.Deferred); setProofFile(null); }}>{t('session.deferred')}</Button>
          </div>
          {paymentMethod === PaymentMethod.Deferred && (
            <Input label={t('session.debtorName')} value={debtorName} onChange={(e) => setDebtorName(e.target.value)} required />
          )}
          {(paymentMethod === PaymentMethod.BankTransfer || paymentMethod === PaymentMethod.DigitalWallet) && (
            <div className="space-y-3 rounded-xl border border-border bg-surface p-3">
              {(() => {
                const accounts = transferAccountsFor(paymentMethod);
                return accounts.length > 0 ? (
                  <div className="space-y-2">
                    <p className="text-xs font-medium text-muted">{t('session.availableAccounts')}</p>
                    {accounts.map((a) => (
                      <div key={a.id || a.accountNumber} className="flex items-center justify-between gap-2 rounded-lg border border-border px-3 py-2 text-sm">
                        <span className="text-muted">{a.label || t('session.sendTo')}</span>
                        <span className="font-mono text-base font-bold tracking-wide text-primary">{a.accountNumber}</span>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-muted">{t('session.noAccountConfigured')}</p>
                );
              })()}
              <div>
                <p className="mb-1 text-sm font-medium">{t('session.proofOptional')}</p>
                <p className="mb-2 text-xs text-muted">{t('session.proofHint')}</p>
                {proofFile ? (
                  <div className="flex items-center justify-between gap-2 rounded-lg border border-border px-3 py-2 text-sm">
                    <span className="truncate">{t('session.proofSelected')}: {proofFile.name}</span>
                    <Button variant="ghost" size="sm" onClick={() => setProofFile(null)}>{t('session.removeProof')}</Button>
                  </div>
                ) : (
                  <input
                    type="file"
                    accept="image/*"
                    className="block w-full text-sm text-muted file:me-3 file:rounded-lg file:border-0 file:bg-primary/15 file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-primary"
                    onChange={(e) => setProofFile(e.target.files?.[0] ?? null)}
                  />
                )}
              </div>
            </div>
          )}
          {cafError && <p className="text-sm text-danger">{cafError}</p>}
        </div>
      </Modal>

      <Modal
        open={!!cafSession}
        onClose={() => setCafSession(null)}
        title={t('dashboard.addCafeteria')}
        footer={
          <>
            <Button variant="secondary" onClick={() => setCafSession(null)}>{t('session.cancel')}</Button>
            <Button loading={loading} onClick={handleAddCafeteriaToSession}>{t('session.confirm')}</Button>
          </>
        }
      >
        <div className="space-y-3">
          {cafSession && (
            <p className="text-sm text-muted">
              {cafSession.deviceName} · {cafSession.roomName}
              {' — '}
              {t('cafeteria.chargeToSession')}
            </p>
          )}
          <Input
            label={t('cafeteria.customerName')}
            value={cafCustomerName}
            onChange={(e) => setCafCustomerName(e.target.value)}
            placeholder={t('cafeteria.customerNameOptional')}
          />
          {(() => {
            const activeItems = cafItems.filter((i: CafeteriaItem) => i.isActive);
            const selectedIds = Object.keys(cafQty).filter((id) => (cafQty[id] ?? 0) > 0);
            const selectedItems = selectedIds
              .map((id) => activeItems.find((i) => i.id === id))
              .filter((i): i is CafeteriaItem => !!i);
            const query = cafSearch.trim().toLowerCase();
            const results = activeItems.filter(
              (i) =>
                !selectedIds.includes(i.id) &&
                (query === '' ||
                  i.name.toLowerCase().includes(query) ||
                  (i.nameAr ?? '').toLowerCase().includes(query))
            );
            const cartTotal = selectedItems.reduce((sum, item) => {
              const unit = cafUnit[item.id] ?? InventoryUnitKind.Base;
              const baseQty = (cafQty[item.id] ?? 0) * (unit === InventoryUnitKind.Large ? item.unitsPerLarge : 1);
              return sum + baseQty * item.sellPrice;
            }, 0);
            return (
              <>
                <Input
                  label={t('cafeteria.searchItems')}
                  value={cafSearch}
                  onChange={(e) => setCafSearch(e.target.value)}
                  placeholder={t('cafeteria.searchPlaceholder')}
                />
                <div className="max-h-40 space-y-1 overflow-y-auto rounded-lg border border-border p-1">
                  {results.map((item: CafeteriaItem) => {
                    const outOfStock = maxSellQuantity(item, InventoryUnitKind.Base) <= 0;
                    return (
                      <button
                        key={item.id}
                        type="button"
                        disabled={outOfStock}
                        className="flex w-full items-center justify-between gap-2 rounded-md px-2 py-1.5 text-start hover:bg-surface-elevated disabled:cursor-not-allowed disabled:opacity-50"
                        onClick={() => {
                          setCafQty((prev) => ({ ...prev, [item.id]: 1 }));
                          setCafSearch('');
                        }}
                      >
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-sm font-medium">{item.name}</span>
                          <span className="block text-xs text-muted">
                            {formatCurrency(item.sellPrice)} / {item.baseUnitName}
                            {' · '}
                            {t('cafeteria.stock')}: {formatStockDisplay(item)}
                          </span>
                        </span>
                        <span className="text-xs font-medium text-primary">+ {t('cafeteria.addToCart')}</span>
                      </button>
                    );
                  })}
                  {results.length === 0 && (
                    <p className="px-2 py-2 text-sm text-muted">
                      {activeItems.length === 0 ? t('cafeteria.noItems') : t('cafeteria.noSearchResults')}
                    </p>
                  )}
                </div>

                {selectedItems.length > 0 && (
                  <div className="space-y-2">
                    <p className="text-sm font-medium">{t('cafeteria.cart')}</p>
                    {selectedItems.map((item) => {
                      const unit = cafUnit[item.id] ?? InventoryUnitKind.Base;
                      const max = maxSellQuantity(item, unit);
                      const qty = cafQty[item.id] ?? 0;
                      return (
                        <div key={item.id} className="flex flex-wrap items-center gap-2 rounded-lg border border-border px-3 py-2">
                          <div className="min-w-0 flex-1">
                            <p className="truncate text-sm font-medium">{item.name}</p>
                            <p className="text-xs text-muted">
                              {formatCurrency(item.sellPrice)} / {item.baseUnitName}
                              {' · '}
                              {t('cafeteria.stock')}: {formatStockDisplay(item)}
                            </p>
                          </div>
                          {hasLargeUnit(item) && (
                            <select
                              className="rounded border border-border bg-surface-elevated px-2 py-1 text-xs"
                              value={unit}
                              onChange={(e) => {
                                const next = Number(e.target.value) as InventoryUnitKind;
                                setCafUnit((prev) => ({ ...prev, [item.id]: next }));
                                setCafQty((prev) => ({
                                  ...prev,
                                  [item.id]: Math.max(1, Math.min(prev[item.id] ?? 1, maxSellQuantity(item, next))),
                                }));
                              }}
                            >
                              <option value={InventoryUnitKind.Base}>{item.baseUnitName}</option>
                              <option value={InventoryUnitKind.Large}>{item.largeUnitName}</option>
                            </select>
                          )}
                          <div className="flex items-center gap-1">
                            <Button
                              variant="secondary"
                              size="sm"
                              onClick={() =>
                                setCafQty((prev) => ({ ...prev, [item.id]: Math.max(1, (prev[item.id] ?? 1) - 1) }))
                              }
                            >
                              −
                            </Button>
                            <Input
                              type="number"
                              min={1}
                              max={max}
                              className="w-16 text-center"
                              value={qty}
                              onChange={(e) =>
                                setCafQty((prev) => ({
                                  ...prev,
                                  [item.id]: Math.max(1, Math.min(max, Number(e.target.value) || 1)),
                                }))
                              }
                            />
                            <Button
                              variant="secondary"
                              size="sm"
                              onClick={() =>
                                setCafQty((prev) => ({ ...prev, [item.id]: Math.min(max, (prev[item.id] ?? 0) + 1) }))
                              }
                            >
                              +
                            </Button>
                          </div>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() =>
                              setCafQty((prev) => {
                                const next = { ...prev };
                                delete next[item.id];
                                return next;
                              })
                            }
                          >
                            ✕
                          </Button>
                        </div>
                      );
                    })}
                    <div className="flex items-center justify-between rounded-lg bg-surface-elevated px-3 py-2 text-sm font-medium">
                      <span>{t('cafeteria.total')}</span>
                      <span>{formatCurrency(cartTotal)}</span>
                    </div>
                  </div>
                )}
              </>
            );
          })()}

          {canReturn && cafSession && (cafSession.cafeteriaLines?.length ?? 0) > 0 && (
            <div className="space-y-2 border-t border-border pt-3">
              <p className="text-sm font-medium">{t('cafeteria.sessionReturns')}</p>
              <p className="text-xs text-muted">{t('cafeteria.sessionReturnsHint')}</p>
              <select
                className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                value={returnLineId}
                onChange={(e) => setReturnLineId(e.target.value)}
              >
                <option value="">{t('cafeteria.selectReturnLine')}</option>
                {(cafSession.cafeteriaLines ?? [])
                  .filter((l) => l.quantity > l.returnedQuantity)
                  .map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.itemName} × {l.quantity - l.returnedQuantity}
                      {l.customerName ? ` (${l.customerName})` : ''}
                    </option>
                  ))}
              </select>
              <div className="grid grid-cols-2 gap-2">
                <Input
                  label={t('inventory.qty')}
                  type="number"
                  min={1}
                  value={returnQty}
                  onChange={(e) => setReturnQty(e.target.value)}
                />
                <Input
                  label={t('inventory.reason')}
                  value={returnReason}
                  onChange={(e) => setReturnReason(e.target.value)}
                />
              </div>
              <Button
                variant="secondary"
                size="sm"
                loading={loading}
                disabled={!returnLineId}
                onClick={handleReturnCafeteria}
              >
                {t('cafeteria.returnItem')}
              </Button>
            </div>
          )}

          {cafError && <p className="text-sm text-danger">{cafError}</p>}
        </div>
      </Modal>
    </div>
  );
}
