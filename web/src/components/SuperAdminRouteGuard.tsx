import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/store';
import { isSuperAdmin } from '@/lib/permissions';

/** Super Admin may only use dashboard, users, and settings. */
const SUPER_ADMIN_PATHS = new Set(['/', '/users', '/settings']);

export function SuperAdminRouteGuard({ children }: { children?: React.ReactNode }) {
  const user = useAuthStore((s) => s.user);
  const location = useLocation();

  if (user && isSuperAdmin(user)) {
    const path = location.pathname.replace(/\/+$/, '') || '/';
    if (!SUPER_ADMIN_PATHS.has(path)) {
      return <Navigate to="/" replace />;
    }
  }

  return children ? <>{children}</> : <Outlet />;
}
