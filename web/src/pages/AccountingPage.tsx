import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { accountingApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { startOfMonth, today, toIsoDate, toIsoDateEnd } from '@/lib/dates';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { DataTable, DateRangeBar, PageHeader, StatCard } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';
import { Pagination } from '@/components/ui/Pagination';

export function AccountingPage() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const queryClient = useQueryClient();
  const canViewReports = hasPermission(user, Permissions.ReportsView);
  const canAddExpense = hasPermission(user, Permissions.ExpensesAdd);
  const canManageCategories =
    hasPermission(user, Permissions.SettingsManage) || hasPermission(user, Permissions.ExpensesAdd);

  const [from, setFrom] = useState(startOfMonth());
  const [to, setTo] = useState(today());
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [addOpen, setAddOpen] = useState(false);
  const [categoryOpen, setCategoryOpen] = useState(false);
  const [categoryId, setCategoryId] = useState('');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [expenseDate, setExpenseDate] = useState(today());
  const [catName, setCatName] = useState('');
  const [catNameAr, setCatNameAr] = useState('');
  const [error, setError] = useState('');

  const { data: dashboard, isLoading: dashLoading } = useQuery({
    queryKey: ['accounting-dashboard', from, to],
    queryFn: () => accountingApi.getDashboard(toIsoDate(from), toIsoDateEnd(to)),
    enabled: canViewReports,
  });

  const { data: expensesPage, isLoading: expLoading } = useQuery({
    queryKey: ['accounting-expenses', from, to, page, pageSize],
    queryFn: () => accountingApi.getExpenses(toIsoDate(from), toIsoDateEnd(to), page, pageSize),
  });
  const expenses = expensesPage?.items ?? [];

  const { data: categories = [] } = useQuery({
    queryKey: ['expense-categories'],
    queryFn: accountingApi.getCategories,
  });

  const addMutation = useMutation({
    mutationFn: () =>
      accountingApi.createExpense({
        categoryId,
        amount: Number(amount),
        description,
        expenseDate,
      }),
    onSuccess: () => {
      setAddOpen(false);
      setAmount('');
      setDescription('');
      queryClient.invalidateQueries({ queryKey: ['accounting-expenses'] });
      queryClient.invalidateQueries({ queryKey: ['accounting-dashboard'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const categoryMutation = useMutation({
    mutationFn: () =>
      accountingApi.createCategory({
        name: catName.trim(),
        nameAr: catNameAr.trim() || undefined,
      }),
    onSuccess: (created) => {
      setCategoryOpen(false);
      setCatName('');
      setCatNameAr('');
      setCategoryId(created.id);
      queryClient.invalidateQueries({ queryKey: ['expense-categories'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  return (
    <div>
      <PageHeader title={t('accounting.title')}>
        <div className="flex flex-wrap items-end gap-3">
          <DateRangeBar
            from={from}
            to={to}
            onFromChange={(v) => { setFrom(v); setPage(1); }}
            onToChange={(v) => { setTo(v); setPage(1); }}
          />
          {canManageCategories && (
            <Button variant="secondary" onClick={() => { setError(''); setCategoryOpen(true); }}>
              <Icon name="plus" className="h-4 w-4" />
              {t('accounting.addCategory')}
            </Button>
          )}
          {canAddExpense && (
            <Button onClick={() => { setError(''); setAddOpen(true); }}>
              <Icon name="plus" className="h-4 w-4" />
              {t('accounting.addExpense')}
            </Button>
          )}
        </div>
      </PageHeader>

      {canViewReports && (
        dashLoading ? (
          <PageLoader />
        ) : dashboard ? (
          <div className="mb-8 grid gap-4 sm:grid-cols-3">
            <StatCard label={t('accounting.revenue')} value={formatCurrency(dashboard.totalRevenue)} accent="success" />
            <StatCard label={t('accounting.expenses')} value={formatCurrency(dashboard.totalExpenses)} accent="danger" />
            <StatCard
              label={t('accounting.netProfit')}
              value={formatCurrency(dashboard.netProfit)}
              accent={dashboard.netProfit >= 0 ? 'success' : 'danger'}
            />
          </div>
        ) : null
      )}

      <div className="mb-8">
        <h2 className="mb-3 text-lg font-semibold">{t('accounting.categories')}</h2>
        {categories.length === 0 ? (
          <p className="text-sm text-muted">{t('accounting.noCategories')}</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {categories.filter((c) => c.isActive).map((c) => (
              <span
                key={c.id}
                className="rounded-lg border border-border bg-surface-elevated px-3 py-1.5 text-sm"
              >
                {c.name}
                {c.nameAr ? <span className="ms-2 text-muted">· {c.nameAr}</span> : null}
              </span>
            ))}
          </div>
        )}
      </div>

      {canViewReports && dashboard && dashboard.expensesByCategory.length > 0 && (
        <div className="mb-8">
          <h2 className="mb-3 text-lg font-semibold">{t('accounting.byCategory')}</h2>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {dashboard.expensesByCategory.map((c) => (
              <div key={c.categoryId} className="rounded-lg border border-border bg-surface-elevated px-4 py-3">
                <p className="text-sm text-muted">{c.categoryName}</p>
                <p className="font-semibold text-danger">{formatCurrency(c.total)}</p>
              </div>
            ))}
          </div>
        </div>
      )}

      <h2 className="mb-3 text-lg font-semibold">{t('accounting.expenseList')}</h2>
      {expLoading ? (
        <PageLoader />
      ) : expenses.length === 0 ? (
        <p className="text-muted">{t('accounting.noExpenses')}</p>
      ) : (
        <>
          <DataTable headers={[t('accounting.date'), t('accounting.category'), t('accounting.description'), t('accounting.amount'), t('accounting.recordedBy')]}>
            {expenses.map((e) => (
              <tr key={e.id} className="hover:bg-surface-hover transition-colors">
                <td className="px-4 py-3">{e.expenseDate}</td>
                <td className="px-4 py-3">{e.categoryName}</td>
                <td className="px-4 py-3">{e.description}</td>
                <td className="px-4 py-3 font-medium text-danger">{formatCurrency(e.amount)}</td>
                <td className="px-4 py-3 text-muted">{e.recordedByName}</td>
              </tr>
            ))}
          </DataTable>
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={expensesPage?.totalCount ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
          />
        </>
      )}

      <Modal open={addOpen} onClose={() => setAddOpen(false)} title={t('accounting.addExpense')}>
        <div className="space-y-4">
          <div>
            <label className="mb-1 block text-sm text-muted">{t('accounting.category')}</label>
            <select
              value={categoryId}
              onChange={(e) => setCategoryId(e.target.value)}
              className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
            >
              <option value="">{t('accounting.selectCategory')}</option>
              {categories.filter((c) => c.isActive).map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
            {categories.length === 0 && (
              <p className="mt-2 text-xs text-warning">{t('accounting.createCategoryFirst')}</p>
            )}
          </div>
          <Input label={t('accounting.amount')} type="number" value={amount} onChange={(e) => setAmount(e.target.value)} />
          <Input label={t('accounting.description')} value={description} onChange={(e) => setDescription(e.target.value)} />
          <Input label={t('accounting.date')} type="date" value={expenseDate} onChange={(e) => setExpenseDate(e.target.value)} />
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={addMutation.isPending}
            disabled={!categoryId || !amount || !description.trim()}
            onClick={() => addMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <Modal open={categoryOpen} onClose={() => setCategoryOpen(false)} title={t('accounting.addCategory')}>
        <div className="space-y-3">
          <Input label={t('accounting.categoryNameAr')} value={catNameAr} onChange={(e) => setCatNameAr(e.target.value)} />
          <Input label={t('accounting.categoryName')} value={catName} onChange={(e) => setCatName(e.target.value)} />
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={categoryMutation.isPending}
            disabled={!catName.trim()}
            onClick={() => categoryMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
