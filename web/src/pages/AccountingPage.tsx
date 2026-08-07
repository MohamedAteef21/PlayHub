import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { accountingApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { startOfMonth, today, toIsoDate, toIsoDateEnd } from '@/lib/dates';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import type { Expense, ExpenseCategory } from '@/types';
import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { DataTable, DateRangeBar, PageHeader, StatCard } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';
import { Pagination } from '@/components/ui/Pagination';

/** Matches backend ExpenseCategoryKind */
const CategoryKind = { Expense: 0, Revenue: 1 } as const;

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
  const [editingExpense, setEditingExpense] = useState<Expense | null>(null);
  const [categoryOpen, setCategoryOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<ExpenseCategory | null>(null);
  const [entryKind, setEntryKind] = useState<number>(CategoryKind.Expense);
  const [categoryId, setCategoryId] = useState('');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [expenseDate, setExpenseDate] = useState(today());
  const [catName, setCatName] = useState('');
  const [catNameAr, setCatNameAr] = useState('');
  const [catKind, setCatKind] = useState<number>(CategoryKind.Expense);
  const [catIsActive, setCatIsActive] = useState(true);
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

  const filteredCategories = useMemo(
    () => categories.filter((c) => c.kind === entryKind && (c.isActive || c.id === categoryId)),
    [categories, entryKind, categoryId]
  );

  const addMutation = useMutation({
    mutationFn: () => {
      const data = {
        categoryId,
        amount: Number(amount),
        description,
        expenseDate,
      };
      return editingExpense
        ? accountingApi.updateExpense(editingExpense.id, data)
        : accountingApi.createExpense(data);
    },
    onSuccess: () => {
      setAddOpen(false);
      setEditingExpense(null);
      setAmount('');
      setDescription('');
      setCategoryId('');
      setExpenseDate(today());
      setEntryKind(CategoryKind.Expense);
      queryClient.invalidateQueries({ queryKey: ['accounting-expenses'] });
      queryClient.invalidateQueries({ queryKey: ['accounting-dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['reports-cash-drawer'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteExpenseMutation = useMutation({
    mutationFn: (id: string) => accountingApi.deleteExpense(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting-expenses'] });
      queryClient.invalidateQueries({ queryKey: ['accounting-dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['reports-cash-drawer'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const categoryMutation = useMutation({
    mutationFn: () =>
      editingCategory
        ? accountingApi.updateCategory(editingCategory.id, {
            name: catName.trim(),
            nameAr: catNameAr.trim() || undefined,
            kind: catKind,
            isActive: catIsActive,
          })
        : accountingApi.createCategory({
            name: catName.trim(),
            nameAr: catNameAr.trim() || undefined,
            kind: catKind,
          }),
    onSuccess: (created) => {
      setCategoryOpen(false);
      setEditingCategory(null);
      setCatName('');
      setCatNameAr('');
      setCatKind(CategoryKind.Expense);
      setCatIsActive(true);
      if (!editingCategory) {
        setEntryKind(created.kind);
        setCategoryId(created.id);
      }
      queryClient.invalidateQueries({ queryKey: ['expense-categories'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteCategoryMutation = useMutation({
    mutationFn: (id: string) => accountingApi.deleteCategory(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['expense-categories'] }),
    onError: (e: Error) => setError(e.message),
  });

  function openCreateExpense() {
    setEditingExpense(null);
    setEntryKind(CategoryKind.Expense);
    setCategoryId('');
    setAmount('');
    setDescription('');
    setExpenseDate(today());
    setError('');
    setAddOpen(true);
  }

  function openEditExpense(e: Expense) {
    setEditingExpense(e);
    setEntryKind(e.categoryKind ?? CategoryKind.Expense);
    setCategoryId(e.categoryId);
    setAmount(String(e.amount));
    setDescription(e.description);
    setExpenseDate(e.expenseDate);
    setError('');
    setAddOpen(true);
  }

  function openCreateCategory() {
    setEditingCategory(null);
    setCatName('');
    setCatNameAr('');
    setCatKind(CategoryKind.Expense);
    setCatIsActive(true);
    setError('');
    setCategoryOpen(true);
  }

  function openEditCategory(c: ExpenseCategory) {
    setEditingCategory(c);
    setCatName(c.name);
    setCatNameAr(c.nameAr ?? '');
    setCatKind(c.kind ?? CategoryKind.Expense);
    setCatIsActive(c.isActive);
    setError('');
    setCategoryOpen(true);
  }

  function kindLabel(kind: number) {
    return kind === CategoryKind.Revenue ? t('accounting.kindRevenue') : t('accounting.kindExpense');
  }

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
            <Button variant="secondary" onClick={openCreateCategory}>
              <Icon name="plus" className="h-4 w-4" />
              {t('accounting.addCategory')}
            </Button>
          )}
          {canAddExpense && (
            <Button onClick={openCreateExpense}>
              <Icon name="plus" className="h-4 w-4" />
              {t('accounting.addCashboxEntry')}
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
            {categories.map((c) => (
              <span
                key={c.id}
                className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface-elevated px-3 py-1.5 text-sm"
              >
                <span
                  className={`rounded px-1.5 py-0.5 text-xs ${
                    c.kind === CategoryKind.Revenue
                      ? 'bg-success/15 text-success'
                      : 'bg-danger/15 text-danger'
                  }`}
                >
                  {kindLabel(c.kind)}
                </span>
                <span className={!c.isActive ? 'text-muted line-through' : undefined}>
                  {c.name}
                  {c.nameAr ? <span className="ms-2 text-muted">· {c.nameAr}</span> : null}
                </span>
                {canManageCategories && (
                  <>
                    <Button variant="ghost" size="sm" onClick={() => openEditCategory(c)}>
                      {t('users.edit')}
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      loading={deleteCategoryMutation.isPending}
                      onClick={() => {
                        if (window.confirm(t('common.confirmDelete'))) {
                          deleteCategoryMutation.mutate(c.id);
                        }
                      }}
                    >
                      {t('common.delete')}
                    </Button>
                  </>
                )}
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
      {error && !addOpen && !categoryOpen && <p className="mb-3 text-sm text-danger">{error}</p>}
      {expLoading ? (
        <PageLoader />
      ) : expenses.length === 0 ? (
        <p className="text-muted">{t('accounting.noExpenses')}</p>
      ) : (
        <>
          <DataTable
            headers={[
              t('accounting.date'),
              t('accounting.entryType'),
              t('accounting.category'),
              t('accounting.description'),
              t('accounting.amount'),
              t('accounting.recordedBy'),
              ...(canAddExpense ? [t('common.actions')] : []),
            ]}
          >
            {expenses.map((e) => {
              const isRevenue = e.categoryKind === CategoryKind.Revenue;
              return (
                <tr key={e.id} className="hover:bg-surface-hover transition-colors">
                  <td className="px-4 py-3">{e.expenseDate}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`rounded px-1.5 py-0.5 text-xs ${
                        isRevenue ? 'bg-success/15 text-success' : 'bg-danger/15 text-danger'
                      }`}
                    >
                      {kindLabel(e.categoryKind)}
                    </span>
                  </td>
                  <td className="px-4 py-3">{e.categoryName}</td>
                  <td className="px-4 py-3">{e.description}</td>
                  <td className={`px-4 py-3 font-medium ${isRevenue ? 'text-success' : 'text-danger'}`}>
                    {formatCurrency(e.amount)}
                  </td>
                  <td className="px-4 py-3 text-muted">{e.recordedByName}</td>
                  {canAddExpense && (
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-1">
                        <Button variant="ghost" size="sm" onClick={() => openEditExpense(e)}>
                          {t('users.edit')}
                        </Button>
                        <Button
                          variant="danger"
                          size="sm"
                          loading={deleteExpenseMutation.isPending}
                          onClick={() => {
                            if (window.confirm(t('common.confirmDelete'))) {
                              deleteExpenseMutation.mutate(e.id);
                            }
                          }}
                        >
                          {t('common.delete')}
                        </Button>
                      </div>
                    </td>
                  )}
                </tr>
              );
            })}
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

      <Modal
        open={addOpen}
        onClose={() => {
          setAddOpen(false);
          setEditingExpense(null);
        }}
        title={editingExpense ? t('users.edit') : t('accounting.addCashboxEntry')}
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1 block text-sm text-muted">{t('accounting.entryType')}</label>
            <select
              value={entryKind}
              onChange={(e) => {
                setEntryKind(Number(e.target.value));
                setCategoryId('');
              }}
              className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
            >
              <option value={CategoryKind.Expense}>{t('accounting.kindExpense')}</option>
              <option value={CategoryKind.Revenue}>{t('accounting.kindRevenue')}</option>
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm text-muted">{t('accounting.category')}</label>
            <select
              value={categoryId}
              onChange={(e) => setCategoryId(e.target.value)}
              className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
            >
              <option value="">{t('accounting.selectCategory')}</option>
              {filteredCategories.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
            {filteredCategories.length === 0 && (
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

      <Modal
        open={categoryOpen}
        onClose={() => {
          setCategoryOpen(false);
          setEditingCategory(null);
        }}
        title={editingCategory ? t('users.edit') : t('accounting.addCategory')}
      >
        <div className="space-y-3">
          <Input label={t('accounting.categoryNameAr')} value={catNameAr} onChange={(e) => setCatNameAr(e.target.value)} />
          <Input label={t('accounting.categoryName')} value={catName} onChange={(e) => setCatName(e.target.value)} />
          <div>
            <label className="mb-1 block text-sm text-muted">{t('accounting.categoryKind')}</label>
            <select
              value={catKind}
              onChange={(e) => setCatKind(Number(e.target.value))}
              className="w-full rounded-lg border border-border bg-surface-elevated px-3 py-2 text-sm"
            >
              <option value={CategoryKind.Expense}>{t('accounting.kindExpense')}</option>
              <option value={CategoryKind.Revenue}>{t('accounting.kindRevenue')}</option>
            </select>
          </div>
          {editingCategory && (
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={catIsActive} onChange={(e) => setCatIsActive(e.target.checked)} />
              {t('common.active')}
            </label>
          )}
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
