import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { AppLayout } from '@/layouts/AppLayout';
import { LoginPage, RegisterPage } from '@/pages/AuthPages';
import { BranchSelectPage } from '@/pages/BranchSelectPage';
import { HomeDashboardPage } from '@/pages/HomeDashboardPage';
import { DashboardPage } from '@/pages/DashboardPage';
import { SessionHistoryPage } from '@/pages/SessionHistoryPage';
import { CafeteriaPage } from '@/pages/CafeteriaPage';
import { InventoryPage } from '@/pages/InventoryPage';
import { AccountingPage } from '@/pages/AccountingPage';
import { ReportsPage } from '@/pages/ReportsPage';
import { UsersPage } from '@/pages/UsersPage';
import { ActivityLogPage } from '@/pages/ActivityLogPage';
import { CustomersPage } from '@/pages/CustomersPage';
import { SettingsPage } from '@/pages/SettingsPage';
import { useAuthStore, useUiStore } from '@/store';
import { AuthSessionKeepAlive } from '@/components/AuthSessionKeepAlive';
import '@/i18n';

export const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, staleTime: 5000 } },
});

function UiDocumentSync() {
  const { i18n } = useTranslation();
  const { language, theme } = useUiStore();

  useEffect(() => {
    document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = language;
    document.documentElement.classList.remove('dark', 'light');
    document.documentElement.classList.add(theme);
    document.documentElement.style.colorScheme = theme;
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.setAttribute('content', theme === 'dark' ? '#07090f' : '#e8eef5');
    void i18n.changeLanguage(language);
  }, [language, theme, i18n]);

  return null;
}

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const accessToken = useAuthStore((s) => s.accessToken);
  if (!accessToken) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <UiDocumentSync />
        <AuthSessionKeepAlive />
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/select-branch" element={
            <ProtectedRoute><BranchSelectPage /></ProtectedRoute>
          } />
          <Route element={
            <ProtectedRoute><AppLayout /></ProtectedRoute>
          }>
            <Route index element={<HomeDashboardPage />} />
            <Route path="floor" element={<DashboardPage />} />
            <Route path="sessions" element={<SessionHistoryPage />} />
            <Route path="cafeteria" element={<CafeteriaPage />} />
            <Route path="inventory" element={<InventoryPage />} />
            <Route path="accounting" element={<AccountingPage />} />
            <Route path="reports" element={<ReportsPage />} />
            <Route path="users" element={<UsersPage />} />
            <Route path="activity" element={<ActivityLogPage />} />
            <Route path="customers" element={<CustomersPage />} />
            <Route path="settings" element={<SettingsPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
