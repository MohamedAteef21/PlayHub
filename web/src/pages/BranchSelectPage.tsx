import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/store';
import { authApi } from '@/api/client';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';

export function BranchSelectPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user, setAuth, activeBranchId } = useAuthStore();
  const [loading, setLoading] = useState<string | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!user) {
      navigate('/login');
      return;
    }
    if (activeBranchId) {
      navigate('/');
      return;
    }
    if (user.isMaster && user.branches.length === 0) {
      navigate('/settings');
      return;
    }
    if (user.isMaster && user.branches.length > 0) {
      void selectBranch(user.branches[0].id);
    }
  }, [user, activeBranchId, navigate]);

  if (!user) return null;

  async function selectBranch(branchId: string) {
    setLoading(branchId);
    setError('');
    try {
      const res = await authApi.selectBranch(branchId);
      setAuth(res.accessToken, res.refreshToken, res.user, res.activeBranchId, res.accessTokenExpiresAt);
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('common.error'));
    } finally {
      setLoading(null);
    }
  }

  if (user.isMaster && user.branches.length === 0) {
    return (
      <div className="auth-stage">
        <div className="relative z-[1] w-full max-w-lg text-center">
          <h1 className="font-display mb-2 text-3xl font-bold uppercase tracking-wide">
            {t('branch.noBranchesYetTitle')}
          </h1>
          <p className="mb-6 text-muted">{t('branch.noBranchesYetHint')}</p>
          <Link to="/settings">
            <Button>{t('branch.goCreateBranch')}</Button>
          </Link>
        </div>
      </div>
    );
  }

  if (user.isMaster) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-surface text-muted">
        {t('common.loading')}
      </div>
    );
  }

  return (
    <div className="auth-stage">
      <div className="relative z-[1] w-full max-w-lg">
        <h1 className="font-display mb-2 text-3xl font-bold uppercase tracking-wide">{t('branch.select')}</h1>
        <p className="mb-6 text-muted">{t('branch.selectPrompt')}</p>
        {error && <p className="mb-4 text-sm text-danger">{error}</p>}
        <div className="space-y-3">
          {user.branches.map((branch) => (
            <Card
              key={branch.id}
              hover
              className="cursor-pointer"
              onClick={() => selectBranch(branch.id)}
            >
              <div className="flex items-center justify-between">
                <span className="font-medium">{branch.name}</span>
                <Button
                  size="sm"
                  loading={loading === branch.id}
                  onClick={(e) => {
                    e.stopPropagation();
                    selectBranch(branch.id);
                  }}
                >
                  {t('branch.continue')}
                </Button>
              </div>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}
