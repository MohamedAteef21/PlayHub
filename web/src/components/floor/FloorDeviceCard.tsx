import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icons';
import { formatCurrency, formatDuration, useLiveTimer } from '@/hooks/useSessions';
import { formatDateTimeEgypt } from '@/lib/dates';
import type { AssetDashboardDevice, DeviceReservation, SessionLive } from '@/types';
import { SessionMode, SessionStatus, TimeUnit } from '@/types';
import { deviceBucket, type DeviceBucket } from './floorHelpers';

function statusKey(status: string) {
  return status.toLowerCase() as 'idle' | 'gaming' | 'watching' | 'paused' | 'inactive';
}

function AttentionBanner({
  bucket,
  session,
  elapsed,
}: {
  bucket: DeviceBucket;
  session: SessionLive;
  elapsed: number;
}) {
  const { t } = useTranslation();
  if (session.plannedDurationMinutes == null) return null;
  const remainingSec = Math.max(0, session.plannedDurationMinutes * 60 - elapsed);
  const expired = bucket === 'timeup' || session.timeExpired || remainingSec <= 0;
  const endingSoon = bucket === 'ending' || (!expired && remainingSec > 0 && remainingSec <= 300);
  return (
    <div
      className={`rounded-md px-2 py-1 text-[11px] leading-snug ${
        expired
          ? 'animate-pulse bg-danger/15 font-semibold text-danger'
          : endingSoon
            ? 'animate-pulse bg-warning/15 font-semibold text-warning'
            : 'bg-primary/10 text-primary'
      }`}
    >
      {expired
        ? t('dashboard.timeUp')
        : endingSoon
          ? t('dashboard.endingSoon', { time: formatDuration(remainingSec) })
          : t('dashboard.remaining', { time: formatDuration(remainingSec) })}
      {' · '}
      {t('dashboard.bookedFor', {
        hours: (session.plannedDurationMinutes / 60).toFixed(
          session.plannedDurationMinutes % 60 === 0 ? 0 : 1
        ),
      })}
    </div>
  );
}

export function FloorDeviceCard({
  device,
  roomName,
  session,
  reservation,
  hideRoomName,
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
  onTransfer,
  canAddCafeteria,
  dateLocale,
}: {
  device: AssetDashboardDevice;
  roomName: string;
  session?: SessionLive;
  reservation?: DeviceReservation;
  hideRoomName?: boolean;
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
  onTransfer: () => void;
  canAddCafeteria: boolean;
  dateLocale: string;
}) {
  const { t } = useTranslation();
  const elapsed = useLiveTimer(session ?? null);
  const isActive = !!session && session.status !== SessionStatus.Closed;
  const deviceOffline = !device.isActive;
  const reservationGuestName = reservation ? (reservation.customerName ?? reservation.guestName) : null;
  const bucket = deviceBucket(device, session, reservation);
  const [moreOpen, setMoreOpen] = useState(
    bucket === 'timeup' || bucket === 'ending' || bucket === 'paused'
  );

  const liveStatus = deviceOffline
    ? 'Inactive'
    : session?.status === SessionStatus.Paused
      ? 'Paused'
      : device.liveStatus;

  return (
    <Card
      id={`floor-device-${device.id}`}
      hover={!isActive}
      className={`relative scroll-mt-44 overflow-hidden !p-3 transition-all ${
        deviceOffline
          ? 'border-danger/60 bg-danger/5 opacity-95'
          : bucket === 'timeup'
            ? 'border-danger/60 shadow-md shadow-danger/10'
            : bucket === 'ending'
              ? 'border-warning/50 shadow-md shadow-warning/10'
              : isActive
                ? 'border-primary/50 shadow-md shadow-primary/10'
                : reservation
                  ? 'border-warning/40'
                  : ''
      }`}
    >
      {isActive && !deviceOffline && (
        <div
          className={`absolute inset-x-0 top-0 h-0.5 ${
            bucket === 'timeup'
              ? 'bg-danger'
              : bucket === 'ending'
                ? 'bg-warning'
                : 'bg-gradient-to-r from-primary to-accent'
          }`}
        />
      )}
      {deviceOffline && <div className="absolute inset-x-0 top-0 h-0.5 bg-danger" />}

      <div className="mb-2 flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h3 className={`truncate text-sm font-semibold leading-tight ${deviceOffline ? 'text-danger' : 'text-text'}`}>
            {device.name}
          </h3>
          {!hideRoomName && <p className="truncate text-[11px] text-muted">{roomName}</p>}
        </div>
        <Badge status={statusKey(liveStatus)} pulse={liveStatus === 'Gaming' || liveStatus === 'Watching'}>
          {deviceOffline
            ? t('common.inactive')
            : reservation && !isActive
              ? t('dashboard.reserved')
              : t(`dashboard.${statusKey(liveStatus)}`, { defaultValue: liveStatus })}
        </Badge>
      </div>

      {deviceOffline ? (
        <p className="text-xs text-danger">{t('dashboard.deviceInactiveHint')}</p>
      ) : isActive && session ? (
        <div className="space-y-2">
          <div className="flex items-baseline justify-between gap-2">
            <span className="font-mono text-2xl font-bold tracking-tight text-accent tabular-nums">
              {formatDuration(elapsed)}
            </span>
            <span className="text-base font-semibold text-success tabular-nums">
              {formatCurrency(session.totalCost)}
            </span>
          </div>

          <AttentionBanner bucket={bucket} session={session} elapsed={elapsed} />

          {session.plannedDurationMinutes != null && session.status === SessionStatus.Open && (
            <div className="flex gap-1">
              <Button variant="secondary" size="sm" className="flex-1 !px-1 text-[11px]" onClick={() => onExtend(30)}>
                {t('dashboard.extendHalfHour')}
              </Button>
              <Button variant="secondary" size="sm" className="flex-1 !px-1 text-[11px]" onClick={() => onExtend(60)}>
                {t('dashboard.extendHour')}
              </Button>
              <Button variant="secondary" size="sm" className="flex-1 !px-1 text-[11px]" onClick={() => onExtend(null)}>
                {t('dashboard.makeOpenTime')}
              </Button>
            </div>
          )}

          {session.sessionMode === SessionMode.Gaming ? (
            <p className="truncate text-[11px] text-muted">
              {session.controllerCount} {t('dashboard.controllers')}
              {' · '}
              {session.pricingPlanName}
              {session.plannedDurationMinutes == null ? ` · ${t('dashboard.openTimer')}` : ''}
            </p>
          ) : (
            <div className="flex flex-wrap items-center gap-1 text-[11px] text-muted">
              <span>{t('dashboard.watchers')}:</span>
              <button
                type="button"
                className="flex h-7 w-7 items-center justify-center rounded-md border border-border bg-surface text-sm font-bold hover:bg-surface-hover disabled:opacity-40"
                disabled={(session.watcherCount ?? 1) <= 1}
                onClick={() => onWatchersChange((session.watcherCount ?? 2) - 1)}
              >
                −
              </button>
              <span className="min-w-5 text-center text-sm font-semibold text-text">{session.watcherCount}</span>
              <button
                type="button"
                className="flex h-7 w-7 items-center justify-center rounded-md border border-border bg-surface text-sm font-bold hover:bg-surface-hover disabled:opacity-40"
                disabled={(session.watcherCount ?? 0) >= device.maxWatchingCapacity}
                onClick={() => onWatchersChange((session.watcherCount ?? 0) + 1)}
              >
                +
              </button>
              <span className="truncate">· {session.pricingPlanName}</span>
            </div>
          )}

          <div className="flex gap-1.5">
            {session.status === SessionStatus.Open ? (
              <Button variant="secondary" size="sm" className="flex-1" onClick={onPause}>
                {t('session.pause')}
              </Button>
            ) : (
              <Button variant="secondary" size="sm" className="flex-1" onClick={onResume}>
                {t('session.resume')}
              </Button>
            )}
            <Button variant="danger" size="sm" className="flex-1" onClick={onClose}>
              {t('session.close')}
            </Button>
          </div>

          <div className="grid grid-cols-2 gap-1.5">
            {canAddCafeteria && (
              <Button variant="primary" size="sm" className="!px-2" onClick={onAddCafeteria}>
                <Icon name="cafeteria" className="h-3.5 w-3.5" />
                {session.cafeteriaCost > 0 ? formatCurrency(session.cafeteriaCost) : t('dashboard.addCafeteria')}
              </Button>
            )}
            <Button variant="secondary" size="sm" className="!px-2" onClick={onPreviewBill}>
              <Icon name="print" className="h-3.5 w-3.5" />
              {t('dashboard.previewBill')}
            </Button>
          </div>

          <button
            type="button"
            className="w-full text-[11px] font-medium text-muted underline-offset-2 hover:text-text hover:underline"
            onClick={() => setMoreOpen((v) => !v)}
          >
            {moreOpen ? t('dashboard.lessActions') : t('dashboard.moreActions')}
          </button>

          {moreOpen && (
            <div className="space-y-1.5 border-t border-border/60 pt-2">
              {(session.cafeteriaCost > 0 || session.currentTimeCost > 0 || (session.appliedHourlyRate ?? 0) > 0) && (
                <p className="text-[11px] leading-snug text-muted">
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
              <div className="grid grid-cols-2 gap-1.5">
                <Button variant="secondary" size="sm" className="!px-2" onClick={onTransfer}>
                  <Icon name="arrow" className="h-3.5 w-3.5" />
                  {t('dashboard.transfer')}
                </Button>
                {session.canChangePricing && (
                  <Button variant="secondary" size="sm" className="!px-2" onClick={onConvert}>
                    <Icon name="settings" className="h-3.5 w-3.5" />
                    {t('dashboard.changePricing')}
                  </Button>
                )}
              </div>
              {session.timeUnit === TimeUnit.PerGame && (
                <p className="text-[11px] text-muted">{t('dashboard.perMatchHint')}</p>
              )}
            </div>
          )}
        </div>
      ) : (
        <div className="space-y-2">
          <p className="text-[11px] text-muted">
            {device.workingControllers} ctrl · {device.maxWatchingCapacity} watch
          </p>
          {reservation ? (
            <>
              <div className="rounded-md border border-warning/40 bg-warning/10 px-2 py-1.5 text-[11px] text-warning">
                <p className="font-medium">
                  {t('dashboard.reservationStarts', {
                    time: formatDateTimeEgypt(reservation.startsAt, dateLocale),
                  })}
                </p>
                {reservation.endsAt && (
                  <p>
                    {t('dashboard.reservationEnds', {
                      time: formatDateTimeEgypt(reservation.endsAt, dateLocale),
                    })}
                  </p>
                )}
                {reservationGuestName && (
                  <p className="truncate">{t('dashboard.reservationGuest', { name: reservationGuestName })}</p>
                )}
                <p className="mt-1 text-[10px] opacity-90">{t('dashboard.startReservationHint')}</p>
              </div>
              <div className="grid grid-cols-2 gap-1.5">
                <Button size="sm" className="col-span-2 min-h-10" onClick={onStartReservation}>
                  <Icon name="play" className="h-3.5 w-3.5" />
                  {t('dashboard.startReservation')}
                </Button>
                <Button variant="secondary" size="sm" onClick={onCancelReservation}>
                  {t('dashboard.cancelReservation')}
                </Button>
                <Button variant="secondary" size="sm" onClick={onOpen}>
                  <Icon name="play" className="h-3.5 w-3.5" />
                  {t('dashboard.openSession')}
                </Button>
              </div>
            </>
          ) : (
            <div className="grid grid-cols-2 gap-1.5">
              <Button size="sm" className="col-span-2 min-h-10 text-sm" onClick={onOpen}>
                <Icon name="play" className="h-4 w-4" />
                {t('dashboard.openSession')}
              </Button>
              <Button variant="secondary" size="sm" className="col-span-2" onClick={onReserve}>
                <Icon name="clock" className="h-3.5 w-3.5" />
                {t('dashboard.reserve')}
              </Button>
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
