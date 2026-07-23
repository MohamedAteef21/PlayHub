import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { customersApi, offersApi, whatsappApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { formatDateTimeEgypt } from '@/lib/dates';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import { WalletTransactionType, SessionMode, SessionStatus, type Customer, type CustomerOffer } from '@/types';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { DataTable, PageHeader } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';
import { Pagination } from '@/components/ui/Pagination';

type Tab = 'customers' | 'offers';

export function CustomersPage() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const queryClient = useQueryClient();
  const canView =
    hasPermission(user, Permissions.CustomersView) ||
    hasPermission(user, Permissions.CustomersManage);
  const canManage = hasPermission(user, Permissions.CustomersManage);
  const canManageOffers = hasPermission(user, Permissions.OffersManage);

  const [tab, setTab] = useState<Tab>('customers');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [search, setSearch] = useState('');
  const [debouncedQ, setDebouncedQ] = useState('');
  const [error, setError] = useState('');
  const [msg, setMsg] = useState('');

  const [customerOpen, setCustomerOpen] = useState(false);
  const [editCustomer, setEditCustomer] = useState<Customer | null>(null);
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [notes, setNotes] = useState('');

  const [offerOpen, setOfferOpen] = useState(false);
  const [editOffer, setEditOffer] = useState<CustomerOffer | null>(null);
  const [offerTitle, setOfferTitle] = useState('');
  const [offerMessage, setOfferMessage] = useState('');
  const [offerActive, setOfferActive] = useState(true);

  const [sendOfferCustomer, setSendOfferCustomer] = useState<Customer | null>(null);
  const [selectedOfferId, setSelectedOfferId] = useState('');

  const [walletCustomer, setWalletCustomer] = useState<Customer | null>(null);
  const [walletAmount, setWalletAmount] = useState('');
  const [walletBonus, setWalletBonus] = useState('');
  const [walletNote, setWalletNote] = useState('');
  const [sessionsCustomer, setSessionsCustomer] = useState<Customer | null>(null);
  const [sessionsPage, setSessionsPage] = useState(1);
  const [sessionsPageSize, setSessionsPageSize] = useState(10);

  useEffect(() => {
    const id = window.setTimeout(() => {
      setDebouncedQ(search.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(id);
  }, [search]);

  const { data: customersPage, isLoading: customersLoading } = useQuery({
    queryKey: ['customers', debouncedQ, page, pageSize],
    queryFn: () => customersApi.getAll(debouncedQ || undefined, page, pageSize),
    enabled: canView && tab === 'customers',
  });

  const { data: offers = [], isLoading: offersLoading } = useQuery({
    queryKey: ['offers'],
    queryFn: () => offersApi.getAll(),
    enabled: canView && (tab === 'offers' || !!sendOfferCustomer),
  });

  const customers = customersPage?.items ?? [];

  function resetCustomerForm() {
    setEditCustomer(null);
    setName('');
    setPhone('');
    setNotes('');
    setError('');
  }

  function openCreateCustomer() {
    resetCustomerForm();
    setCustomerOpen(true);
  }

  function openEditCustomer(c: Customer) {
    setEditCustomer(c);
    setName(c.name);
    setPhone(c.phone);
    setNotes(c.notes ?? '');
    setError('');
    setCustomerOpen(true);
  }

  function resetOfferForm() {
    setEditOffer(null);
    setOfferTitle('');
    setOfferMessage('');
    setOfferActive(true);
    setError('');
  }

  function openCreateOffer() {
    resetOfferForm();
    setOfferOpen(true);
  }

  function openEditOffer(o: CustomerOffer) {
    setEditOffer(o);
    setOfferTitle(o.title);
    setOfferMessage(o.message);
    setOfferActive(o.isActive);
    setError('');
    setOfferOpen(true);
  }

  const saveCustomerMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        name: name.trim(),
        phone: phone.trim(),
        notes: notes.trim() || undefined,
      };
      if (editCustomer) {
        return customersApi.update(editCustomer.id, {
          ...payload,
          notes: notes.trim() || null,
          isActive: editCustomer.isActive,
        });
      }
      return customersApi.create(payload);
    },
    onSuccess: () => {
      setCustomerOpen(false);
      resetCustomerForm();
      queryClient.invalidateQueries({ queryKey: ['customers'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteCustomerMutation = useMutation({
    mutationFn: (id: string) => customersApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['customers'] }),
    onError: (e: Error) => setError(e.message),
  });

  const saveOfferMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        title: offerTitle.trim(),
        message: offerMessage.trim(),
        isActive: offerActive,
      };
      if (editOffer) {
        return offersApi.update(editOffer.id, payload);
      }
      return offersApi.create(payload);
    },
    onSuccess: () => {
      setOfferOpen(false);
      resetOfferForm();
      queryClient.invalidateQueries({ queryKey: ['offers'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteOfferMutation = useMutation({
    mutationFn: (id: string) => offersApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['offers'] }),
    onError: (e: Error) => setError(e.message),
  });

  const { data: walletHistory } = useQuery({
    queryKey: ['wallet-transactions', walletCustomer?.id],
    queryFn: () => customersApi.getWalletTransactions(walletCustomer!.id, 1, 10),
    enabled: !!walletCustomer,
  });

  const { data: customerSessions, isLoading: sessionsLoading } = useQuery({
    queryKey: ['customer-sessions', sessionsCustomer?.id, sessionsPage, sessionsPageSize],
    queryFn: () => customersApi.getSessions(sessionsCustomer!.id, sessionsPage, sessionsPageSize),
    enabled: !!sessionsCustomer,
  });

  const topUpMutation = useMutation({
    mutationFn: () =>
      customersApi.topUpWallet(walletCustomer!.id, {
        amount: Number(walletAmount),
        bonusAmount: Number(walletBonus) || 0,
        note: walletNote.trim() || undefined,
      }),
    onSuccess: (updated) => {
      setMsg(t('customers.walletTopUpDone'));
      setWalletCustomer(updated);
      setWalletAmount('');
      setWalletBonus('');
      setWalletNote('');
      setError('');
      queryClient.invalidateQueries({ queryKey: ['customers'] });
      queryClient.invalidateQueries({ queryKey: ['wallet-transactions'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  function walletTypeLabel(type: number) {
    if (type === WalletTransactionType.TopUp) return t('customers.walletTypeTopUp');
    if (type === WalletTransactionType.Bonus) return t('customers.walletTypeBonus');
    if (type === WalletTransactionType.Payment) return t('customers.walletTypePayment');
    return t('customers.walletTypeAdjustment');
  }

  const sendOfferMutation = useMutation({
    mutationFn: () =>
      whatsappApi.sendOffer({
        customerId: sendOfferCustomer!.id,
        offerId: selectedOfferId,
      }),
    onSuccess: (res) => {
      if (res.success) {
        setMsg(t('customers.offerSent'));
        setSendOfferCustomer(null);
        setSelectedOfferId('');
        setError('');
      } else {
        setError(res.error || t('common.error'));
      }
    },
    onError: (e: Error) => setError(e.message),
  });

  if (!canView) return <Navigate to="/" replace />;

  return (
    <div>
      <PageHeader title={t('customers.title')}>
        {tab === 'customers' && canManage && (
          <Button onClick={openCreateCustomer}>
            <Icon name="plus" className="h-4 w-4" />
            {t('customers.add')}
          </Button>
        )}
        {tab === 'offers' && canManageOffers && (
          <Button onClick={openCreateOffer}>
            <Icon name="plus" className="h-4 w-4" />
            {t('offers.add')}
          </Button>
        )}
      </PageHeader>

      <p className="mb-4 max-w-2xl text-sm text-muted">{t('customers.hint')}</p>

      <div className="mb-6 flex flex-wrap gap-2">
        <Button
          variant={tab === 'customers' ? 'primary' : 'secondary'}
          size="sm"
          onClick={() => setTab('customers')}
        >
          <Icon name="customers" className="h-4 w-4" />
          {t('customers.tabCustomers')}
        </Button>
        <Button
          variant={tab === 'offers' ? 'primary' : 'secondary'}
          size="sm"
          onClick={() => setTab('offers')}
        >
          {t('customers.tabOffers')}
        </Button>
      </div>

      {msg && (
        <p className="mb-4 rounded-lg bg-success/10 px-3 py-2 text-sm text-success">{msg}</p>
      )}
      {error && !customerOpen && !offerOpen && !sendOfferCustomer && !walletCustomer && (
        <p className="mb-4 text-sm text-danger">{error}</p>
      )}

      {tab === 'customers' && (
        <>
          <div className="mb-4 max-w-md">
            <Input
              label={t('customers.search')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('customers.searchPlaceholder')}
            />
          </div>

          {customersLoading ? (
            <PageLoader />
          ) : (
            <>
              <DataTable
                headers={[
                  t('customers.code'),
                  t('customers.name'),
                  t('customers.phone'),
                  t('customers.walletBalance'),
                  t('customers.notes'),
                  t('common.status'),
                  t('customers.actions'),
                ]}
              >
                {customers.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="px-4 py-8 text-center text-muted">
                      {t('customers.empty')}
                    </td>
                  </tr>
                ) : (
                  customers.map((c) => (
                    <tr key={c.id} className="hover:bg-surface-hover/50">
                      <td className="px-4 py-3 font-mono text-xs">{c.code}</td>
                      <td className="px-4 py-3 font-medium">{c.name}</td>
                      <td className="px-4 py-3" dir="ltr">
                        {c.phone}
                      </td>
                      <td className="px-4 py-3 font-medium text-accent">
                        {formatCurrency(c.walletBalance)}
                      </td>
                      <td className="max-w-[14rem] truncate px-4 py-3 text-muted">
                        {c.notes || '—'}
                      </td>
                      <td className="px-4 py-3">
                        <Badge status={c.isActive ? 'gaming' : 'idle'}>
                          {c.isActive ? t('common.active') : t('common.inactive')}
                        </Badge>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap gap-1">
                          {canManage && (
                            <Button size="sm" variant="secondary" onClick={() => openEditCustomer(c)}>
                              {t('customers.edit')}
                            </Button>
                          )}
                          {canView && (
                            <Button
                              size="sm"
                              variant="secondary"
                              onClick={() => {
                                setSessionsPage(1);
                                setSessionsCustomer(c);
                              }}
                            >
                              {t('customers.sessionHistory')}
                            </Button>
                          )}
                          {canManage && (
                            <Button
                              size="sm"
                              variant="secondary"
                              onClick={() => {
                                setMsg('');
                                setError('');
                                setWalletAmount('');
                                setWalletBonus('');
                                setWalletNote('');
                                setWalletCustomer(c);
                              }}
                            >
                              {t('customers.wallet')}
                            </Button>
                          )}
                          {canManage && (
                            <Button
                              size="sm"
                              variant="secondary"
                              onClick={() => {
                                setMsg('');
                                setError('');
                                setSelectedOfferId('');
                                setSendOfferCustomer(c);
                              }}
                            >
                              {t('customers.sendOffer')}
                            </Button>
                          )}
                          {canManage && (
                            <Button
                              size="sm"
                              variant="danger"
                              loading={deleteCustomerMutation.isPending}
                              onClick={() => {
                                if (window.confirm(t('common.confirmDelete'))) {
                                  deleteCustomerMutation.mutate(c.id);
                                }
                              }}
                            >
                              {t('common.delete')}
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </DataTable>
              <Pagination
                page={page}
                pageSize={pageSize}
                totalCount={customersPage?.totalCount ?? 0}
                onPageChange={setPage}
                onPageSizeChange={(size) => {
                  setPageSize(size);
                  setPage(1);
                }}
              />
            </>
          )}
        </>
      )}

      {tab === 'offers' && (
        <>
          {offersLoading ? (
            <PageLoader />
          ) : (
            <DataTable
              headers={[
                t('offers.title'),
                t('offers.message'),
                t('common.status'),
                t('customers.actions'),
              ]}
            >
              {offers.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-muted">
                    {t('offers.empty')}
                  </td>
                </tr>
              ) : (
                offers.map((o) => (
                  <tr key={o.id} className="hover:bg-surface-hover/50">
                    <td className="px-4 py-3 font-medium">{o.title}</td>
                    <td className="max-w-md truncate px-4 py-3 text-muted">{o.message}</td>
                    <td className="px-4 py-3">
                      <Badge status={o.isActive ? 'gaming' : 'idle'}>
                        {o.isActive ? t('common.active') : t('common.inactive')}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-1">
                        {canManageOffers && (
                          <Button size="sm" variant="secondary" onClick={() => openEditOffer(o)}>
                            {t('offers.edit')}
                          </Button>
                        )}
                        {canManageOffers && (
                          <Button
                            size="sm"
                            variant="danger"
                            loading={deleteOfferMutation.isPending}
                            onClick={() => {
                              if (window.confirm(t('common.confirmDelete'))) {
                                deleteOfferMutation.mutate(o.id);
                              }
                            }}
                          >
                            {t('common.delete')}
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </DataTable>
          )}
        </>
      )}

      <Modal
        open={customerOpen}
        onClose={() => {
          setCustomerOpen(false);
          resetCustomerForm();
        }}
        title={editCustomer ? t('customers.edit') : t('customers.add')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setCustomerOpen(false);
                resetCustomerForm();
              }}
            >
              {t('session.cancel')}
            </Button>
            <Button
              loading={saveCustomerMutation.isPending}
              onClick={() => saveCustomerMutation.mutate()}
              disabled={!name.trim() || !phone.trim()}
            >
              {t('common.save')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          {editCustomer && (
            <Input label={t('customers.code')} value={editCustomer.code} readOnly disabled />
          )}
          <Input
            label={t('customers.name')}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
          <Input
            label={t('customers.phone')}
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            dir="ltr"
            required
          />
          <div className="space-y-1.5">
            <label className="block text-sm font-medium text-muted">{t('customers.notes')}</label>
            <textarea
              className="w-full rounded-lg border border-border bg-surface px-3 py-2.5 text-text placeholder:text-muted/60 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
              rows={3}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
            />
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      </Modal>

      <Modal
        open={offerOpen}
        onClose={() => {
          setOfferOpen(false);
          resetOfferForm();
        }}
        title={editOffer ? t('offers.edit') : t('offers.add')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setOfferOpen(false);
                resetOfferForm();
              }}
            >
              {t('session.cancel')}
            </Button>
            <Button
              loading={saveOfferMutation.isPending}
              onClick={() => saveOfferMutation.mutate()}
              disabled={!offerTitle.trim() || !offerMessage.trim()}
            >
              {t('common.save')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <Input
            label={t('offers.title')}
            value={offerTitle}
            onChange={(e) => setOfferTitle(e.target.value)}
            required
          />
          <div className="space-y-1.5">
            <label className="block text-sm font-medium text-muted">{t('offers.message')}</label>
            <textarea
              className="w-full rounded-lg border border-border bg-surface px-3 py-2.5 text-text placeholder:text-muted/60 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
              rows={4}
              value={offerMessage}
              onChange={(e) => setOfferMessage(e.target.value)}
              required
            />
          </div>
          {editOffer && (
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={offerActive}
                onChange={(e) => setOfferActive(e.target.checked)}
              />
              {t('common.active')}
            </label>
          )}
          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      </Modal>

      <Modal
        open={!!sendOfferCustomer}
        onClose={() => {
          setSendOfferCustomer(null);
          setSelectedOfferId('');
          setError('');
        }}
        title={t('customers.sendOffer')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setSendOfferCustomer(null);
                setSelectedOfferId('');
                setError('');
              }}
            >
              {t('session.cancel')}
            </Button>
            <Button
              loading={sendOfferMutation.isPending}
              disabled={!selectedOfferId}
              onClick={() => sendOfferMutation.mutate()}
            >
              {t('customers.sendOfferConfirm')}
            </Button>
          </>
        }
      >
        {sendOfferCustomer && (
          <div className="space-y-4">
            <p className="text-sm text-muted">
              {t('customers.sendOfferTo', {
                name: sendOfferCustomer.name,
                phone: sendOfferCustomer.phone,
              })}
            </p>
            <div>
              <label className="mb-1 block text-sm text-muted">{t('customers.pickOffer')}</label>
              <select
                className="w-full rounded-lg border border-border bg-surface px-3 py-2"
                value={selectedOfferId}
                onChange={(e) => setSelectedOfferId(e.target.value)}
              >
                <option value="">—</option>
                {offers
                  .filter((o) => o.isActive)
                  .map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.title}
                    </option>
                  ))}
              </select>
            </div>
            {error && <p className="text-sm text-danger">{error}</p>}
          </div>
        )}
      </Modal>

      <Modal
        open={!!walletCustomer}
        onClose={() => {
          setWalletCustomer(null);
          setError('');
        }}
        title={walletCustomer ? t('customers.walletTopUpFor', { name: walletCustomer.name }) : t('customers.wallet')}
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setWalletCustomer(null);
                setError('');
              }}
            >
              {t('session.cancel')}
            </Button>
            <Button
              loading={topUpMutation.isPending}
              disabled={!(Number(walletAmount) > 0)}
              onClick={() => topUpMutation.mutate()}
            >
              {t('customers.walletTopUp')}
            </Button>
          </>
        }
      >
        {walletCustomer && (
          <div className="space-y-4">
            <p className="text-sm">
              {t('customers.walletBalance')}:{' '}
              <span className="font-semibold text-accent">
                {formatCurrency(walletCustomer.walletBalance)}
              </span>
            </p>
            <div className="grid gap-3 sm:grid-cols-2">
              <Input
                label={t('customers.walletAmount')}
                type="number"
                value={walletAmount}
                onChange={(e) => setWalletAmount(e.target.value)}
              />
              <Input
                label={t('customers.walletBonus')}
                type="number"
                value={walletBonus}
                onChange={(e) => setWalletBonus(e.target.value)}
              />
            </div>
            <p className="text-xs text-muted">{t('customers.walletBonusHint')}</p>
            <Input
              label={t('customers.walletNote')}
              value={walletNote}
              onChange={(e) => setWalletNote(e.target.value)}
            />
            <div>
              <p className="mb-2 text-sm font-medium">{t('customers.walletHistory')}</p>
              {!walletHistory || walletHistory.items.length === 0 ? (
                <p className="text-xs text-muted">{t('customers.walletNoTransactions')}</p>
              ) : (
                <div className="max-h-48 space-y-1 overflow-y-auto">
                  {walletHistory.items.map((tx) => (
                    <div
                      key={tx.id}
                      className="flex items-center justify-between rounded-lg border border-border px-3 py-2 text-xs"
                    >
                      <span>{walletTypeLabel(tx.type)}</span>
                      <span className={tx.amount < 0 ? 'text-danger' : 'text-success'}>
                        {tx.amount > 0 ? '+' : ''}
                        {formatCurrency(tx.amount)}
                      </span>
                      <span className="text-muted">
                        {t('customers.walletBalanceAfter')}: {formatCurrency(tx.balanceAfter)}
                      </span>
                      <span className="text-muted" dir="ltr">
                        {new Date(tx.createdAt).toLocaleDateString()}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
            {error && <p className="text-sm text-danger">{error}</p>}
          </div>
        )}
      </Modal>

      <Modal
        open={!!sessionsCustomer}
        onClose={() => setSessionsCustomer(null)}
        size="xl"
        title={
          sessionsCustomer
            ? t('customers.sessionHistoryFor', { name: sessionsCustomer.name })
            : t('customers.sessionHistory')
        }
        footer={
          <Button variant="secondary" onClick={() => setSessionsCustomer(null)}>
            {t('session.cancel')}
          </Button>
        }
      >
        {sessionsCustomer && (
          <div className="space-y-3">
            {sessionsLoading ? (
              <PageLoader />
            ) : !customerSessions || customerSessions.items.length === 0 ? (
              <p className="text-sm text-muted">{t('customers.sessionHistoryEmpty')}</p>
            ) : (
              <>
                <DataTable
                  headers={[
                    t('session.device'),
                    t('session.room'),
                    t('session.branch'),
                    t('session.mode'),
                    t('common.status'),
                    t('session.started'),
                    t('session.closedAt'),
                    t('session.timeCost'),
                    t('session.cafeteria'),
                    t('session.total'),
                  ]}
                >
                  {customerSessions.items.map((s) => (
                    <tr key={s.id} className="hover:bg-surface-hover/50">
                      <td className="px-3 py-2 font-medium">{s.deviceName}</td>
                      <td className="px-3 py-2">{s.roomName ?? '—'}</td>
                      <td className="px-3 py-2">{s.branchName ?? '—'}</td>
                      <td className="px-3 py-2">
                        {s.sessionMode === SessionMode.Gaming
                          ? t('session.gaming')
                          : t('session.watching')}
                      </td>
                      <td className="px-3 py-2">
                        <Badge
                          status={
                            s.status === SessionStatus.Open
                              ? 'gaming'
                              : s.status === SessionStatus.Paused
                                ? 'watching'
                                : 'idle'
                          }
                        >
                          {s.status === SessionStatus.Open
                            ? t('dashboard.gaming')
                            : s.status === SessionStatus.Paused
                              ? t('dashboard.paused')
                              : t('sessionHistory.closed')}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 text-xs whitespace-nowrap" dir="ltr">
                        {formatDateTimeEgypt(s.startedAt)}
                      </td>
                      <td className="px-3 py-2 text-xs whitespace-nowrap" dir="ltr">
                        {formatDateTimeEgypt(s.closedAt)}
                      </td>
                      <td className="px-3 py-2">
                        {s.status === SessionStatus.Closed ? formatCurrency(s.timeCost) : '—'}
                      </td>
                      <td className="px-3 py-2">{formatCurrency(s.cafeteriaCost)}</td>
                      <td className="px-3 py-2 font-medium">
                        {s.status === SessionStatus.Closed ? formatCurrency(s.totalCost) : '—'}
                      </td>
                    </tr>
                  ))}
                </DataTable>
                <Pagination
                  page={sessionsPage}
                  pageSize={sessionsPageSize}
                  totalCount={customerSessions.totalCount}
                  onPageChange={setSessionsPage}
                  onPageSizeChange={(size) => {
                    setSessionsPageSize(size);
                    setSessionsPage(1);
                  }}
                />
              </>
            )}
          </div>
        )}
      </Modal>
    </div>
  );
}
