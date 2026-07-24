import { useTranslation } from 'react-i18next';
import { formatCurrency, formatDuration, useLiveTimer } from '@/hooks/useSessions';
import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icons';
import type { SessionLive } from '@/types';
import { SessionStatus } from '@/types';

function sessionUrgency(session: SessionLive, elapsed: number): 'timeup' | 'ending' | 'paused' | 'active' {
  if (session.status === SessionStatus.Paused) return 'paused';
  if (session.plannedDurationMinutes != null) {
    const remaining = session.plannedDurationMinutes * 60 - elapsed;
    if (session.timeExpired || remaining <= 0) return 'timeup';
    if (remaining <= 300) return 'ending';
  }
  return 'active';
}

function ActiveChip({
  session,
  canAddCafeteria,
  onJump,
  onCafeteria,
  onClose,
}: {
  session: SessionLive;
  canAddCafeteria: boolean;
  onJump: () => void;
  onCafeteria: () => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const elapsed = useLiveTimer(session);
  const bucket = sessionUrgency(session, elapsed);
  const urgent = bucket === 'timeup' || bucket === 'ending' || bucket === 'paused';

  return (
    <div
      className={`flex min-w-[11.5rem] max-w-[16rem] shrink-0 items-center gap-2 rounded-lg border px-2.5 py-1.5 ${
        bucket === 'timeup'
          ? 'border-danger/50 bg-danger/10'
          : bucket === 'ending'
            ? 'border-warning/50 bg-warning/10'
            : bucket === 'paused'
              ? 'border-border bg-surface'
              : 'border-primary/30 bg-primary/5'
      }`}
    >
      <button type="button" className="min-w-0 flex-1 text-start" onClick={onJump}>
        <p className="truncate text-xs font-semibold text-text">{session.deviceName}</p>
        <p className="flex items-center gap-1.5 font-mono text-[11px] tabular-nums text-muted">
          <span className={urgent ? 'font-semibold text-warning' : 'text-accent'}>{formatDuration(elapsed)}</span>
          <span>·</span>
          <span className="text-success">{formatCurrency(session.totalCost)}</span>
        </p>
      </button>
      <div className="flex shrink-0 gap-1">
        {canAddCafeteria && (
          <Button
            size="sm"
            variant="secondary"
            className="!h-8 !w-8 !p-0"
            title={t('dashboard.addCafeteria')}
            onClick={onCafeteria}
          >
            <Icon name="cafeteria" className="h-3.5 w-3.5" />
          </Button>
        )}
        <Button
          size="sm"
          variant="danger"
          className="!h-8 !w-8 !p-0"
          title={t('session.close')}
          onClick={onClose}
        >
          <Icon name="stop" className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}

export function FloorActiveStrip({
  sessions,
  canAddCafeteria,
  onJump,
  onCafeteria,
  onClose,
}: {
  sessions: SessionLive[];
  canAddCafeteria: boolean;
  onJump: (session: SessionLive) => void;
  onCafeteria: (session: SessionLive) => void;
  onClose: (session: SessionLive) => void;
}) {
  const { t } = useTranslation();
  if (sessions.length === 0) return null;

  return (
    <div className="rounded-xl border border-border bg-surface-elevated/90 px-3 py-2 backdrop-blur">
      <div className="mb-1.5 flex items-center justify-between gap-2">
        <p className="text-xs font-semibold text-text">
          {t('dashboard.activeStrip', { count: sessions.length })}
        </p>
        <p className="text-[11px] text-muted">{t('dashboard.activeStripHint')}</p>
      </div>
      <div className="flex gap-2 overflow-x-auto pb-0.5 [-ms-overflow-style:none] [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {sessions.map((s) => (
          <ActiveChip
            key={s.id}
            session={s}
            canAddCafeteria={canAddCafeteria}
            onJump={() => onJump(s)}
            onCafeteria={() => onCafeteria(s)}
            onClose={() => onClose(s)}
          />
        ))}
      </div>
    </div>
  );
}
