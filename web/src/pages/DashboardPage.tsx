import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { alertsApi, assetsApi, branchesApi, ApiError, cafeteriaApi, customersApi, pricingApi, reservationsApi, sessionsApi, uploadsApi } from '@/api/client';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card, CardHeader, CardTitle } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { formatCurrency, formatDuration, parseServerUtc, useLiveTimer, useSessionHub } from '@/hooks/useSessions';
import { formatDateTimeEgypt, toEgyptDateTimeLocalInput, egyptLocalInputToUtcIso } from '@/lib/dates';
import { playTimeUpSound } from '@/lib/timeUpSound';
import { hasPermission, Permissions } from '@/lib/permissions';
import { printSessionInvoice } from '@/lib/printSessionInvoice';
import { useAuthStore, useUiStore } from '@/store';
import type {
  AssetDashboardDevice,
  BillingSegment,
  CafeteriaAddOn,
  CafeteriaItem,
  CafeteriaItemVariant,
  Customer,
  DeviceReservation,
  MissingIngredient,
  PricingPlan,
  SessionDetail,
  SessionLive,
} from '@/types';
import { CafeteriaItemKind, PaymentMethod, SessionMode, SessionStatus, TimeUnit, WatchingBilling, PaymentAccountType } from '@/types';

type CafCartAddOn = { addOnId: string; name: string; price: number; quantity: number };

type CafCartLine = {
  itemId: string;
  itemName: string;
  itemKind: number;
  variantId: string;
  variantName: string;
  variant: CafeteriaItemVariant;
  price: number;
  quantity: number;
  stockDeduct: number;
  stockAvailable: number;
  addOns: CafCartAddOn[];
};

function variantHasRecipe(variant: CafeteriaItemVariant) {
  return (variant.recipeLines ?? []).length > 0;
}

function needsManualStockDeduct(item: { kind: number }, variant: CafeteriaItemVariant) {
  return item.kind === CafeteriaItemKind.SellAsIs && !variantHasRecipe(variant);
}

function parseMissingIngredients(err: unknown): MissingIngredient[] | null {
  if (!(err instanceof ApiError) || err.code !== 'MISSING_INGREDIENTS') return null;
  const body = err.data as { missing?: MissingIngredient[] } | undefined;
  return body?.missing ?? null;
}

type GuestType = 'none' | 'registered' | 'quick';

function statusKey(status: string) {
  return status.toLowerCase() as 'idle' | 'gaming' | 'watching' | 'paused' | 'inactive';
}

function billingSegmentFormula(
  seg: BillingSegment,
  t: (key: string, opts?: Record<string, unknown>) => string
): string {
  const money = (n: number) => formatCurrency(n);
  if (seg.quantityUnit === 'match') {
    return t('session.formulaMatch', {
      rate: money(seg.rate),
      count: seg.quantity,
      amount: money(seg.amount),
    });
  }
  if (seg.quantityUnit === 'guest') {
    return t('session.formulaWatching', {
      rate: money(seg.rate),
      count: seg.quantity,
      amount: money(seg.amount),
    });
  }
  if (seg.quantityUnit === 'hour' && (seg.peopleCount ?? 0) > 0) {
    return t('session.formulaWatchingTime', {
      rate: money(seg.rate),
      people: seg.peopleCount,
      hours: seg.quantity,
      amount: money(seg.amount),
    });
  }
  if (seg.quantityUnit === 'min' && (seg.peopleCount ?? 0) > 0) {
    return t('session.formulaWatchingMin', {
      rate: money(seg.rate),
      people: seg.peopleCount,
      mins: seg.quantity,
      amount: money(seg.amount),
    });
  }
  if (seg.quantityUnit === 'hour') {
    return t('session.formulaHourly', {
      rate: money(seg.rate),
      hours: seg.quantity,
      amount: money(seg.amount),
    });
  }
  if (seg.quantityUnit === 'min') {
    return t('session.formulaMinute', {
      rate: money(seg.rate),
      mins: seg.quantity,
      amount: money(seg.amount),
    });
  }
  return `${money(seg.rate)} × ${seg.quantity} = ${money(seg.amount)}`;
}

function DeviceCard({
  device,
  roomName,
  session,
  reservation,
  onOpen,
  onReserve,
  onCancelReservation,
  onStartReservation,
  onPause,
  onResume,
  onClose,
  onConvert,
  onExtend,
  onWatchersChange,
  onAddCafeteria,
  onPreviewBill,
  canAddCafeteria,
  dateLocale,
}: {
  device: AssetDashboardDevice;
  roomName: string;
  session?: SessionLive;
  reservation?: DeviceReservation;
  onOpen: () => void;
  onReserve: () => void;
  onCancelReservation: () => void;
  onStartReservation: () => void;
  onPause: () => void;
  onResume: () => void;
  onClose: () => void;
  onConvert: () => void;
  onExtend: (additionalMinutes: number | null) => void;
  onWatchersChange: (watcherCount: number) => void;
  onAddCafeteria: () => void;
  onPreviewBill: () => void;
  canAddCafeteria: boolean;
  dateLocale: string;
}) {
  const { t } = useTranslation();
  const elapsed = useLiveTimer(session ?? null);
  const isActive = !!session && session.status !== SessionStatus.Closed;
  const deviceOffline = !device.isActive;
  const reservationGuestName = reservation ? (reservation.customerName ?? reservation.guestName) : null;
  const liveStatus = deviceOffline
    ? 'Inactive'
    : session?.status === SessionStatus.Paused
      ? 'Paused'
      : device.liveStatus;

  return (
    <Card
      className={`relative overflow-hidden transition-all ${
        deviceOffline
          ? 'border-danger/60 bg-danger/5 opacity-95'
          : isActive
            ? 'border-primary/50 shadow-lg shadow-primary/10'
            : ''
      }`}
    >
      {isActive && !deviceOffline && (
        <div className="absolute inset-x-0 top-0 h-0.5 bg-gradient-to-r from-primary to-accent" />
      )}
      {deviceOffline && (
        <div className="absolute inset-x-0 top-0 h-0.5 bg-danger" />
      )}
      <CardHeader>
        <div>
          <CardTitle className={`text-base ${deviceOffline ? 'text-danger' : ''}`}>{device.name}</CardTitle>
          <p className="text-xs text-muted">{roomName}</p>
        </div>
        <Badge status={statusKey(liveStatus)} pulse={liveStatus === 'Gaming' || liveStatus === 'Watching'}>
          {deviceOffline
            ? t('common.inactive')
            : t(`dashboard.${statusKey(liveStatus)}`, { defaultValue: liveStatus })}
        </Badge>
      </CardHeader>

      {deviceOffline ? (
        <div className="space-y-2 pt-1">
          <p className="text-sm text-danger">{t('dashboard.deviceInactiveHint')}</p>
        </div>
      ) : isActive && session ? (
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
          {(session.cafeteriaCost > 0 || session.currentTimeCost > 0 || (session.appliedHourlyRate ?? 0) > 0) && (
            <p className="text-xs text-muted">
              {session.appliedHourlyRate != null && session.appliedHourlyRate > 0 && (
                <>
                  {session.sessionMode === SessionMode.Watching
                    ? t('session.watching')
                    : session.appliedRateTier === 'Couple'
                      ? t('settings.couple')
                      : t('settings.individual')}
                  {': '}
                  {formatCurrency(session.appliedHourlyRate)}
                  {session.timeUnit === TimeUnit.PerGame
                    ? `/${t('dashboard.match')}`
                    : session.sessionMode === SessionMode.Watching
                      ? `/${t('session.hoursShort')}/${t('dashboard.guestsShort')}`
                      : `/${t('session.hoursShort')}`}
                  {session.sessionMode === SessionMode.Watching && session.watcherCount
                    ? ` · ${session.watcherCount} ${t('dashboard.guestsShort')}`
                    : ''}
                  {' · '}
                </>
              )}
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
          <Button variant="secondary" size="sm" className="w-full" onClick={onPreviewBill}>
            <Icon name="print" className="h-3.5 w-3.5" />
            {t('dashboard.previewBill')}
            {` · ${formatCurrency(session.totalCost)}`}
          </Button>
          {session.canChangePricing && (
            <Button variant="secondary" size="sm" className="w-full" onClick={onConvert}>
              <Icon name="settings" className="h-3.5 w-3.5" />
              {t('dashboard.changePricing')}
            </Button>
          )}
          {session.timeUnit === TimeUnit.PerGame && (
            <p className="text-xs text-muted">{t('dashboard.perMatchHint')}</p>
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
        <div className="space-y-3 pt-1">
          <span className="text-sm text-muted">
            {device.workingControllers} ctrl · {device.maxWatchingCapacity} watch
          </span>
          {reservation ? (
            <div className="space-y-2">
              <div className="rounded-lg border border-warning/40 bg-warning/10 px-3 py-2 text-xs text-warning">
                <p className="font-medium">
                  {t('dashboard.reservationStarts', {
                    time: formatDateTimeEgypt(reservation.startsAt, dateLocale),
                  })}
                </p>
                {reservationGuestName && (
                  <p>{t('dashboard.reservationGuest', { name: reservationGuestName })}</p>
                )}
              </div>
              <div className="flex flex-wrap gap-2">
                <Button variant="secondary" size="sm" onClick={onCancelReservation}>
                  {t('dashboard.cancelReservation')}
                </Button>
                <Button variant="secondary" size="sm" onClick={onStartReservation}>
                  {t('dashboard.startReservation')}
                </Button>
                <Button size="sm" onClick={onOpen}>
                  <Icon name="play" className="h-3.5 w-3.5" />
                  {t('dashboard.openSession')}
                </Button>
              </div>
            </div>
          ) : (
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" size="sm" onClick={onReserve}>
                <Icon name="clock" className="h-3.5 w-3.5" />
                {t('dashboard.reserve')}
              </Button>
              <Button size="sm" onClick={onOpen}>
                <Icon name="play" className="h-3.5 w-3.5" />
                {t('dashboard.openSession')}
              </Button>
            </div>
          )}
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
  const [reserveModal, setReserveModal] = useState<AssetDashboardDevice | null>(null);
  const [reserveStartsAt, setReserveStartsAt] = useState(toEgyptDateTimeLocalInput());
  const [reserveGuestName, setReserveGuestName] = useState('');
  const [reserveNotes, setReserveNotes] = useState('');
  const [reserveError, setReserveError] = useState('');
  const [reserveLoading, setReserveLoading] = useState(false);
  const [openingReservationId, setOpeningReservationId] = useState<string | null>(null);
  const [closeModal, setCloseModal] = useState<SessionLive | null>(null);
  const [convertModal, setConvertModal] = useState<SessionLive | null>(null);
  const [convertPlanId, setConvertPlanId] = useState('');
  const [convertControllers, setConvertControllers] = useState(2);
  const [convertWatchers, setConvertWatchers] = useState(2);
  const [convertMatchCount, setConvertMatchCount] = useState('1');
  const [convertError, setConvertError] = useState('');
  const [closeMatchCount, setCloseMatchCount] = useState('1');
  const [invoiceResult, setInvoiceResult] = useState<SessionDetail | null>(null);
  const [billPreview, setBillPreview] = useState<SessionLive | null>(null);
  const [cafSession, setCafSession] = useState<SessionLive | null>(null);
  const [cafCart, setCafCart] = useState<CafCartLine[]>([]);
  const [cafPickItem, setCafPickItem] = useState<CafeteriaItem | null>(null);
  const [cafPickVariantId, setCafPickVariantId] = useState('');
  const [cafPickQty, setCafPickQty] = useState('1');
  const [cafPickStock, setCafPickStock] = useState('1');
  const [cafPickAddOns, setCafPickAddOns] = useState<Record<string, number>>({});
  const [cafMissingDialog, setCafMissingDialog] = useState<{
    missing: MissingIngredient[];
    retry: () => Promise<void>;
  } | null>(null);
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
  const dateLocale = language === 'ar' ? 'ar-EG' : 'en-EG';
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
  const [invoiceActionError, setInvoiceActionError] = useState('');
  const [pdfLoading, setPdfLoading] = useState(false);

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

  const { data: reservations = [] } = useQuery({
    queryKey: ['reservations', user?.id, activeBranchId],
    queryFn: reservationsApi.list,
    enabled: !!activeBranchId,
    refetchInterval: 30000,
  });

  const { data: gamingPlans } = useQuery({
    queryKey: ['plans', SessionMode.Gaming],
    queryFn: () => pricingApi.getPlans(SessionMode.Gaming),
    enabled: !!openModal || !!convertModal,
  });

  const { data: watchingPlans } = useQuery({
    queryKey: ['plans', SessionMode.Watching],
    queryFn: () => pricingApi.getPlans(SessionMode.Watching),
    enabled: !!openModal || !!convertModal,
  });

  const convertPlans = useMemo(
    () => [...(gamingPlans ?? []), ...(watchingPlans ?? [])],
    [gamingPlans, watchingPlans]
  );
  const convertSelectedPlan = convertPlans.find((p) => p.id === convertPlanId);
  const convertDevice = useMemo(() => {
    if (!convertModal || !dashboard) return null;
    for (const room of dashboard.rooms ?? []) {
      const d = room.devices.find((x) => x.id === convertModal.deviceId);
      if (d) return d;
    }
    return (dashboard.unassignedDevices ?? []).find((x) => x.id === convertModal.deviceId) ?? null;
  }, [convertModal, dashboard]);

  const { data: customerSearchResults } = useQuery({
    queryKey: ['customers', 'open-session', debouncedCustomerQ],
    queryFn: () => customersApi.getAll(debouncedCustomerQ || undefined, 1, 10),
    enabled: !!openModal && guestType === 'registered',
  });

  const { data: cafItems = [] } = useQuery({
    queryKey: ['cafeteria-items', 'for-sale', user?.id, activeBranchId],
    queryFn: () => cafeteriaApi.getItems({ forSaleOnly: true }),
    enabled: !!cafSession,
  });

  const { data: cafAddOns = [] } = useQuery({
    queryKey: ['cafeteria-addons', 'active', user?.id, activeBranchId],
    queryFn: () => cafeteriaApi.getAddOns(true),
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
  const reservationMap = useMemo(() => {
    const map = new Map<string, DeviceReservation>();
    for (const reservation of reservations) {
      if (!map.has(reservation.deviceId)) map.set(reservation.deviceId, reservation);
    }
    return map;
  }, [reservations]);
  const plans = mode === SessionMode.Gaming ? gamingPlans : watchingPlans;
  const selectedPlan = plans?.find((p) => p.id === planId);
  const isPerGamePlan = selectedPlan?.timeUnit === TimeUnit.PerGame;
  const isFlatWatchingPlan =
    selectedPlan?.sessionMode === SessionMode.Watching &&
    selectedPlan.watchingBilling === WatchingBilling.PerPerson;
  const hideTimerOptions = isPerGamePlan || isFlatWatchingPlan;
  const openModalReservation = openModal ? reservationMap.get(openModal.id) : undefined;
  const showReservationConflictBanner =
    openingReservationId === null && !!openModalReservation?.warnWithinOneHour;

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

  function openSessionForDevice(device: AssetDashboardDevice) {
    setOpeningReservationId(null);
    setOpenModal(device);
    setPlanId('');
    setBookingMode('open');
    setDurationHours(2);
    setControllerCount(2);
    setOpenError('');
  }

  function openReserveForDevice(device: AssetDashboardDevice) {
    setReserveModal(device);
    setReserveStartsAt(toEgyptDateTimeLocalInput());
    setReserveGuestName('');
    setReserveNotes('');
    setReserveError('');
  }

  async function handleCancelReservation(reservation: DeviceReservation) {
    if (!window.confirm(t('dashboard.cancelReservation'))) return;
    try {
      await reservationsApi.cancel(reservation.id);
      queryClient.invalidateQueries({ queryKey: ['reservations'] });
    } catch (e) {
      window.alert(e instanceof Error ? e.message : t('common.error'));
    }
  }

  function startReservationForDevice(device: AssetDashboardDevice, reservation: DeviceReservation) {
    setOpeningReservationId(reservation.id);
    setOpenModal(device);
    setPlanId('');
    setBookingMode('open');
    setDurationHours(2);
    setControllerCount(2);
    setOpenError('');
    if (reservation.customerId) {
      setGuestType('registered');
      setCustomerSearch(reservation.customerName ?? '');
      setSelectedCustomer({
        id: reservation.customerId,
        code: '',
        name: reservation.customerName ?? '',
        phone: '',
        notes: null,
        walletBalance: 0,
        isActive: true,
        createdAt: '',
        outstandingDebtAmount: 0,
        outstandingDebtCount: 0,
      });
      setQuickGuestName('');
    } else {
      setGuestType('quick');
      setCustomerSearch('');
      setSelectedCustomer(null);
      setQuickGuestName(reservation.guestName ?? '');
    }
  }

  async function handleReserveSubmit() {
    if (!reserveModal) return;
    const guestName = reserveGuestName.trim();
    if (!guestName) {
      setReserveError(t('dashboard.reserveGuestName'));
      return;
    }
    setReserveLoading(true);
    setReserveError('');
    try {
      const startsAt = egyptLocalInputToUtcIso(reserveStartsAt);
      await reservationsApi.create({
        deviceId: reserveModal.id,
        startsAt,
        guestName,
        notes: reserveNotes.trim() || undefined,
      });
      setReserveModal(null);
      setReserveStartsAt(toEgyptDateTimeLocalInput());
      setReserveGuestName('');
      setReserveNotes('');
      queryClient.invalidateQueries({ queryKey: ['reservations'] });
    } catch (e) {
      setReserveError(e instanceof Error ? e.message : t('common.error'));
    } finally {
      setReserveLoading(false);
    }
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
      if (!openingReservationId) {
        const conflict = await reservationsApi.checkConflict(openModal.id);
        if (conflict.hasConflict) {
          const message = language === 'ar' ? conflict.messageAr : conflict.messageEn;
          if (!window.confirm(message || t('dashboard.reservationConflictWarn'))) return;
        }
      }
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
        reservationId: openingReservationId ?? undefined,
      });
      setOpenModal(null);
      setOpeningReservationId(null);
      setBookingMode('open');
      setDurationHours(2);
      setGuestType('none');
      setCustomerSearch('');
      setSelectedCustomer(null);
      setQuickGuestName('');
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['reservations'] });
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

      const isClosePerMatch = closeModal.timeUnit === TimeUnit.PerGame;
      const matchCount = Number(closeMatchCount) || 0;
      if (isClosePerMatch && matchCount < 1) {
        setCafError(t('session.matchCountRequired'));
        return;
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
        matchCount: isClosePerMatch ? matchCount : undefined,
      });
      setCloseModal(null);
      setCloseMatchCount('1');
      setDebtorName('');
      setDiscountAmount('');
      setDiscountReason('');
      setPaymentMethod(PaymentMethod.Cash);
      setWalletPayAmount('');
      setProofFile(null);
      setInvoiceActionError('');
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

  async function handleDownloadInvoicePdf() {
    if (!invoiceResult) return;
    setPdfLoading(true);
    setInvoiceActionError('');
    try {
      const blob = await alertsApi.downloadInvoicePdf(invoiceResult.id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${invoiceResult.invoice?.invoiceNumber ?? invoiceResult.invoiceNumber ?? 'invoice'}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setInvoiceActionError(e instanceof Error ? e.message : t('common.error'));
    } finally {
      setPdfLoading(false);
    }
  }

  async function submitCafeteriaToSession(allowSkip = false) {
    if (!cafSession) return;
    const sessionCustomerName =
      cafSession.customerName?.trim() ||
      cafSession.quickGuestName?.trim() ||
      undefined;
    let last: SessionLive | null = null;
    for (const line of cafCart) {
      last = await sessionsApi.addCafeteria(
        cafSession.id,
        line.itemId,
        line.variantId,
        line.quantity,
        line.stockDeduct,
        sessionCustomerName || cafCustomerName.trim() || undefined,
        line.addOns.map((a) => ({ addOnId: a.addOnId, quantity: a.quantity })),
        allowSkip
      );
    }
    if (last) onUpdate(last);
    setCafSession(null);
    setCafCart([]);
    setCafPickItem(null);
    setCafCustomerName('');
    setCafSearch('');
    setReturnLineId('');
    setReturnQty('1');
    setReturnReason('');
    setCafMissingDialog(null);
    queryClient.invalidateQueries({ queryKey: ['cafeteria-items'] });
    queryClient.invalidateQueries({ queryKey: ['sessions'] });
  }

  async function handleAddCafeteriaToSession() {
    if (!cafSession) return;
    if (cafCart.length === 0) {
      setCafError(t('cafeteria.emptyCart'));
      return;
    }
    setLoading(true);
    setCafError('');
    try {
      await submitCafeteriaToSession(false);
    } catch (e) {
      const missing = parseMissingIngredients(e);
      if (missing) {
        setCafMissingDialog({
          missing,
          retry: () => submitCafeteriaToSession(true),
        });
      } else {
        setCafError(e instanceof Error ? e.message : t('common.error'));
      }
    } finally {
      setLoading(false);
    }
  }

  function confirmCafPick() {
    if (!cafPickItem) return;
    const variant = (cafPickItem.variants ?? []).find((v: CafeteriaItemVariant) => v.id === cafPickVariantId);
    if (!variant) return;
    const quantity = Math.max(1, Number(cafPickQty) || 1);
    const manual = needsManualStockDeduct(cafPickItem, variant);
    const stockDeduct = manual ? Math.max(1, Number(cafPickStock) || quantity) : quantity;
    if (manual && stockDeduct > cafPickItem.currentQuantity) {
      setCafError(t('inventory.insufficientStock'));
      return;
    }
    const selectedAddOns: CafCartAddOn[] = Object.entries(cafPickAddOns)
      .filter(([, q]) => q > 0)
      .map(([addOnId, qty]) => {
        const addon = cafAddOns.find((a: CafeteriaAddOn) => a.id === addOnId)!;
        return { addOnId, name: addon.name, price: addon.sellPrice, quantity: qty };
      });
    setCafCart((prev) => {
      const existing = prev.find((l) => l.variantId === variant.id);
      if (existing) {
        return prev.map((l) =>
          l.variantId === variant.id
            ? {
                ...l,
                quantity: l.quantity + quantity,
                stockDeduct: l.stockDeduct + stockDeduct,
                stockAvailable: cafPickItem.currentQuantity,
                addOns: mergeCafAddOns(l.addOns, selectedAddOns),
              }
            : l
        );
      }
      return [
        ...prev,
        {
          itemId: cafPickItem.id,
          itemName: cafPickItem.name,
          itemKind: cafPickItem.kind,
          variantId: variant.id,
          variantName: variant.name,
          variant,
          price: variant.sellPrice,
          quantity,
          stockDeduct,
          stockAvailable: cafPickItem.currentQuantity,
          addOns: selectedAddOns,
        },
      ];
    });
    setCafPickItem(null);
    setCafPickAddOns({});
    setCafSearch('');
    setCafError('');
  }

  function mergeCafAddOns(existing: CafCartAddOn[], added: CafCartAddOn[]): CafCartAddOn[] {
    const map = new Map(existing.map((a) => [a.addOnId, { ...a }]));
    for (const a of added) {
      const prev = map.get(a.addOnId);
      if (prev) prev.quantity += a.quantity;
      else map.set(a.addOnId, { ...a });
    }
    return [...map.values()];
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
                  setCafCart([]);
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
                const reservation = reservationMap.get(device.id);
                return (
                  <DeviceCard
                    key={device.id}
                    device={device}
                    roomName={room.name}
                    session={session}
                    reservation={reservation}
                    onOpen={() => openSessionForDevice(device)}
                    onReserve={() => openReserveForDevice(device)}
                    onCancelReservation={() => reservation && handleCancelReservation(reservation)}
                    onStartReservation={() => reservation && startReservationForDevice(device, reservation)}
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
                      setConvertControllers(session.controllerCount && session.controllerCount > 0 ? session.controllerCount : 2);
                      setConvertWatchers(session.watcherCount && session.watcherCount > 0 ? session.watcherCount : 2);
                      setConvertMatchCount('1');
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
                      setCafCart([]);
                      setCafCustomerName('');
                      setCafSearch('');
                      setReturnLineId('');
                      setReturnQty('1');
                      setReturnReason('');
                      setCafError('');
                    }}
                    onPreviewBill={() => {
                      if (!session) return;
                      setBillPreview(session);
                    }}
                    canAddCafeteria={canSellCafeteria}
                    dateLocale={dateLocale}
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
                const reservation = reservationMap.get(device.id);
                return (
                  <DeviceCard
                    key={device.id}
                    device={device}
                    roomName={t('settings.noRoom')}
                    session={session}
                    reservation={reservation}
                    onOpen={() => openSessionForDevice(device)}
                    onReserve={() => openReserveForDevice(device)}
                    onCancelReservation={() => reservation && handleCancelReservation(reservation)}
                    onStartReservation={() => reservation && startReservationForDevice(device, reservation)}
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
                      setConvertControllers(session.controllerCount && session.controllerCount > 0 ? session.controllerCount : 2);
                      setConvertWatchers(session.watcherCount && session.watcherCount > 0 ? session.watcherCount : 2);
                      setConvertMatchCount('1');
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
                      setCafCart([]);
                      setCafCustomerName('');
                      setCafSearch('');
                      setReturnLineId('');
                      setReturnQty('1');
                      setReturnReason('');
                      setCafError('');
                    }}
                    onPreviewBill={() => {
                      if (!session) return;
                      setBillPreview(session);
                    }}
                    canAddCafeteria={canSellCafeteria}
                    dateLocale={dateLocale}
                  />
                );
              })}
            </div>
          </section>
        )}
        </>
      )}

      <Modal
        open={!!billPreview}
        onClose={() => setBillPreview(null)}
        title={t('dashboard.previewBillTitle')}
        footer={
          <Button variant="secondary" onClick={() => setBillPreview(null)}>
            {t('session.done')}
          </Button>
        }
      >
        {billPreview && (() => {
          const live = sessions.find((s) => s.id === billPreview.id) ?? billPreview;
          const segments = live.billingSegments ?? [];
          const cafLines = (live.cafeteriaLines ?? []).filter((l) => l.quantity - l.returnedQuantity > 0);
          return (
            <div className="space-y-3">
              <p className="text-sm text-muted">{t('dashboard.previewBillHint')}</p>
              <div className="rounded-xl border border-border bg-surface p-3 space-y-1.5">
                <div className="flex justify-between text-sm">
                  <span className="text-muted">{t('session.device')}</span>
                  <span>{live.deviceName}{live.roomName ? ` · ${live.roomName}` : ''}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-muted">{t('session.pricingPlan', { defaultValue: 'Pricing plan' })}</span>
                  <span>{live.pricingPlanName}</span>
                </div>
                {(live.customerName || live.quickGuestName) && (
                  <div className="flex justify-between text-sm">
                    <span className="text-muted">{t('session.guest')}</span>
                    <span>{live.customerName || live.quickGuestName}</span>
                  </div>
                )}
                <div className="flex justify-between text-sm">
                  <span className="text-muted">{t('dashboard.elapsed')}</span>
                  <span dir="ltr">{formatDuration(live.elapsedSeconds)}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-muted">{t('session.timeCost')}</span>
                  <span>{formatCurrency(live.currentTimeCost)}</span>
                </div>
                {segments.length > 0 && (
                  <div className="space-y-1 rounded-lg border border-border/60 bg-bg/40 px-2 py-2">
                    <p className="text-xs font-medium text-muted">{t('session.billingSegments')}</p>
                    {segments.map((seg, idx) => {
                      const formula = billingSegmentFormula(seg, t);
                      return (
                        <div key={`${seg.startedAt}-${idx}`} className="space-y-0.5 border-b border-border/40 py-1.5 last:border-0">
                          <div className="flex justify-between gap-2 text-xs">
                            <span className="min-w-0 font-medium leading-snug">{formula}</span>
                            <span className="shrink-0 font-semibold">{formatCurrency(seg.amount)}</span>
                          </div>
                          {seg.label && (
                            <p className="text-[11px] text-muted leading-snug">{seg.label}</p>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
                {live.timeUnit === TimeUnit.PerGame && (
                  <p className="text-xs text-muted">{t('dashboard.perMatchHint')}</p>
                )}
                {live.roomSurchargeCost > 0 && (
                  <div className="flex justify-between text-sm">
                    <span className="text-muted">{t('session.roomSurcharge')}</span>
                    <span>{formatCurrency(live.roomSurchargeCost)}</span>
                  </div>
                )}
                <div className="flex justify-between text-sm">
                  <span className="text-muted">{t('session.cafeteria')}</span>
                  <span>{formatCurrency(live.cafeteriaCost)}</span>
                </div>
                {cafLines.length > 0 && (
                  <div className="space-y-1 rounded-lg border border-border/60 bg-bg/40 px-2 py-2">
                    <p className="text-xs font-medium text-muted">{t('dashboard.cafeteriaLines')}</p>
                    {cafLines.map((line) => {
                      const qty = line.quantity - line.returnedQuantity;
                      return (
                        <div key={line.id} className="flex justify-between gap-2 text-xs">
                          <span className="min-w-0 leading-snug">
                            {line.itemName} × {qty}
                            {(line.addOns?.length ?? 0) > 0
                              ? ` (+${line.addOns!.map((a) => a.name).join(', ')})`
                              : ''}
                          </span>
                          <span className="shrink-0 font-semibold">
                            {formatCurrency(
                              line.quantity > 0
                                ? (line.lineTotal * qty) / line.quantity
                                : 0
                            )}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                )}
                <div className="flex justify-between border-t border-border pt-2 text-lg font-bold text-success">
                  <span>{t('session.total')}</span>
                  <span>{formatCurrency(live.totalCost)}</span>
                </div>
              </div>
            </div>
          );
        })()}
      </Modal>

      <Modal
        open={!!invoiceResult}
        onClose={() => {
          setInvoiceResult(null);
          setInvoiceActionError('');
        }}
        title={t('session.closedSuccess')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setInvoiceResult(null);
                setInvoiceActionError('');
              }}
            >
              {t('session.done')}
            </Button>
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
              <div className="flex justify-between text-sm">
                <span className="text-muted">{t('session.pricingPlan', { defaultValue: 'Pricing plan' })}</span>
                <span>{invoiceResult.pricingPlanName}</span>
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
              {(invoiceResult.billingSegments?.length ?? 0) > 0 && (
                <div className="space-y-1 rounded-lg border border-border/60 bg-bg/40 px-2 py-2">
                  <p className="text-xs font-medium text-muted">{t('session.billingSegments')}</p>
                  {invoiceResult.billingSegments.map((seg, idx) => {
                    const money = (n: number) => formatCurrency(n);
                    let formula: string;
                    if (seg.quantityUnit === 'match') {
                      formula = t('session.formulaMatch', {
                        rate: money(seg.rate),
                        count: seg.quantity,
                        amount: money(seg.amount),
                      });
                    } else if (seg.quantityUnit === 'guest') {
                      formula = t('session.formulaWatching', {
                        rate: money(seg.rate),
                        count: seg.quantity,
                        amount: money(seg.amount),
                      });
                    } else if (seg.quantityUnit === 'hour' && (seg.peopleCount ?? 0) > 0) {
                      formula = t('session.formulaWatchingTime', {
                        rate: money(seg.rate),
                        people: seg.peopleCount,
                        hours: seg.quantity,
                        amount: money(seg.amount),
                      });
                    } else if (seg.quantityUnit === 'min' && (seg.peopleCount ?? 0) > 0) {
                      formula = t('session.formulaWatchingMin', {
                        rate: money(seg.rate),
                        people: seg.peopleCount,
                        mins: seg.quantity,
                        amount: money(seg.amount),
                      });
                    } else if (seg.quantityUnit === 'hour') {
                      formula = t('session.formulaHourly', {
                        rate: money(seg.rate),
                        hours: seg.quantity,
                        amount: money(seg.amount),
                      });
                    } else if (seg.quantityUnit === 'min') {
                      formula = t('session.formulaMinute', {
                        rate: money(seg.rate),
                        mins: seg.quantity,
                        amount: money(seg.amount),
                      });
                    } else {
                      formula = `${money(seg.rate)} × ${seg.quantity} = ${money(seg.amount)}`;
                    }
                    return (
                      <div key={`${seg.startedAt}-${idx}`} className="space-y-0.5 border-b border-border/40 py-1.5 last:border-0">
                        <div className="flex justify-between gap-2 text-xs">
                          <span className="min-w-0 font-medium leading-snug">{formula}</span>
                          <span className="shrink-0 font-semibold">{money(seg.amount)}</span>
                        </div>
                        {seg.quantityUnit === 'match' && (
                          <p className="text-[11px] text-muted">
                            {t('session.pricePerMatch')}: {money(seg.rate)} · {t('session.matchesCount')}: {seg.quantity}
                          </p>
                        )}
                        {seg.quantityUnit === 'guest' && (
                          <p className="text-[11px] text-muted">
                            {t('session.pricePerPerson')}: {money(seg.rate)} · {t('session.peopleCount')}: {seg.quantity}
                          </p>
                        )}
                        {seg.quantityUnit === 'hour' && (seg.peopleCount ?? 0) > 0 && (
                          <p className="text-[11px] text-muted">
                            {t('session.pricePerPerson')}: {money(seg.rate)} · {t('session.peopleCount')}: {seg.peopleCount} · {t('dashboard.consumedTime')}: {seg.quantity} {t('session.hoursShort')}
                          </p>
                        )}
                        {seg.quantityUnit === 'hour' && !(seg.peopleCount ?? 0) && (
                          <p className="text-[11px] text-muted">
                            {t('session.hourlyRate')}: {money(seg.rate)} · {t('dashboard.consumedTime')}: {seg.quantity} {t('session.hoursShort')}
                          </p>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
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
            {invoiceActionError && <p className="text-sm text-danger">{invoiceActionError}</p>}
          </div>
        )}
      </Modal>

      <Modal
        open={!!convertModal}
        onClose={() => setConvertModal(null)}
        title={t('dashboard.changePricing')}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConvertModal(null)}>{t('session.cancel')}</Button>
            <Button
              loading={loading}
              disabled={
                !convertPlanId ||
                (convertSelectedPlan?.sessionMode === SessionMode.Gaming && !convertControllers) ||
                (convertSelectedPlan?.sessionMode === SessionMode.Watching && !convertWatchers)
              }
              onClick={async () => {
                if (!convertModal || !convertPlanId || !convertSelectedPlan) return;
                const leavingMatch = convertModal.timeUnit === TimeUnit.PerGame;
                const matchCount = Number(convertMatchCount) || 0;
                if (leavingMatch && matchCount < 1) {
                  setConvertError(t('session.matchCountRequired'));
                  return;
                }
                if (convertSelectedPlan.sessionMode === SessionMode.Watching) {
                  const max = convertDevice?.maxWatchingCapacity ?? 20;
                  if (convertWatchers < 1 || convertWatchers > max) {
                    setConvertError(t('session.watchers'));
                    return;
                  }
                }
                setLoading(true);
                setConvertError('');
                try {
                  const updated = await sessionsApi.convert(convertModal.id, {
                    pricingPlanId: convertPlanId,
                    controllerCount:
                      convertSelectedPlan.sessionMode === SessionMode.Gaming
                        ? convertControllers
                        : undefined,
                    watcherCount:
                      convertSelectedPlan.sessionMode === SessionMode.Watching
                        ? convertWatchers
                        : undefined,
                    matchCount: leavingMatch ? matchCount : undefined,
                  });
                  onUpdate(updated);
                  setConvertModal(null);
                  setConvertMatchCount('1');
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
          <p className="text-sm text-muted">{t('dashboard.changePricingHint')}</p>
          {convertModal && convertModal.sessionMode === SessionMode.Watching && (
            <p className="text-xs text-muted">
              {t('dashboard.convertWatchingNote', {
                count: convertModal.watcherCount ?? 0,
                cost: formatCurrency(convertModal.currentTimeCost),
              })}
            </p>
          )}
          {convertModal && convertModal.sessionMode === SessionMode.Gaming && (
            <p className="text-xs text-muted">
              {t('dashboard.convertGamingNote', {
                cost: formatCurrency(convertModal.currentTimeCost),
              })}
            </p>
          )}
          {convertModal && convertModal.timeUnit === TimeUnit.PerGame && (
            <Input
              label={t('session.matchCount')}
              type="number"
              min={1}
              value={convertMatchCount}
              onChange={(e) => setConvertMatchCount(e.target.value)}
            />
          )}
          <div>
            <label className="mb-1 block text-sm text-muted">{t('session.pricingPlan')}</label>
            <select
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm"
              value={convertPlanId}
              onChange={(e) => {
                const id = e.target.value;
                setConvertPlanId(id);
                const plan = convertPlans.find((p) => p.id === id);
                if (plan?.sessionMode === SessionMode.Watching && convertModal?.watcherCount) {
                  setConvertWatchers(convertModal.watcherCount);
                }
              }}
            >
              <option value="">{t('session.pricingPlan')}</option>
              {convertPlans.map((p) => {
                const modeLabel =
                  p.sessionMode === SessionMode.Watching
                    ? t('session.watching')
                    : p.timeUnit === TimeUnit.PerGame
                      ? t('settings.perGame')
                      : t('session.gaming');
                const rateHint =
                  p.sessionMode === SessionMode.Watching
                    ? p.watchingRates[0]
                      ? ` · ${formatCurrency(p.watchingRates[0].ratePerPerson)}`
                      : ''
                    : p.timeUnit === TimeUnit.PerGame
                      ? p.gamingRates[0]
                        ? ` · ${formatCurrency(p.gamingRates[0].rate)}/${t('dashboard.match')}`
                        : ''
                      : p.gamingRates[0]
                        ? ` · ${formatCurrency(p.gamingRates[0].rate)}/${t('session.hoursShort')}`
                        : '';
                return (
                  <option key={p.id} value={p.id}>
                    {p.name} · {modeLabel}
                    {rateHint}
                  </option>
                );
              })}
            </select>
          </div>

          {convertSelectedPlan?.sessionMode === SessionMode.Gaming && (
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
                    const rate = convertSelectedPlan.gamingRates.find((r) => r.controllerCount === 1)?.rate;
                    return rate != null
                      ? ` · ${formatCurrency(rate)}${convertSelectedPlan.timeUnit === TimeUnit.PerGame ? `/${t('dashboard.match')}` : `/${t('session.hoursShort')}`}`
                      : '';
                  })()}
                </Button>
                <Button
                  size="sm"
                  variant={convertControllers >= 3 ? 'primary' : 'secondary'}
                  onClick={() => setConvertControllers(4)}
                >
                  {t('settings.couple')}
                  {(() => {
                    const rate = convertSelectedPlan.gamingRates.find((r) => r.controllerCount === 2)?.rate;
                    return rate != null
                      ? ` · ${formatCurrency(rate)}${convertSelectedPlan.timeUnit === TimeUnit.PerGame ? `/${t('dashboard.match')}` : `/${t('session.hoursShort')}`}`
                      : '';
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
          )}

          {convertSelectedPlan?.sessionMode === SessionMode.Watching && (
            <div>
              <p className="mb-2 text-sm text-muted">{t('dashboard.watchMode')}</p>
              {convertSelectedPlan.watchingRates[0] && (
                <p className="mb-2 text-xs text-muted">
                  {t('dashboard.ratePerHour', {
                    rate: formatCurrency(convertSelectedPlan.watchingRates[0].ratePerPerson),
                  })}
                  {convertSelectedPlan.watchingBilling === WatchingBilling.PerPerson
                    ? ` · ${t('settings.perPerson')}`
                    : ` · ${t('settings.perScreen')}`}
                </p>
              )}
              <Input
                label={t('dashboard.watchers')}
                type="number"
                min={1}
                max={convertDevice?.maxWatchingCapacity ?? 20}
                value={convertWatchers}
                onChange={(e) => setConvertWatchers(Math.max(1, Number(e.target.value) || 1))}
              />
            </div>
          )}
          {convertError && <p className="text-sm text-danger">{convertError}</p>}
        </div>
      </Modal>

      <Modal
        open={!!reserveModal}
        onClose={() => {
          setReserveModal(null);
          setReserveError('');
        }}
        title={t('dashboard.reserveTitle')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setReserveModal(null);
                setReserveError('');
              }}
            >
              {t('session.cancel')}
            </Button>
            <Button
              loading={reserveLoading}
              disabled={!reserveGuestName.trim()}
              onClick={handleReserveSubmit}
            >
              {t('dashboard.reserveSave')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <Input
            label={t('dashboard.reserveStartsAt')}
            type="datetime-local"
            value={reserveStartsAt}
            onChange={(e) => setReserveStartsAt(e.target.value)}
          />
          <Input
            label={t('dashboard.reserveGuestName')}
            value={reserveGuestName}
            onChange={(e) => setReserveGuestName(e.target.value)}
            required
          />
          <Input
            label={t('dashboard.reserveNotes')}
            value={reserveNotes}
            onChange={(e) => setReserveNotes(e.target.value)}
          />
          {reserveError && (
            <p className="rounded-lg bg-danger/10 px-3 py-2 text-sm text-danger">{reserveError}</p>
          )}
        </div>
      </Modal>

      <Modal
        open={!!openModal}
        onClose={() => {
          setOpenModal(null);
          setOpeningReservationId(null);
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
                setOpeningReservationId(null);
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
          {showReservationConflictBanner && (
            <p className="rounded-lg border border-warning/40 bg-warning/15 px-3 py-2 text-sm text-warning">
              {t('dashboard.reservationConflictBanner')}
            </p>
          )}
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
              {selectedCustomer && (selectedCustomer.outstandingDebtAmount ?? 0) > 0 && (
                <div className="rounded-lg border border-warning/40 bg-warning/15 px-3 py-2 text-sm text-warning">
                  {t('dashboard.outstandingDebtWarning', {
                    count: selectedCustomer.outstandingDebtCount ?? 0,
                    amount: formatCurrency(selectedCustomer.outstandingDebtAmount ?? 0),
                  })}
                </div>
              )}
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
                <span>{t('session.pricingPlan', { defaultValue: 'Pricing plan' })}</span>
                <span>{closeModal.pricingPlanName}</span>
              </div>
              {closeModal.timeUnit === TimeUnit.PerGame ? (
                <p className="text-xs text-muted">{t('session.matchCloseHint')}</p>
              ) : (
                <>
                  {(closeModal.appliedHourlyRate ?? 0) > 0 && (
                    <div className="flex justify-between text-sm text-muted">
                      <span>
                        {closeModal.appliedRateTier === 'Couple'
                          ? t('settings.couple')
                          : t('settings.individual')}{' '}
                        ({t('session.hourlyRate')})
                      </span>
                      <span>
                        {formatCurrency(closeModal.appliedHourlyRate!)}/{t('session.hoursShort')}
                      </span>
                    </div>
                  )}
                  <div className="flex justify-between text-sm text-muted">
                    <span>{t('session.timeCost')}</span>
                    <span>{formatCurrency(closeModal.currentTimeCost)}</span>
                  </div>
                </>
              )}
              {closeModal.accruedTimeCost > 0 && (
                <div className="flex justify-between text-sm text-muted">
                  <span>{t('dashboard.accrued')}</span>
                  <span>{formatCurrency(closeModal.accruedTimeCost)}</span>
                </div>
              )}
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
          {closeModal?.timeUnit === TimeUnit.PerGame && (
            <Input
              label={t('session.matchCount')}
              type="number"
              min={1}
              value={closeMatchCount}
              onChange={(e) => setCloseMatchCount(e.target.value)}
            />
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
          {cafSession &&
            !cafSession.customerId &&
            !cafSession.customerName &&
            !cafSession.quickGuestName && (
            <Input
              label={t('cafeteria.customerName')}
              value={cafCustomerName}
              onChange={(e) => setCafCustomerName(e.target.value)}
              placeholder={t('cafeteria.customerNameOptional')}
            />
          )}
          {(() => {
            const activeItems = cafItems.filter((i: CafeteriaItem) => i.isActive);
            const query = cafSearch.trim().toLowerCase();
            const results = activeItems.filter(
              (i) =>
                query === '' ||
                i.name.toLowerCase().includes(query) ||
                (i.nameAr ?? '').toLowerCase().includes(query)
            );
            const cartTotal = cafCart.reduce(
              (sum, l) =>
                sum + l.price * l.quantity + l.addOns.reduce((as, a) => as + a.price * a.quantity, 0),
              0
            );
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
                    const variants = (item.variants ?? []).filter((v) => v.isActive);
                    const sellAsIsNoRecipe =
                      item.kind === CafeteriaItemKind.SellAsIs &&
                      variants.every((v) => !variantHasRecipe(v));
                    const outOfStock = sellAsIsNoRecipe && item.currentQuantity <= 0;
                    const variantCount = variants.length;
                    return (
                      <button
                        key={item.id}
                        type="button"
                        disabled={outOfStock}
                        className="flex w-full items-center justify-between gap-2 rounded-md px-2 py-1.5 text-start hover:bg-surface-elevated disabled:cursor-not-allowed disabled:opacity-50"
                        onClick={() => {
                          if (variants.length === 0) {
                            setCafError(t('inventory.noVariants'));
                            return;
                          }
                          setCafPickItem(item);
                          setCafPickVariantId(variants[0].id);
                          setCafPickQty('1');
                          setCafPickStock('1');
                          setCafPickAddOns({});
                          setCafError('');
                        }}
                      >
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-sm font-medium">{item.name}</span>
                          <span className="block text-xs text-muted">
                            {variantCount} {t('inventory.variants', { defaultValue: 'variants' })}
                            {' · '}
                            {t('cafeteria.stock')}: {item.currentQuantity}
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

                {cafCart.length > 0 && (
                  <div className="space-y-2">
                    <p className="text-sm font-medium">{t('cafeteria.cart')}</p>
                    {cafCart.map((line) => (
                      <div key={line.variantId} className="flex flex-wrap items-center gap-2 rounded-lg border border-border px-3 py-2">
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-sm font-medium">
                            {line.itemName} — {line.variantName}
                          </p>
                          <p className="text-xs text-muted">
                            {formatCurrency(line.price)}
                            {needsManualStockDeduct({ kind: line.itemKind }, line.variant) && (
                              <>
                                {' · '}
                                {t('inventory.stockDeduct')} {line.stockDeduct}
                              </>
                            )}
                          </p>
                          {line.addOns.length > 0 && (
                            <p className="text-xs text-muted">
                              {line.addOns.map((a) => `+ ${a.name} ×${a.quantity}`).join(', ')}
                            </p>
                          )}
                        </div>
                        <div className="flex items-center gap-1">
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() =>
                              setCafCart((prev) =>
                                prev
                                  .map((l) => {
                                    if (l.variantId !== line.variantId) return l;
                                    const nextQty = Math.max(0, l.quantity - 1);
                                    const manual = needsManualStockDeduct({ kind: l.itemKind }, l.variant);
                                    const deductPer = l.quantity > 0 ? l.stockDeduct / l.quantity : 1;
                                    return {
                                      ...l,
                                      quantity: nextQty,
                                      stockDeduct: manual
                                        ? Math.max(nextQty > 0 ? Math.round(deductPer * nextQty) : 0, nextQty > 0 ? 1 : 0)
                                        : nextQty,
                                    };
                                  })
                                  .filter((l) => l.quantity > 0)
                              )
                            }
                          >
                            −
                          </Button>
                          <span className="w-8 text-center text-sm">{line.quantity}</span>
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() =>
                              setCafCart((prev) =>
                                prev.map((l) => {
                                  if (l.variantId !== line.variantId) return l;
                                  const nextQty = l.quantity + 1;
                                  const manual = needsManualStockDeduct({ kind: l.itemKind }, l.variant);
                                  const deductPer = l.quantity > 0 ? l.stockDeduct / l.quantity : 1;
                                  return {
                                    ...l,
                                    quantity: nextQty,
                                    stockDeduct: manual
                                      ? Math.max(Math.round(deductPer * nextQty), 1)
                                      : nextQty,
                                  };
                                })
                              )
                            }
                          >
                            +
                          </Button>
                        </div>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setCafCart((prev) => prev.filter((l) => l.variantId !== line.variantId))}
                        >
                          ✕
                        </Button>
                      </div>
                    ))}
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

      <Modal
        open={!!cafPickItem}
        onClose={() => setCafPickItem(null)}
        title={cafPickItem?.name ?? t('inventory.variant', { defaultValue: 'Variant' })}
        footer={
          <>
            <Button variant="secondary" onClick={() => setCafPickItem(null)}>
              {t('session.cancel')}
            </Button>
            <Button onClick={confirmCafPick}>{t('cafeteria.addToCart')}</Button>
          </>
        }
      >
        {cafPickItem && (() => {
          const pickVariant = (cafPickItem.variants ?? []).find((v) => v.id === cafPickVariantId);
          const showStock = pickVariant ? needsManualStockDeduct(cafPickItem, pickVariant) : false;
          return (
          <div className="space-y-3">
            <div>
              <label className="mb-1 block text-sm text-muted">
                {t('inventory.variant')}
              </label>
              <select
                className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                value={cafPickVariantId}
                onChange={(e) => {
                  setCafPickVariantId(e.target.value);
                  setCafPickStock(cafPickQty);
                }}
              >
                {(cafPickItem.variants ?? [])
                  .filter((v) => v.isActive)
                  .map((v) => (
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
              value={cafPickQty}
              onChange={(e) => {
                setCafPickQty(e.target.value);
                if (showStock) setCafPickStock(e.target.value);
              }}
            />
            {showStock && (
              <>
                <Input
                  label={t('inventory.stockDeduct')}
                  type="number"
                  min={1}
                  value={cafPickStock}
                  onChange={(e) => setCafPickStock(e.target.value)}
                />
                <p className="text-xs text-muted">
                  {t('inventory.stockAvailable')}: {cafPickItem.currentQuantity}
                </p>
              </>
            )}
            {cafAddOns.length > 0 && (
              <div className="space-y-2 border-t border-border pt-2">
                <p className="text-sm font-medium">{t('inventory.addOns')}</p>
                {cafAddOns.map((addon: CafeteriaAddOn) => (
                  <div key={addon.id} className="flex items-center justify-between gap-2">
                    <span className="text-sm">
                      {addon.name} (+{formatCurrency(addon.sellPrice)})
                    </span>
                    <Input
                      type="number"
                      min={0}
                      className="w-20"
                      value={cafPickAddOns[addon.id] ?? 0}
                      onChange={(e) =>
                        setCafPickAddOns((prev) => ({
                          ...prev,
                          [addon.id]: Math.max(0, Number(e.target.value) || 0),
                        }))
                      }
                    />
                  </div>
                ))}
              </div>
            )}
          </div>
          );
        })()}
      </Modal>

      <Modal
        open={!!cafMissingDialog}
        onClose={() => setCafMissingDialog(null)}
        title={t('inventory.missingIngredients')}
      >
        {cafMissingDialog && (
          <div className="space-y-3">
            <p className="text-sm text-muted">{t('inventory.skipMissingConfirm')}</p>
            <ul className="space-y-1 text-sm">
              {cafMissingDialog.missing.map((m) => (
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
              <Button variant="secondary" onClick={() => setCafMissingDialog(null)}>
                {t('session.cancel')}
              </Button>
              <Button
                loading={loading}
                onClick={async () => {
                  setLoading(true);
                  setCafError('');
                  try {
                    await cafMissingDialog.retry();
                  } catch (e) {
                    setCafError(e instanceof Error ? e.message : t('common.error'));
                  } finally {
                    setLoading(false);
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
