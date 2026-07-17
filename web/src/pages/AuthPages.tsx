import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, Link } from 'react-router-dom';
import { queryClient } from '@/App';
import { useAuthStore, useUiStore } from '@/store';
import { authApi } from '@/api/client';
import { Button } from '@/components/ui/Button';
import { Input, PasswordInput } from '@/components/ui/Input';

function AuthLayout({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation();
  const { language, setLanguage } = useUiStore();

  return (
    <div className="auth-stage relative">
      <div className="absolute end-4 top-4 z-10">
        <Button variant="ghost" size="sm" onClick={() => setLanguage(language === 'en' ? 'ar' : 'en')}>
          {language === 'en' ? 'عربي' : 'En'}
        </Button>
      </div>
      <div className="relative z-[1] w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="font-display bg-gradient-to-r from-primary to-accent bg-clip-text text-4xl font-bold uppercase tracking-wider text-transparent">
            {t('app.name')}
          </h1>
          <p className="mt-2 text-sm text-muted">{t('app.tagline')}</p>
        </div>
        <div className="auth-card rounded-2xl p-6">{children}</div>
      </div>
    </div>
  );
}

export function LoginPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      const res = await authApi.login(username, password);
      queryClient.clear();
      setAuth(res.accessToken, res.refreshToken, res.user, res.activeBranchId, res.accessTokenExpiresAt);
      navigate(res.activeBranchId || res.user.isMaster ? '/' : '/select-branch');
    } catch (err) {
      const msg = err instanceof Error ? err.message : t('common.error');
      setError(msg.includes('SUBSCRIPTION_EXPIRED') ? t('auth.subscriptionExpired') : msg);
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthLayout>
      <h2 className="mb-6 text-xl font-bold">{t('auth.login')}</h2>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input label={t('auth.username')} type="text" autoComplete="username" value={username} onChange={(e) => setUsername(e.target.value)} required />
        <PasswordInput label={t('auth.password')} autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        {error && <p className="text-sm text-danger">{error}</p>}
        <Button type="submit" loading={loading} className="w-full">{t('auth.login')}</Button>
      </form>
      <p className="mt-4 text-center text-sm text-muted">
        {t('auth.noAccount')}{' '}
        <Link to="/register" className="text-primary hover:underline">{t('auth.register')}</Link>
      </p>
    </AuthLayout>
  );
}

export function RegisterPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [form, setForm] = useState({
    tenantName: '', slug: '', email: '', password: '',
    firstName: '', lastName: '', branchName: '',
  });

  // `email` field is the login username (API still uses Email property name)

  function update(field: string, value: string) {
    setForm((f) => {
      const next = { ...f, [field]: value };
      if (field === 'tenantName' && !f.slug) {
        next.slug = value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
      }
      return next;
    });
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      const res = await authApi.register(form);
      queryClient.clear();
      setAuth(res.accessToken, res.refreshToken, res.user, res.activeBranchId, res.accessTokenExpiresAt);
      navigate(res.activeBranchId ? '/' : '/select-branch');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('common.error'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthLayout>
      <h2 className="mb-6 text-xl font-bold">{t('auth.register')}</h2>
      <form onSubmit={handleSubmit} className="space-y-3">
        <Input label={t('auth.tenantName')} value={form.tenantName} onChange={(e) => update('tenantName', e.target.value)} required />
        <Input label={t('auth.slug')} value={form.slug} onChange={(e) => update('slug', e.target.value)} required />
        <div className="grid grid-cols-2 gap-3">
          <Input label={t('auth.firstName')} value={form.firstName} onChange={(e) => update('firstName', e.target.value)} required />
          <Input label={t('auth.lastName')} value={form.lastName} onChange={(e) => update('lastName', e.target.value)} required />
        </div>
        <Input label={t('auth.username')} type="text" autoComplete="username" value={form.email} onChange={(e) => update('email', e.target.value)} required />
        <PasswordInput label={t('auth.password')} autoComplete="new-password" value={form.password} onChange={(e) => update('password', e.target.value)} required />
        <Input label={t('auth.branchName')} value={form.branchName} onChange={(e) => update('branchName', e.target.value)} required />
        {error && <p className="text-sm text-danger">{error}</p>}
        <Button type="submit" loading={loading} className="w-full">{t('auth.register')}</Button>
      </form>
      <p className="mt-4 text-center text-sm text-muted">
        {t('auth.hasAccount')}{' '}
        <Link to="/login" className="text-primary hover:underline">{t('auth.login')}</Link>
      </p>
    </AuthLayout>
  );
}
