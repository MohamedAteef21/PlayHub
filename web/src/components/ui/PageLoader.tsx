import { useTranslation } from 'react-i18next';

export function PageLoader({ label }: { label?: string }) {
  const { t } = useTranslation();
  return (
    <div className="flex min-h-[12rem] flex-col items-center justify-center gap-3 animate-fade-in">
      <span className="ph-loader" />
      <p className="text-sm text-muted">{label ?? t('common.loading')}</p>
    </div>
  );
}
