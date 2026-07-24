import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { assetsApi, cafeteriaApi, loyaltyOffersApi } from '@/api/client';
import {
  LoyaltyConditionMetric,
  LoyaltyConditionLogic,
  LoyaltyFulfillment,
  LoyaltyPlayerScope,
  LoyaltyRewardMetric,
  type LoyaltyOffer,
  type UpsertLoyaltyOfferConditionRequest,
  type UpsertLoyaltyOfferRequest,
  type UpsertLoyaltyOfferRewardRequest,
} from '@/types';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { DataTable } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';

type CondDraft = UpsertLoyaltyOfferConditionRequest;
type RewardDraft = UpsertLoyaltyOfferRewardRequest;

const emptyCond = (): CondDraft => ({
  metric: LoyaltyConditionMetric.PlayHours,
  requiredQuantity: 2,
  windowDays: null,
  cafeteriaItemId: null,
  variantId: null,
});

const emptyReward = (): RewardDraft => ({
  metric: LoyaltyRewardMetric.FreeHours,
  quantity: 1,
  cafeteriaItemId: null,
  variantId: null,
});

function toLocalInput(iso?: string | null) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function fromLocalInput(v: string): string | null {
  if (!v.trim()) return null;
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

export function LoyaltyOffersPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const [open, setOpen] = useState(false);
  const [edit, setEdit] = useState<LoyaltyOffer | null>(null);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [startsAt, setStartsAt] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [playerScope, setPlayerScope] = useState<number>(LoyaltyPlayerScope.Any);
  const [fulfillment, setFulfillment] = useState<number>(LoyaltyFulfillment.EarnCredit);
  const [conditionLogic, setConditionLogic] = useState<number>(LoyaltyConditionLogic.All);
  const [conditions, setConditions] = useState<CondDraft[]>([emptyCond()]);
  const [rewards, setRewards] = useState<RewardDraft[]>([emptyReward()]);
  const [deviceIds, setDeviceIds] = useState<string[]>([]);

  const { data: offers = [], isLoading } = useQuery({
    queryKey: ['loyalty-offers'],
    queryFn: () => loyaltyOffersApi.getAll(),
  });

  const { data: devices = [] } = useQuery({
    queryKey: ['devices-all'],
    queryFn: () => assetsApi.getDevices(),
    enabled: open,
  });

  const { data: cafItems = [] } = useQuery({
    queryKey: ['cafeteria-items', 'loyalty'],
    queryFn: () => cafeteriaApi.getItems({ forSaleOnly: true }),
    enabled: open,
  });

  const sellItems = useMemo(
    () => cafItems.filter((i) => i.isActive && i.kind !== 1),
    [cafItems]
  );

  function resetForm() {
    setEdit(null);
    setTitle('');
    setDescription('');
    setIsActive(true);
    setStartsAt('');
    setEndsAt('');
    setPlayerScope(LoyaltyPlayerScope.Any);
    setFulfillment(LoyaltyFulfillment.EarnCredit);
    setConditionLogic(LoyaltyConditionLogic.All);
    setConditions([emptyCond()]);
    setRewards([emptyReward()]);
    setDeviceIds([]);
    setError('');
  }

  function openCreate() {
    resetForm();
    setOpen(true);
  }

  function openEdit(o: LoyaltyOffer) {
    setEdit(o);
    setTitle(o.title);
    setDescription(o.description ?? '');
    setIsActive(o.isActive);
    setStartsAt(toLocalInput(o.startsAt));
    setEndsAt(toLocalInput(o.endsAt));
    setPlayerScope(o.playerScope);
    setFulfillment(o.fulfillment);
    setConditionLogic(o.conditionLogic);
    setConditions(
      o.conditions.length
        ? o.conditions.map((c) => ({
            metric: c.metric,
            requiredQuantity: c.requiredQuantity,
            windowDays: c.windowDays,
            cafeteriaItemId: c.cafeteriaItemId,
            variantId: c.variantId,
          }))
        : [emptyCond()]
    );
    setRewards(
      o.rewards.length
        ? o.rewards.map((r) => ({
            metric: r.metric,
            quantity: r.quantity,
            cafeteriaItemId: r.cafeteriaItemId,
            variantId: r.variantId,
          }))
        : [emptyReward()]
    );
    setDeviceIds([...o.deviceIds]);
    setError('');
    setOpen(true);
  }

  function buildPayload(): UpsertLoyaltyOfferRequest {
    return {
      title: title.trim(),
      description: description.trim() || null,
      isActive,
      startsAt: fromLocalInput(startsAt),
      endsAt: fromLocalInput(endsAt),
      playerScope,
      fulfillment,
      conditionLogic,
      conditions: conditions.map((c) => ({
        ...c,
        requiredQuantity: Number(c.requiredQuantity) || 0,
        windowDays:
          c.metric === LoyaltyConditionMetric.PlayHoursInDays
            ? Number(c.windowDays) || 7
            : null,
        cafeteriaItemId:
          c.metric === LoyaltyConditionMetric.CafeteriaQuantity ? c.cafeteriaItemId : null,
        variantId: c.metric === LoyaltyConditionMetric.CafeteriaQuantity ? c.variantId : null,
      })),
      rewards: rewards.map((r) => ({
        ...r,
        quantity: Number(r.quantity) || 0,
        cafeteriaItemId: r.metric === LoyaltyRewardMetric.CafeteriaItem ? r.cafeteriaItemId : null,
        variantId: r.metric === LoyaltyRewardMetric.CafeteriaItem ? r.variantId : null,
      })),
      deviceIds,
    };
  }

  const saveMutation = useMutation({
    mutationFn: () =>
      edit ? loyaltyOffersApi.update(edit.id, buildPayload()) : loyaltyOffersApi.create(buildPayload()),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['loyalty-offers'] });
      setOpen(false);
      resetForm();
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => loyaltyOffersApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['loyalty-offers'] }),
    onError: (e: Error) => setError(e.message),
  });

  function metricCondLabel(m: number) {
    switch (m) {
      case LoyaltyConditionMetric.PlayHours:
        return t('loyalty.condPlayHours');
      case LoyaltyConditionMetric.PlayHoursInDays:
        return t('loyalty.condPlayHoursInDays');
      case LoyaltyConditionMetric.Matches:
        return t('loyalty.condMatches');
      case LoyaltyConditionMetric.CafeteriaQuantity:
        return t('loyalty.condCafeteria');
      default:
        return String(m);
    }
  }

  function metricRewardLabel(m: number) {
    switch (m) {
      case LoyaltyRewardMetric.FreeHours:
        return t('loyalty.rewardFreeHours');
      case LoyaltyRewardMetric.FreeMatches:
        return t('loyalty.rewardFreeMatches');
      case LoyaltyRewardMetric.CafeteriaItem:
        return t('loyalty.rewardCafeteria');
      default:
        return String(m);
    }
  }

  function scopeLabel(s: number) {
    if (s === LoyaltyPlayerScope.Individual) return t('loyalty.scopeIndividual');
    if (s === LoyaltyPlayerScope.Couple) return t('loyalty.scopeCouple');
    return t('loyalty.scopeAny');
  }

  if (isLoading) return <PageLoader />;

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-2xl text-sm text-muted">{t('loyalty.hint')}</p>
        {canManage && (
          <Button onClick={openCreate}>
            <Icon name="plus" className="h-4 w-4" />
            {t('loyalty.add')}
          </Button>
        )}
      </div>

      {error && !open && <p className="mb-3 text-sm text-danger">{error}</p>}

      <DataTable
        headers={[
          t('offers.title'),
          t('loyalty.playerScope'),
          t('loyalty.fulfillment'),
          t('loyalty.conditions'),
          t('loyalty.rewards'),
          t('common.status'),
          t('customers.actions'),
        ]}
      >
        {offers.length === 0 ? (
          <tr>
            <td colSpan={7} className="px-4 py-8 text-center text-muted">
              {t('loyalty.empty')}
            </td>
          </tr>
        ) : (
          offers.map((o) => (
            <tr key={o.id} className="hover:bg-surface-hover/50">
              <td className="px-4 py-3">
                <p className="font-medium">{o.title}</p>
                {o.description && <p className="text-xs text-muted line-clamp-1">{o.description}</p>}
              </td>
              <td className="px-4 py-3 text-sm">{scopeLabel(o.playerScope)}</td>
              <td className="px-4 py-3 text-sm">
                {o.fulfillment === LoyaltyFulfillment.ApplyNow
                  ? t('loyalty.fulfillApplyNow')
                  : t('loyalty.fulfillEarnCredit')}
              </td>
              <td className="px-4 py-3 text-xs text-muted">
                {o.conditions.map((c) => `${metricCondLabel(c.metric)} ≥ ${c.requiredQuantity}`).join(' · ') ||
                  '—'}
              </td>
              <td className="px-4 py-3 text-xs text-muted">
                {o.rewards.map((r) => `${r.quantity} ${metricRewardLabel(r.metric)}`).join(' · ') || '—'}
              </td>
              <td className="px-4 py-3">
                <Badge status={o.isActive ? 'gaming' : 'idle'}>
                  {o.isActive ? t('common.active') : t('common.inactive')}
                </Badge>
              </td>
              <td className="px-4 py-3">
                {canManage && (
                  <div className="flex flex-wrap gap-1">
                    <Button size="sm" variant="secondary" onClick={() => openEdit(o)}>
                      {t('customers.edit')}
                    </Button>
                    <Button
                      size="sm"
                      variant="danger"
                      onClick={() => {
                        if (window.confirm(t('common.confirmDelete'))) deleteMutation.mutate(o.id);
                      }}
                    >
                      {t('common.delete')}
                    </Button>
                  </div>
                )}
              </td>
            </tr>
          ))
        )}
      </DataTable>

      <Modal
        open={open}
        onClose={() => {
          setOpen(false);
          resetForm();
        }}
        size="xl"
        title={edit ? t('loyalty.edit') : t('loyalty.add')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setOpen(false);
                resetForm();
              }}
            >
              {t('session.cancel')}
            </Button>
            <Button
              loading={saveMutation.isPending}
              disabled={!title.trim() || conditions.length === 0 || rewards.length === 0}
              onClick={() => saveMutation.mutate()}
            >
              {t('common.save')}
            </Button>
          </>
        }
      >
        <div className="max-h-[70vh] space-y-4 overflow-y-auto pe-1">
          <Input label={t('offers.title')} value={title} onChange={(e) => setTitle(e.target.value)} required />
          <Input
            label={t('loyalty.description')}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
          <div className="grid gap-3 sm:grid-cols-2">
            <Input
              label={t('loyalty.startsAt')}
              type="datetime-local"
              value={startsAt}
              onChange={(e) => setStartsAt(e.target.value)}
            />
            <Input
              label={t('loyalty.endsAt')}
              type="datetime-local"
              value={endsAt}
              onChange={(e) => setEndsAt(e.target.value)}
            />
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <label className="mb-1 block text-sm text-muted">{t('loyalty.playerScope')}</label>
              <select
                className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                value={playerScope}
                onChange={(e) => setPlayerScope(Number(e.target.value))}
              >
                <option value={LoyaltyPlayerScope.Any}>{t('loyalty.scopeAny')}</option>
                <option value={LoyaltyPlayerScope.Individual}>{t('loyalty.scopeIndividual')}</option>
                <option value={LoyaltyPlayerScope.Couple}>{t('loyalty.scopeCouple')}</option>
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm text-muted">{t('loyalty.fulfillment')}</label>
              <select
                className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                value={fulfillment}
                onChange={(e) => setFulfillment(Number(e.target.value))}
              >
                <option value={LoyaltyFulfillment.EarnCredit}>{t('loyalty.fulfillEarnCredit')}</option>
                <option value={LoyaltyFulfillment.ApplyNow}>{t('loyalty.fulfillApplyNow')}</option>
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm text-muted">{t('loyalty.conditionLogic')}</label>
              <select
                className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
                value={conditionLogic}
                onChange={(e) => setConditionLogic(Number(e.target.value))}
              >
                <option value={LoyaltyConditionLogic.All}>{t('loyalty.logicAll')}</option>
                <option value={LoyaltyConditionLogic.Any}>{t('loyalty.logicAny')}</option>
              </select>
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            {t('common.active')}
          </label>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <p className="font-medium">{t('loyalty.conditions')}</p>
              <Button
                size="sm"
                variant="secondary"
                onClick={() => setConditions((prev) => [...prev, emptyCond()])}
              >
                {t('loyalty.addCondition')}
              </Button>
            </div>
            {conditions.map((c, idx) => (
              <div key={idx} className="grid gap-2 rounded-lg border border-border p-3 sm:grid-cols-4">
                <div>
                  <label className="mb-1 block text-xs text-muted">{t('loyalty.metric')}</label>
                  <select
                    className="w-full rounded-lg border border-border bg-surface-elevated px-2 py-1.5 text-sm"
                    value={c.metric}
                    onChange={(e) => {
                      const metric = Number(e.target.value);
                      setConditions((prev) =>
                        prev.map((x, i) => (i === idx ? { ...x, metric, windowDays: metric === 2 ? 7 : null } : x))
                      );
                    }}
                  >
                    <option value={LoyaltyConditionMetric.PlayHours}>{t('loyalty.condPlayHours')}</option>
                    <option value={LoyaltyConditionMetric.PlayHoursInDays}>
                      {t('loyalty.condPlayHoursInDays')}
                    </option>
                    <option value={LoyaltyConditionMetric.Matches}>{t('loyalty.condMatches')}</option>
                    <option value={LoyaltyConditionMetric.CafeteriaQuantity}>
                      {t('loyalty.condCafeteria')}
                    </option>
                  </select>
                </div>
                <Input
                  label={t('loyalty.requiredQty')}
                  type="number"
                  min={0.1}
                  step={0.5}
                  value={c.requiredQuantity}
                  onChange={(e) =>
                    setConditions((prev) =>
                      prev.map((x, i) =>
                        i === idx ? { ...x, requiredQuantity: Number(e.target.value) || 0 } : x
                      )
                    )
                  }
                />
                {c.metric === LoyaltyConditionMetric.PlayHoursInDays && (
                  <Input
                    label={t('loyalty.windowDays')}
                    type="number"
                    min={1}
                    value={c.windowDays ?? 7}
                    onChange={(e) =>
                      setConditions((prev) =>
                        prev.map((x, i) =>
                          i === idx ? { ...x, windowDays: Number(e.target.value) || 1 } : x
                        )
                      )
                    }
                  />
                )}
                {c.metric === LoyaltyConditionMetric.CafeteriaQuantity && (
                  <div>
                    <label className="mb-1 block text-xs text-muted">{t('loyalty.cafeteriaItem')}</label>
                    <select
                      className="w-full rounded-lg border border-border bg-surface-elevated px-2 py-1.5 text-sm"
                      value={c.cafeteriaItemId ?? ''}
                      onChange={(e) =>
                        setConditions((prev) =>
                          prev.map((x, i) =>
                            i === idx
                              ? { ...x, cafeteriaItemId: e.target.value || null, variantId: null }
                              : x
                          )
                        )
                      }
                    >
                      <option value="">—</option>
                      {sellItems.map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.name}
                        </option>
                      ))}
                    </select>
                  </div>
                )}
                <div className="flex items-end">
                  <Button
                    size="sm"
                    variant="danger"
                    disabled={conditions.length <= 1}
                    onClick={() => setConditions((prev) => prev.filter((_, i) => i !== idx))}
                  >
                    {t('loyalty.remove')}
                  </Button>
                </div>
              </div>
            ))}
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <p className="font-medium">{t('loyalty.rewards')}</p>
              <Button
                size="sm"
                variant="secondary"
                onClick={() => setRewards((prev) => [...prev, emptyReward()])}
              >
                {t('loyalty.addReward')}
              </Button>
            </div>
            {rewards.map((r, idx) => (
              <div key={idx} className="grid gap-2 rounded-lg border border-border p-3 sm:grid-cols-4">
                <div>
                  <label className="mb-1 block text-xs text-muted">{t('loyalty.metric')}</label>
                  <select
                    className="w-full rounded-lg border border-border bg-surface-elevated px-2 py-1.5 text-sm"
                    value={r.metric}
                    onChange={(e) => {
                      const metric = Number(e.target.value);
                      setRewards((prev) =>
                        prev.map((x, i) => (i === idx ? { ...x, metric, cafeteriaItemId: null } : x))
                      );
                    }}
                  >
                    <option value={LoyaltyRewardMetric.FreeHours}>{t('loyalty.rewardFreeHours')}</option>
                    <option value={LoyaltyRewardMetric.FreeMatches}>{t('loyalty.rewardFreeMatches')}</option>
                    <option value={LoyaltyRewardMetric.CafeteriaItem}>{t('loyalty.rewardCafeteria')}</option>
                  </select>
                </div>
                <Input
                  label={t('loyalty.rewardQty')}
                  type="number"
                  min={0.1}
                  step={0.5}
                  value={r.quantity}
                  onChange={(e) =>
                    setRewards((prev) =>
                      prev.map((x, i) =>
                        i === idx ? { ...x, quantity: Number(e.target.value) || 0 } : x
                      )
                    )
                  }
                />
                {r.metric === LoyaltyRewardMetric.CafeteriaItem && (
                  <div>
                    <label className="mb-1 block text-xs text-muted">{t('loyalty.cafeteriaItem')}</label>
                    <select
                      className="w-full rounded-lg border border-border bg-surface-elevated px-2 py-1.5 text-sm"
                      value={r.cafeteriaItemId ?? ''}
                      onChange={(e) =>
                        setRewards((prev) =>
                          prev.map((x, i) =>
                            i === idx
                              ? { ...x, cafeteriaItemId: e.target.value || null, variantId: null }
                              : x
                          )
                        )
                      }
                    >
                      <option value="">—</option>
                      {sellItems.map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.name}
                        </option>
                      ))}
                    </select>
                  </div>
                )}
                <div className="flex items-end">
                  <Button
                    size="sm"
                    variant="danger"
                    disabled={rewards.length <= 1}
                    onClick={() => setRewards((prev) => prev.filter((_, i) => i !== idx))}
                  >
                    {t('loyalty.remove')}
                  </Button>
                </div>
              </div>
            ))}
          </div>

          <div>
            <p className="mb-1 font-medium">{t('loyalty.devices')}</p>
            <p className="mb-2 text-xs text-muted">{t('loyalty.devicesHint')}</p>
            <div className="max-h-40 space-y-1 overflow-y-auto rounded-lg border border-border p-2">
              {devices.length === 0 ? (
                <p className="text-sm text-muted">{t('loyalty.allDevices')}</p>
              ) : (
                devices.map((d) => (
                  <label key={d.id} className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={deviceIds.includes(d.id)}
                      onChange={(e) => {
                        setDeviceIds((prev) =>
                          e.target.checked ? [...prev, d.id] : prev.filter((id) => id !== d.id)
                        );
                      }}
                    />
                    {d.name}
                  </label>
                ))
              )}
            </div>
          </div>

          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      </Modal>
    </div>
  );
}
