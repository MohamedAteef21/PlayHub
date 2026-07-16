import { useEffect, useState } from 'react';
import { useIsFetching, useIsMutating } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

/** Full-viewport neon loader with blurred page underneath. */
export function GlobalBusyOverlay() {
  const { t } = useTranslation();
  const fetching = useIsFetching({
    predicate: (q) => q.meta?.silent !== true,
  });
  const mutating = useIsMutating();
  const busy = fetching > 0 || mutating > 0;
  const [show, setShow] = useState(false);

  useEffect(() => {
    if (!busy) {
      setShow(false);
      return;
    }
    const timer = window.setTimeout(() => setShow(true), 100);
    return () => window.clearTimeout(timer);
  }, [busy]);

  if (!show) return null;

  return (
    <div
      className="ph-busy-overlay fixed inset-0 z-[100] flex items-center justify-center animate-fade-in"
      role="status"
      aria-live="polite"
      aria-busy="true"
    >
      <div className="ph-busy-glow" aria-hidden />
      <div className="relative z-10 flex flex-col items-center gap-5 animate-pop-in">
        <div className="ph-neon-ring" aria-hidden>
          <span className="ph-neon-core" />
        </div>
        <div className="text-center">
          <p className="text-sm font-semibold tracking-wide text-text drop-shadow-[0_0_12px_rgba(99,102,241,0.55)]">
            {t('common.pleaseWait')}
          </p>
          <p className="mt-1 text-xs text-muted">{t('common.loadingHint')}</p>
        </div>
      </div>
    </div>
  );
}
