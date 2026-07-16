import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthUser } from '@/types';

interface UiState {
  language: 'en' | 'ar';
  theme: 'dark' | 'light';
  sidebarOpen: boolean;
  sidebarCollapsed: boolean;
  setLanguage: (lang: 'en' | 'ar') => void;
  setTheme: (theme: 'dark' | 'light') => void;
  toggleTheme: () => void;
  toggleSidebar: () => void;
  toggleSidebarCollapsed: () => void;
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      language: 'en',
      theme: 'dark',
      sidebarOpen: true,
      sidebarCollapsed: false,
      setLanguage: (language) => set({ language }),
      setTheme: (theme) => set({ theme }),
      toggleTheme: () => set((s) => ({ theme: s.theme === 'dark' ? 'light' : 'dark' })),
      toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),
      toggleSidebarCollapsed: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
    }),
    { name: 'playhub-ui' }
  )
);

export function applyUserUiPreferences(user: AuthUser) {
  const patch: Partial<{ language: 'en' | 'ar'; theme: 'dark' | 'light' }> = {};
  if (user.preferredLanguage === 'ar' || user.preferredLanguage === 'en') {
    patch.language = user.preferredLanguage;
  }
  if (user.preferredTheme === 'dark' || user.preferredTheme === 'light') {
    patch.theme = user.preferredTheme;
  }
  if (Object.keys(patch).length > 0) useUiStore.setState(patch);
}

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresAt: string | null;
  user: AuthUser | null;
  activeBranchId: string | null;
  setAuth: (
    accessToken: string,
    refreshToken: string,
    user: AuthUser,
    activeBranchId: string | null,
    accessTokenExpiresAt?: string | null
  ) => void;
  patchUser: (partial: Partial<AuthUser>) => void;
  setActiveBranch: (branchId: string) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      refreshToken: null,
      accessTokenExpiresAt: null,
      user: null,
      activeBranchId: null,
      setAuth: (accessToken, refreshToken, user, activeBranchId, accessTokenExpiresAt = null) => {
        set({ accessToken, refreshToken, user, activeBranchId, accessTokenExpiresAt });
        applyUserUiPreferences(user);
      },
      patchUser: (partial) =>
        set((s) => ({ user: s.user ? { ...s.user, ...partial } : null })),
      setActiveBranch: (branchId) => set({ activeBranchId: branchId }),
      logout: () =>
        set({
          accessToken: null,
          refreshToken: null,
          accessTokenExpiresAt: null,
          user: null,
          activeBranchId: null,
        }),
    }),
    {
      name: 'playhub-auth',
      onRehydrateStorage: () => (state) => {
        if (state?.user) applyUserUiPreferences(state.user);
      },
    }
  )
);
