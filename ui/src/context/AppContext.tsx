import React, { createContext, useContext, useState, useCallback, useEffect, useMemo, type ReactNode } from 'react';

interface User {
  id: string;
  email: string;
  role: string;
  name: string;
}

interface AppContextValue {
  user: User | null;
  unreadNotificationCount: number;
  setUnreadNotificationCount: (n: number) => void;
  refreshUser: () => void;
}

const noop = () => {};

const AppContext = createContext<AppContextValue>({
  user: null,
  unreadNotificationCount: 0,
  setUnreadNotificationCount: noop,
  refreshUser: noop,
});

/**
 * Decodes a JWT token payload using plain base64 decoding (no external library).
 * Returns null if decoding fails.
 */
function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;

    const payloadBase64 = parts[1];
    // Handle URL-safe base64 by replacing characters
    const base64 = payloadBase64.replace(/-/g, '+').replace(/_/g, '/');
    // Pad if necessary
    const padded = base64.padEnd(base64.length + (4 - (base64.length % 4)) % 4, '=');

    const decoded = atob(padded);
    return JSON.parse(decoded);
  } catch {
    return null;
  }
}

/**
 * Derives user info from a JWT token stored in localStorage.
 */
function getUserFromToken(): User | null {
  const token = localStorage.getItem('token');
  if (!token) return null;

  const payload = decodeJwtPayload(token);
  if (!payload) return null;

  // Extract standard JWT claims
  const id = (payload.sub ?? payload.id ?? payload.userId ?? '') as string;
  const email = (payload.email ?? payload.unique_name ?? '') as string;
  const role = (payload.role ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? 'user') as string;
  const firstName = (payload.given_name ?? '') as string;
  const lastName = (payload.family_name ?? '') as string;
  const name = [firstName, lastName].filter(Boolean).join(' ') || email;

  if (!id) return null;

  return { id, email, role, name };
}

interface AppProviderProps {
  children: ReactNode;
}

export function AppProvider({ children }: AppProviderProps) {
  const [user, setUser] = useState<User | null>(() => getUserFromToken());
  const [unreadNotificationCount, setUnreadNotificationCount] = useState(0);

  const refreshUser = useCallback(() => {
    setUser(getUserFromToken());
  }, []);

  // Listen for storage events to sync user state across tabs
  useEffect(() => {
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === 'token') {
        refreshUser();
      }
    };

    window.addEventListener('storage', handleStorageChange);
    return () => window.removeEventListener('storage', handleStorageChange);
  }, [refreshUser]);

  const value = useMemo<AppContextValue>(() => ({
    user,
    unreadNotificationCount,
    setUnreadNotificationCount,
    refreshUser,
  }), [user, unreadNotificationCount, refreshUser]);

  return (
    <AppContext.Provider value={value}>
      {children}
    </AppContext.Provider>
  );
}

export function useAppContext(): AppContextValue {
  const context = useContext(AppContext);
  return context;
}

export default AppContext;
