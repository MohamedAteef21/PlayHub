import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { platformApi } from '@/api/client';
import type { NotificationTarget } from '@/types';
import { NotificationChannel } from '@/types';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Input } from '@/components/ui/Input';
import { PageHeader } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';

type TargetDraft = {
  allowedChannels: number;
  notifyLowStock: boolean;
  notifySubscription: boolean;
  notifyDeviceMaintenance: boolean;
  alertRecipientEmail: string;
  ownerWhatsAppPhone: string;
};

function toDraft(row: NotificationTarget): TargetDraft {
  return {
    allowedChannels: row.allowedChannels,
    notifyLowStock: row.notifyLowStock,
    notifySubscription: row.notifySubscription,
    notifyDeviceMaintenance: row.notifyDeviceMaintenance,
    alertRecipientEmail: row.alertRecipientEmail ?? '',
    ownerWhatsAppPhone: row.ownerWhatsAppPhone ?? '',
  };
}

export function SuperAdminSettingsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [platformSmtp, setPlatformSmtp] = useState('');
  const [platformSmtpPassword, setPlatformSmtpPassword] = useState('');
  const [platformSenderName, setPlatformSenderName] = useState('PlayHub System');
  const [platformMsg, setPlatformMsg] = useState('');
  const [platformError, setPlatformError] = useState('');
  const [drafts, setDrafts] = useState<Record<string, TargetDraft>>({});
  const [rowMsg, setRowMsg] = useState<Record<string, string>>({});
  const [rowError, setRowError] = useState<Record<string, string>>({});
  const [savingUserId, setSavingUserId] = useState<string | null>(null);

  const { data: platformSettings } = useQuery({
    queryKey: ['platform-alert-settings'],
    queryFn: platformApi.getAlertSettings,
  });

  const { data: targets = [], isLoading: targetsLoading } = useQuery({
    queryKey: ['platform-notification-targets'],
    queryFn: platformApi.getNotificationTargets,
  });

  useEffect(() => {
    if (!platformSettings) return;
    setPlatformSmtp(platformSettings.smtpUsername || '');
    setPlatformSenderName(platformSettings.senderDisplayName || 'PlayHub System');
    setPlatformSmtpPassword('');
  }, [platformSettings]);

  useEffect(() => {
    const next: Record<string, TargetDraft> = {};
    for (const row of targets) {
      next[row.userId] = toDraft(row);
    }
    setDrafts(next);
  }, [targets]);

  const savePlatformMutation = useMutation({
    mutationFn: () =>
      platformApi.upsertAlertSettings({
        smtpUsername: platformSmtp.trim() || null,
        smtpPassword: platformSmtpPassword.trim() || null,
        senderDisplayName: platformSenderName.trim() || 'PlayHub System',
        whatsAppIntegrationEnabled: false,
      }),
    onSuccess: () => {
      setPlatformMsg(t('superAdmin.settingsSaved'));
      setPlatformError('');
      setPlatformSmtpPassword('');
      queryClient.invalidateQueries({ queryKey: ['platform-alert-settings'] });
    },
    onError: (err: Error) => {
      setPlatformError(err.message || t('common.error'));
      setPlatformMsg('');
    },
  });

  const testPlatformEmailMutation = useMutation({
    mutationFn: () => platformApi.testEmail(),
    onSuccess: () => {
      setPlatformMsg(t('superAdmin.testEmailSent'));
      setPlatformError('');
    },
    onError: (err: Error) => {
      setPlatformError(err.message || t('common.error'));
      setPlatformMsg('');
    },
  });

  async function saveTarget(userId: string) {
    const draft = drafts[userId];
    if (!draft) return;
    setSavingUserId(userId);
    setRowMsg((m) => ({ ...m, [userId]: '' }));
    setRowError((m) => ({ ...m, [userId]: '' }));
    try {
      await platformApi.upsertNotificationTarget(userId, {
        allowedChannels: draft.allowedChannels,
        notifyLowStock: draft.notifyLowStock,
        notifySubscription: draft.notifySubscription,
        notifyDeviceMaintenance: draft.notifyDeviceMaintenance,
        alertRecipientEmail: draft.alertRecipientEmail.trim() || null,
        ownerWhatsAppPhone: draft.ownerWhatsAppPhone.trim() || null,
      });
      setRowMsg((m) => ({ ...m, [userId]: t('superAdmin.targetSaved') }));
      queryClient.invalidateQueries({ queryKey: ['platform-notification-targets'] });
    } catch (err) {
      setRowError((m) => ({
        ...m,
        [userId]: err instanceof Error ? err.message : t('common.error'),
      }));
    } finally {
      setSavingUserId(null);
    }
  }

  function patchDraft(userId: string, patch: Partial<TargetDraft>) {
    setDrafts((prev) => {
      const current = prev[userId];
      if (!current) return prev;
      return { ...prev, [userId]: { ...current, ...patch } };
    });
  }

  return (
    <div>
      <PageHeader title={t('nav.settings')} />
      <p className="mb-6 max-w-2xl text-sm text-muted">{t('superAdmin.settingsPageHint')}</p>

      <Card className="mb-6 space-y-4">
        <div>
          <h2 className="text-base font-semibold">{t('superAdmin.platformSettings')}</h2>
          <p className="mt-1 text-sm text-muted">{t('superAdmin.platformSettingsHint')}</p>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <Input
            label={t('superAdmin.gmail')}
            type="email"
            value={platformSmtp}
            onChange={(e) => setPlatformSmtp(e.target.value)}
            placeholder="alerts@gmail.com"
            dir="ltr"
          />
          <Input
            label={t('superAdmin.gmailAppPassword')}
            type="password"
            value={platformSmtpPassword}
            onChange={(e) => setPlatformSmtpPassword(e.target.value)}
            placeholder={
              platformSettings?.hasSmtpPassword
                ? t('superAdmin.passwordKeep')
                : 'App Password'
            }
            dir="ltr"
          />
          <Input
            label={t('superAdmin.senderName')}
            value={platformSenderName}
            onChange={(e) => setPlatformSenderName(e.target.value)}
          />
        </div>

        <div className="rounded-xl border border-border bg-surface/50 px-4 py-3">
          <p className="text-sm font-medium">{t('superAdmin.whatsappTitle')}</p>
          <p className="mt-1 text-sm text-muted">{t('superAdmin.whatsappComingSoon')}</p>
        </div>

        {(platformMsg || platformError) && (
          <p className={`text-sm ${platformError ? 'text-danger' : 'text-success'}`}>
            {platformError || platformMsg}
          </p>
        )}

        <div className="flex flex-wrap gap-2">
          <Button
            loading={savePlatformMutation.isPending}
            onClick={() => savePlatformMutation.mutate()}
          >
            {t('common.save')}
          </Button>
          <Button
            variant="secondary"
            loading={testPlatformEmailMutation.isPending}
            onClick={() => testPlatformEmailMutation.mutate()}
          >
            {t('superAdmin.testEmail')}
          </Button>
        </div>
      </Card>

      <Card className="space-y-4">
        <div>
          <h2 className="text-base font-semibold">{t('superAdmin.notificationTargets')}</h2>
          <p className="mt-1 text-sm text-muted">{t('superAdmin.notificationTargetsHint')}</p>
        </div>

        {targetsLoading ? (
          <PageLoader />
        ) : targets.length === 0 ? (
          <p className="text-sm text-muted">{t('superAdmin.noTargets')}</p>
        ) : (
          <div className="space-y-4">
            {targets.map((row) => {
              const draft = drafts[row.userId] ?? toDraft(row);
              return (
                <div
                  key={row.userId}
                  className="rounded-xl border border-border bg-surface/40 p-4 space-y-3"
                >
                  <div className="flex flex-wrap items-baseline justify-between gap-2">
                    <div>
                      <p className="font-medium">{row.fullName || row.username}</p>
                      <p className="text-sm text-muted" dir="ltr">
                        {row.username}
                      </p>
                    </div>
                    <label className="flex flex-col gap-1 text-sm">
                      <span className="text-muted">{t('superAdmin.channel')}</span>
                      <select
                        className="rounded-lg border border-border bg-bg px-3 py-2"
                        value={draft.allowedChannels}
                        onChange={(e) =>
                          patchDraft(row.userId, { allowedChannels: Number(e.target.value) })
                        }
                      >
                        <option value={NotificationChannel.None}>{t('superAdmin.channelNone')}</option>
                        <option value={NotificationChannel.Email}>{t('superAdmin.channelEmail')}</option>
                        <option value={NotificationChannel.WhatsApp}>
                          {t('superAdmin.channelWhatsApp')}
                        </option>
                        <option value={NotificationChannel.EmailAndWhatsApp}>
                          {t('superAdmin.channelBoth')}
                        </option>
                      </select>
                    </label>
                  </div>

                  <div className="flex flex-wrap gap-4 text-sm">
                    <label className="flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={draft.notifyLowStock}
                        onChange={(e) =>
                          patchDraft(row.userId, { notifyLowStock: e.target.checked })
                        }
                      />
                      {t('superAdmin.notifyLowStock')}
                    </label>
                    <label className="flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={draft.notifySubscription}
                        onChange={(e) =>
                          patchDraft(row.userId, { notifySubscription: e.target.checked })
                        }
                      />
                      {t('superAdmin.notifySubscription')}
                    </label>
                    <label className="flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={draft.notifyDeviceMaintenance}
                        onChange={(e) =>
                          patchDraft(row.userId, { notifyDeviceMaintenance: e.target.checked })
                        }
                      />
                      {t('superAdmin.notifyDeviceMaintenance')}
                    </label>
                  </div>

                  <div className="grid gap-3 sm:grid-cols-2">
                    <Input
                      label={t('superAdmin.recipientEmail')}
                      type="email"
                      value={draft.alertRecipientEmail}
                      onChange={(e) =>
                        patchDraft(row.userId, { alertRecipientEmail: e.target.value })
                      }
                      placeholder={row.username}
                      dir="ltr"
                    />
                    <Input
                      label={t('superAdmin.whatsappPhone')}
                      value={draft.ownerWhatsAppPhone}
                      onChange={(e) =>
                        patchDraft(row.userId, { ownerWhatsAppPhone: e.target.value })
                      }
                      placeholder="2010xxxxxxx"
                      dir="ltr"
                    />
                  </div>

                  {(rowMsg[row.userId] || rowError[row.userId]) && (
                    <p
                      className={`text-sm ${
                        rowError[row.userId] ? 'text-danger' : 'text-success'
                      }`}
                    >
                      {rowError[row.userId] || rowMsg[row.userId]}
                    </p>
                  )}

                  <Button
                    size="sm"
                    loading={savingUserId === row.userId}
                    onClick={() => void saveTarget(row.userId)}
                  >
                    {t('common.save')}
                  </Button>
                </div>
              );
            })}
          </div>
        )}
      </Card>
    </div>
  );
}
