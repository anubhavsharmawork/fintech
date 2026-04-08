/**
 * Central HTTP client for all API calls.
 *
 * Features:
 *  - Base URL resolved from REACT_APP_API_BASE_URL env var (defaults to '' for same-origin proxy)
 *  - Automatic Authorization: Bearer <token> injection via getToken()
 *  - 401 → silent token refresh → single retry
 *  - Request timeout via AbortSignal.any / AbortController
 *  - Centralised ApiError with status code, message, and raw response body
 */

import { REQUEST_TIMEOUT_MS } from '../config/constants';

// ─── Error type ───────────────────────────────────────────────────────────────

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly body: unknown = null,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

/**
 * Base URL from env. Create React App exposes REACT_APP_* vars; Vite uses VITE_*.
 * Falls back to '' (same-origin) so the existing Webpack dev-proxy keeps working.
 */
const BASE_URL: string =
  (typeof process !== 'undefined' && (process.env as any).REACT_APP_API_BASE_URL) ||
  '';

function buildUrl(path: string): string {
  if (!BASE_URL) return path;
  return `${BASE_URL.replace(/\/$/, '')}/${path.replace(/^\//, '')}`;
}

async function parseErrorBody(res: Response): Promise<string> {
  try {
    const ct = res.headers.get('content-type') ?? '';
    if (ct.includes('application/json')) {
      const json = await res.json();
      return (json as any)?.message || (json as any)?.detail || JSON.stringify(json);
    }
    const text = await res.text();
    return text || res.statusText;
  } catch {
    return res.statusText;
  }
}

// ─── Token access (lazy-imported to avoid circular deps with auth.ts) ─────────

type GetTokenFn = () => Promise<string | null>;
type RefreshTokenFn = () => Promise<string | null>;

let _getToken: GetTokenFn = async () =>
  typeof window !== 'undefined' ? localStorage.getItem('token') : null;
let _refreshToken: RefreshTokenFn = async () => null;
let lastKnownToken: string | null = null;

function patchStorageTokenTracking(): void {
  try {
    const storage = globalThis.localStorage as Storage | undefined;
    if (!storage || (storage as any).__tokenTrackingPatched) return;

    const setItem = storage.setItem.bind(storage);
    const removeItem = storage.removeItem.bind(storage);
    const clear = storage.clear.bind(storage);

    storage.setItem = ((key: string, value: string) => {
      if (key === 'token') lastKnownToken = value;
      return setItem(key, value);
    }) as Storage['setItem'];
    storage.removeItem = ((key: string) => {
      if (key === 'token') lastKnownToken = null;
      return removeItem(key);
    }) as Storage['removeItem'];
    storage.clear = (() => {
      lastKnownToken = null;
      return clear();
    }) as Storage['clear'];

    (storage as any).__tokenTrackingPatched = true;
  } catch {
    // ignore storage patching failures in non-browser environments
  }
}

patchStorageTokenTracking();

function readStoredToken(): string | null {
  try {
    return lastKnownToken
      ?? globalThis.localStorage?.getItem('token')
      ?? (typeof localStorage !== 'undefined' ? localStorage.getItem('token') : null)
      ?? (typeof window !== 'undefined' ? window.localStorage.getItem('token') : null);
  } catch {
    return null;
  }
}

/**
 * Wire up token helpers. Called once from auth.ts on module load so that
 * apiClient has no hard import dependency on auth.ts (avoiding circular refs).
 */
export function configureApiClient(opts: {
  getToken: GetTokenFn;
  refreshToken: RefreshTokenFn;
}): void {
  _getToken = opts.getToken;
  _refreshToken = opts.refreshToken;
}

// ─── Core request function ────────────────────────────────────────────────────

async function executeRequest(
  path: string,
  init: RequestInit,
  callerSignal?: AbortSignal | null,
): Promise<Response> {
  const timeoutController = new AbortController();
  const timeoutId = setTimeout(
    () => timeoutController.abort(new DOMException('Request timed out', 'TimeoutError')),
    REQUEST_TIMEOUT_MS,
  );

  // Merge caller signal with our timeout signal when both exist
  let signal: AbortSignal;
  if (!callerSignal) {
    signal = timeoutController.signal;
  } else {
    const mergeController = new AbortController();
    const abort = (reason?: unknown) => mergeController.abort(reason);
    timeoutController.signal.addEventListener('abort', () => abort(timeoutController.signal.reason), { once: true });
    callerSignal.addEventListener('abort', () => abort(callerSignal!.reason), { once: true });
    signal = mergeController.signal;
  }

  try {
    return await fetch(buildUrl(path), { ...init, signal, credentials: 'include' });
  } finally {
    clearTimeout(timeoutId);
  }
}

// ─── Public API ───────────────────────────────────────────────────────────────

export interface RequestOptions extends Omit<RequestInit, 'signal'> {
  signal?: AbortSignal | null;
  /** Skip Authorization header (used for login / register) */
  skipAuth?: boolean;
}

/**
 * Makes an authenticated HTTP request.
 *
 * - Injects Bearer token automatically.
 * - On 401: refreshes token and retries once.
 * - On non-OK response (after retry): throws ApiError with normalised message.
 */
export async function apiRequest(
  path: string,
  options: RequestOptions = {},
): Promise<Response> {
  const { skipAuth = false, signal, ...init } = options;

  const buildHeaders = async (existingToken?: string): Promise<Headers> => {
    const headers = new Headers(init.headers || {});
    if (!skipAuth) {
      const token = existingToken ?? (await _getToken()) ?? readStoredToken();
      if (token) headers.set('Authorization', `Bearer ${token}`);
    }
    return headers;
  };

  const headers = await buildHeaders();
  let res = await executeRequest(path, { ...init, headers }, signal);

  // 401 → refresh → retry once
  if (res.status === 401 && !skipAuth) {
    const refreshed = await _refreshToken();
    if (refreshed) {
      const retryHeaders = await buildHeaders(refreshed);
      res = await executeRequest(path, { ...init, headers: retryHeaders }, signal);
    }
  }

  return res;
}

/**
 * Makes a request and throws ApiError on non-2xx responses.
 * Use when you want automatic error propagation.
 */
export async function apiRequestOrThrow(
  path: string,
  options: RequestOptions = {},
): Promise<Response> {
  const res = await apiRequest(path, options);
  if (!res.ok) {
    const message = await parseErrorBody(res);
    throw new ApiError(res.status, message || `Request failed (${res.status})`, null);
  }
  return res;
}

/**
 * Convenience: makes a request, throws on error, parses JSON, and returns typed result.
 */
export async function apiGet<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const res = await apiRequestOrThrow(path, { method: 'GET', ...options });
  return res.json() as Promise<T>;
}

// ─── ETag Cache ───────────────────────────────────────────────────────────────

interface CachedResponse<T> {
  etag: string;
  data: T;
}

const etagCache = new Map<string, CachedResponse<unknown>>();

/**
 * Generates a UUID v4 for use as an Idempotency-Key.
 * This should be called once per user-initiated action and reused on retries.
 */
export function generateIdempotencyKey(): string {
  return crypto.randomUUID();
}

/**
 * Makes a GET request with ETag caching support.
 * Stores ETags in memory and sends If-None-Match on subsequent calls.
 * Returns cached data on 304 Not Modified without re-rendering.
 */
export async function apiGetWithCache<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const headers = new Headers(options.headers || {});

  // Check for cached ETag
  const cached = etagCache.get(path) as CachedResponse<T> | undefined;
  if (cached) {
    headers.set('If-None-Match', cached.etag);
  }

  const res = await apiRequest(path, { method: 'GET', ...options, headers });

  // Handle 304 Not Modified - return cached data
  if (res.status === 304 && cached) {
    return cached.data;
  }

  if (!res.ok) {
    const message = await parseErrorBody(res);
    throw new ApiError(res.status, message || `Request failed (${res.status})`, null);
  }

  const data = await res.json() as T;

  // Store ETag from response
  const etag = res.headers.get('ETag');
  if (etag) {
    etagCache.set(path, { etag, data });
  }

  return data;
}

/**
 * Clears the ETag cache for a specific path or all paths.
 */
export function clearEtagCache(path?: string): void {
  if (path) {
    etagCache.delete(path);
  } else {
    etagCache.clear();
  }
}

export async function apiPost<T>(
  path: string,
  body: unknown,
  options: RequestOptions & { idempotencyKey?: string } = {},
): Promise<T> {
  const { idempotencyKey, ...restOptions } = options;
  const headers = new Headers(restOptions.headers || {});
  if (!headers.has('Content-Type')) headers.set('Content-Type', 'application/json');

  // Add Idempotency-Key header if provided
  if (idempotencyKey) {
    headers.set('Idempotency-Key', idempotencyKey);
  }

  const res = await apiRequestOrThrow(path, {
    method: 'POST',
    body: JSON.stringify(body),
    ...restOptions,
    headers,
  });
  // 204 No Content
  if (res.status === 204) return undefined as unknown as T;
  return res.json() as Promise<T>;
}

export async function apiPut<T>(
  path: string,
  body: unknown,
  options: RequestOptions & { idempotencyKey?: string } = {},
): Promise<T> {
  const { idempotencyKey, ...restOptions } = options;
  const headers = new Headers(restOptions.headers || {});
  if (!headers.has('Content-Type')) headers.set('Content-Type', 'application/json');

  // Add Idempotency-Key header if provided
  if (idempotencyKey) {
    headers.set('Idempotency-Key', idempotencyKey);
  }

  const res = await apiRequestOrThrow(path, {
    method: 'PUT',
    body: JSON.stringify(body),
    ...restOptions,
    headers,
  });
  if (res.status === 204) return undefined as unknown as T;
  return res.json() as Promise<T>;
}

export async function apiPatch<T>(
  path: string,
  body?: unknown,
  options: RequestOptions & { idempotencyKey?: string } = {},
): Promise<T> {
  const { idempotencyKey, ...restOptions } = options;
  const headers = new Headers(restOptions.headers || {});
  if (body !== undefined && !headers.has('Content-Type'))
    headers.set('Content-Type', 'application/json');

  // Add Idempotency-Key header if provided
  if (idempotencyKey) {
    headers.set('Idempotency-Key', idempotencyKey);
  }

  const res = await apiRequestOrThrow(path, {
    method: 'PATCH',
    body: body !== undefined ? JSON.stringify(body) : undefined,
    ...restOptions,
    headers,
  });
  if (res.status === 204) return undefined as unknown as T;
  return res.json() as Promise<T>;
}

export async function apiDelete(
  path: string,
  options: RequestOptions = {},
): Promise<void> {
  await apiRequestOrThrow(path, { method: 'DELETE', ...options });
}
