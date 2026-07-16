import { useEffect } from 'react';
import { refreshAccessToken } from '@/api/client';
import { useAuthStore } from '@/store';

/**
 * Keeps the session alive while the app is open:
 * - refreshes ~30 min before access-token expiry
 * - refreshes when the tab becomes visible again after offline
 * Never logs the user out on network failures.
 */
export function AuthSessionKeepAlive() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const accessTokenExpiresAt = useAuthStore((s) => s.accessTokenExpiresAt);

  // Refresh once on app load so role/permission changes made on the server
  // are picked up immediately instead of waiting for token expiry.
  useEffect(() => {
    if (useAuthStore.getState().refreshToken) void refreshAccessToken();
  }, []);

  useEffect(() => {
    if (!accessToken || !refreshToken) return;

    const refreshIfNeeded = () => {
      const expiresAt = accessTokenExpiresAt ? Date.parse(accessTokenExpiresAt) : NaN;
      if (!Number.isFinite(expiresAt)) {
        void refreshAccessToken();
        return;
      }
      const msLeft = expiresAt - Date.now();
      // Refresh when less than 30 minutes remain (access token is 4h)
      if (msLeft < 30 * 60 * 1000) {
        void refreshAccessToken();
      }
    };

    refreshIfNeeded();
    const interval = window.setInterval(refreshIfNeeded, 5 * 60 * 1000);

    const onVisible = () => {
      if (document.visibilityState === 'visible') refreshIfNeeded();
    };
    const onOnline = () => refreshIfNeeded();

    document.addEventListener('visibilitychange', onVisible);
    window.addEventListener('online', onOnline);

    return () => {
      window.clearInterval(interval);
      document.removeEventListener('visibilitychange', onVisible);
      window.removeEventListener('online', onOnline);
    };
  }, [accessToken, refreshToken, accessTokenExpiresAt]);

  return null;
}
