// Lightweight auth utilities for the SPA
// - getToken: returns valid access token, auto-refreshing via /users/refresh if expired
// - authFetch: wrapper around fetch that injects Authorization and retries once on 401

import { configureApiClient, apiRequest } from './api/apiClient';
import { API } from './config/constants';

// Simple auth event bus so UI can react to login/logout/refresh changes in same tab
const authEvents = new EventTarget();
function emitAuth(token: string | null) {
 try {
 authEvents.dispatchEvent(new CustomEvent<string | null>('auth', { detail: token }));
 } catch {}
}

export function onAuthChange(handler: (token: string | null) => void): () => void {
 const listener = (e: Event) => handler((e as CustomEvent<string | null>)?.detail ?? null);
 authEvents.addEventListener('auth', listener as EventListener);
 return () => authEvents.removeEventListener('auth', listener as EventListener);
 }

export function decodeJwt(token: string): any | null {
 try {
 const [, payload] = token.split('.');
 const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
 return JSON.parse(decodeURIComponent(escape(json)));
 } catch {
 return null;
 }
}

function isExpired(token: string): boolean {
 const p = decodeJwt(token);
 if (!p || !p.exp) return true;
 const nowSec = Math.floor(Date.now() /1000);
 // consider token expiring within next30s as expired to avoid race
 return p.exp <= nowSec +30;
}

let refreshing: Promise<string | null> | null = null;

export function setAuth(token: string, userId?: string) {
 try { localStorage.setItem('token', token); } catch {}
 if (userId) { try { localStorage.setItem('userId', userId); } catch {} }
 emitAuth(token);
}

export async function refreshAccessToken(): Promise<string | null> {
  try {
    const res = await fetch(API.REFRESH, { method: 'POST', credentials: 'include' });
    if (!res.ok) return null;
    const data = await res.json();
    const t = (data as any)?.token as string | undefined;
    const uid = (data as any)?.userId as string | undefined;
    if (t) {
      setAuth(t, uid);
      return t;
    }
  } catch {
    // ignore
  }
  return null;
}

export async function getToken(): Promise<string | null> {
  let token = (typeof window !== 'undefined') ? localStorage.getItem('token') : null;
  if (token && !isExpired(token)) return token;
  if (!refreshing) refreshing = refreshAccessToken().finally(() => { refreshing = null; });
  token = await refreshing;
  return token;
}

// Wire apiClient with token helpers (must run after getToken / refreshAccessToken are defined)
configureApiClient({ getToken, refreshToken: refreshAccessToken });

/**
 * Authenticated fetch — delegates to the central apiClient so that all
 * requests share base-URL resolution, timeout, and 401-retry logic.
 * Kept for backward compatibility; prefer importing apiRequest directly in new code.
 */
export async function authFetch(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
  return apiRequest(String(input), init);
}

export async function clearAuth() {
  try { await fetch(API.LOGOUT, { method: 'POST', credentials: 'include' }); } catch {}
  try { localStorage.removeItem('token'); } catch {}
  try { localStorage.removeItem('userId'); } catch {}
  emitAuth(null);
}

export function getClientType(): string {
  const token = (typeof window !== 'undefined') ? localStorage.getItem('token') : null;
  if (!token) return 'Individual';
  const payload = decodeJwt(token);
  return payload?.client_type ?? 'Individual';
}

export function getOrganisationId(): string | null {
  const token = (typeof window !== 'undefined') ? localStorage.getItem('token') : null;
  if (!token) return null;
  const payload = decodeJwt(token);
  return payload?.organisation_id ?? null;
}

export function getOrganisationRole(): string | null {
  const token = (typeof window !== 'undefined') ? localStorage.getItem('token') : null;
  if (!token) return null;
  const payload = decodeJwt(token);
  return payload?.organisation_role ?? null;
}

export function isCorporateUser(): boolean {
  return getClientType() === 'Corporate';
}
