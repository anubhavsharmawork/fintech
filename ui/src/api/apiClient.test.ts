import { ApiError, configureApiClient, apiRequest, apiRequestOrThrow, apiGet, apiPost, apiPut, apiGetWithCache, clearEtagCache, generateIdempotencyKey } from './apiClient';

const originalFetch = global.fetch;

beforeEach(() => {
  global.fetch = jest.fn();
  localStorage.clear();
});

afterEach(() => {
  global.fetch = originalFetch;
  jest.restoreAllMocks();
});

// ─── ApiError ────────────────────────────────────────────────────────────────

describe('ApiError', () => {
  it('has name ApiError', () => {
    const err = new ApiError(400, 'Bad Request');
    expect(err.name).toBe('ApiError');
  });

  it('stores status and message', () => {
    const err = new ApiError(404, 'Not Found');
    expect(err.status).toBe(404);
    expect(err.message).toBe('Not Found');
  });

  it('stores body when supplied', () => {
    const body = { detail: 'missing' };
    const err = new ApiError(422, 'Unprocessable', body);
    expect(err.body).toEqual(body);
  });

  it('defaults body to null', () => {
    const err = new ApiError(500, 'Internal');
    expect(err.body).toBeNull();
  });

  it('is instanceof Error', () => {
    expect(new ApiError(400, 'Bad')).toBeInstanceOf(Error);
  });
});

// ─── configureApiClient ───────────────────────────────────────────────────────

describe('configureApiClient', () => {
  it('injects custom getToken into requests', async () => {
    const customToken = 'custom-token-123';
    configureApiClient({
      getToken: async () => customToken,
      refreshToken: async () => null,
    });

    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(JSON.stringify({}), { status: 200 }),
    );

    await apiRequest('/test');

    const call = (global.fetch as jest.Mock).mock.calls[0];
    const headers: Headers = call[1].headers;
    expect(headers.get('Authorization')).toBe(`Bearer ${customToken}`);

    // Restore default
    configureApiClient({
      getToken: async () => localStorage.getItem('token'),
      refreshToken: async () => null,
    });
  });
});

// ─── apiRequest ───────────────────────────────────────────────────────────────

describe('apiRequest', () => {
  beforeEach(() => {
    configureApiClient({
      getToken: async () => localStorage.getItem('token'),
      refreshToken: async () => null,
    });
  });

  it('injects Bearer token from localStorage', async () => {
    // Use a valid JWT with a far-future expiry so the client won't attempt a refresh
    const validToken = 'header.eyJleHAiOjk5OTk5OTk5OTl9.sig';
    localStorage.setItem('token', validToken);
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('{}', { status: 200 }),
    );

    await apiRequest('/some-endpoint');

    const headers: Headers = (global.fetch as jest.Mock).mock.calls[0][1].headers;
    expect(headers.get('Authorization')).toBe(`Bearer ${validToken}`);
  });

  it('skips Authorization header when skipAuth is true', async () => {
    localStorage.setItem('token', 'should-not-be-sent');
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('{}', { status: 200 }),
    );

    await apiRequest('/login', { skipAuth: true });

    const headers: Headers = (global.fetch as jest.Mock).mock.calls[0][1].headers;
    expect(headers.get('Authorization')).toBeNull();
  });

  it('returns response without throwing on non-2xx status', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('Error', { status: 400 }),
    );

    const res = await apiRequest('/bad');
    expect(res.status).toBe(400);
  });

  it('retries once on 401 when refreshToken returns a new token', async () => {
    const refreshedToken = 'refreshed-token';
    configureApiClient({
      getToken: async () => 'original-token',
      refreshToken: async () => refreshedToken,
    });

    (global.fetch as jest.Mock)
      .mockResolvedValueOnce(new Response('Unauthorized', { status: 401 }))
      .mockResolvedValueOnce(new Response('{}', { status: 200 }));

    const res = await apiRequest('/protected');
    expect(res.status).toBe(200);
    expect(global.fetch).toHaveBeenCalledTimes(2);

    const retryHeaders: Headers = (global.fetch as jest.Mock).mock.calls[1][1].headers;
    expect(retryHeaders.get('Authorization')).toBe(`Bearer ${refreshedToken}`);
  });

  it('does not retry on 401 when skipAuth is true', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('Unauthorized', { status: 401 }),
    );

    const res = await apiRequest('/endpoint', { skipAuth: true });
    expect(res.status).toBe(401);
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });

  it('does not retry on 401 when refreshToken returns null', async () => {
    configureApiClient({
      getToken: async () => 'token',
      refreshToken: async () => null,
    });

    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('Unauthorized', { status: 401 }),
    );

    const res = await apiRequest('/endpoint');
    expect(res.status).toBe(401);
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });

  it('passes through custom headers', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('{}', { status: 200 }),
    );

    await apiRequest('/endpoint', {
      headers: { 'Content-Type': 'application/json' },
    });

    const headers: Headers = (global.fetch as jest.Mock).mock.calls[0][1].headers;
    expect(headers.get('Content-Type')).toBe('application/json');
  });
});

// ─── apiRequestOrThrow ────────────────────────────────────────────────────────

describe('apiRequestOrThrow', () => {
  beforeEach(() => {
    configureApiClient({
      getToken: async () => null,
      refreshToken: async () => null,
    });
  });

  it('returns response on 2xx', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('{}', { status: 200 }),
    );

    const res = await apiRequestOrThrow('/ok');
    expect(res.ok).toBe(true);
  });

  it('throws ApiError on 400', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(JSON.stringify({ message: 'Bad input' }), {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await expect(apiRequestOrThrow('/bad')).rejects.toBeInstanceOf(ApiError);
  });

  it('throws ApiError with correct status', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('Not Found', { status: 404 }),
    );

    await expect(apiRequestOrThrow('/missing')).rejects.toMatchObject({ status: 404 });
  });

  it('extracts JSON error message from response', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(JSON.stringify({ message: 'Resource not found' }), {
        status: 404,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await expect(apiRequestOrThrow('/missing')).rejects.toMatchObject({
      message: 'Resource not found',
    });
  });

  it('throws ApiError on 500', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('Internal Server Error', { status: 500 }),
    );

    await expect(apiRequestOrThrow('/server-err')).rejects.toBeInstanceOf(ApiError);
  });
});

// ─── apiGet ───────────────────────────────────────────────────────────────────

describe('apiGet', () => {
  beforeEach(() => {
    configureApiClient({
      getToken: async () => null,
      refreshToken: async () => null,
    });
  });

  it('returns parsed JSON on success', async () => {
    const payload = { id: 1, name: 'Test' };
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    const result = await apiGet<typeof payload>('/data');
    expect(result).toEqual(payload);
  });

  it('uses GET method', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('[]', { status: 200 }),
    );

    await apiGet('/list');

    expect((global.fetch as jest.Mock).mock.calls[0][1].method).toBe('GET');
  });

  it('throws ApiError on non-2xx', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('Forbidden', { status: 403 }),
    );

    await expect(apiGet('/forbidden')).rejects.toBeInstanceOf(ApiError);
  });
});

// ─── generateIdempotencyKey ───────────────────────────────────────────────────

describe('generateIdempotencyKey', () => {
  it('returns a UUID v4 string', () => {
    const key = generateIdempotencyKey();
    expect(key).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
    );
  });

  it('generates unique keys on each call', () => {
    const keys = new Set(Array.from({ length: 20 }, generateIdempotencyKey));
    expect(keys.size).toBe(20);
  });
});

// ─── apiPost ──────────────────────────────────────────────────────────────────

describe('apiPost', () => {
  beforeEach(() => {
    configureApiClient({
      getToken: async () => null,
      refreshToken: async () => null,
    });
  });

  it('sends POST request with JSON body', async () => {
    const payload = { name: 'Test', amount: 100 };
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(JSON.stringify({ id: '123', ...payload }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    const result = await apiPost('/items', payload);

    expect((global.fetch as jest.Mock).mock.calls[0][1].method).toBe('POST');
    expect(result).toEqual({ id: '123', ...payload });
  });

  it('sets Content-Type to application/json by default', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('{}', { status: 200 }),
    );

    await apiPost('/items', { test: true });

    const headers: Headers = (global.fetch as jest.Mock).mock.calls[0][1].headers;
    expect(headers.get('Content-Type')).toBe('application/json');
  });

  it('includes Idempotency-Key header when provided', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('{}', { status: 200 }),
    );

    await apiPost('/items', { test: true }, { idempotencyKey: 'my-key-123' });

    const headers: Headers = (global.fetch as jest.Mock).mock.calls[0][1].headers;
    expect(headers.get('Idempotency-Key')).toBe('my-key-123');
  });

  it('handles 204 No Content response', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(null, { status: 204 }),
    );

    const result = await apiPost('/items', { test: true });

    expect(result).toBeUndefined();
  });

  it('throws ApiError on non-2xx', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(JSON.stringify({ message: 'Invalid data' }), {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await expect(apiPost('/items', {})).rejects.toBeInstanceOf(ApiError);
  });
});

// ─── apiPut ───────────────────────────────────────────────────────────────────

describe('apiPut', () => {
  beforeEach(() => {
    configureApiClient({
      getToken: async () => null,
      refreshToken: async () => null,
    });
  });

  it('sends PUT request with JSON body', async () => {
    const payload = { name: 'Updated' };
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    const result = await apiPut('/items/123', payload);

    expect((global.fetch as jest.Mock).mock.calls[0][1].method).toBe('PUT');
    expect(result).toEqual(payload);
  });

  it('includes Idempotency-Key header when provided', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('{}', { status: 200 }),
    );

    await apiPut('/items/123', { test: true }, { idempotencyKey: 'put-key' });

    const headers: Headers = (global.fetch as jest.Mock).mock.calls[0][1].headers;
    expect(headers.get('Idempotency-Key')).toBe('put-key');
  });

  it('handles 204 No Content response', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(null, { status: 204 }),
    );

    const result = await apiPut('/items/123', {});

    expect(result).toBeUndefined();
  });
});

// ─── apiGetWithCache ──────────────────────────────────────────────────────────

describe('apiGetWithCache', () => {
  beforeEach(() => {
    configureApiClient({
      getToken: async () => null,
      refreshToken: async () => null,
    });
    clearEtagCache();
  });

  it('stores ETag and data in cache', async () => {
    const payload = { id: 1, data: 'test' };
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: {
          'Content-Type': 'application/json',
          'ETag': '"abc123"',
        },
      }),
    );

    const result = await apiGetWithCache<typeof payload>('/cached-resource');

    expect(result).toEqual(payload);
  });

  it('sends If-None-Match header on subsequent requests', async () => {
    const payload = { id: 1 };

    // First request - returns with ETag
    (global.fetch as jest.Mock).mockResolvedValueOnce(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: {
          'Content-Type': 'application/json',
          'ETag': '"etag-value"',
        },
      }),
    );

    await apiGetWithCache('/cached');

    // Second request - should include If-None-Match
    (global.fetch as jest.Mock).mockResolvedValueOnce(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await apiGetWithCache('/cached');

    const secondCallHeaders: Headers = (global.fetch as jest.Mock).mock.calls[1][1].headers;
    expect(secondCallHeaders.get('If-None-Match')).toBe('"etag-value"');
  });

  it('returns cached data on 304 Not Modified', async () => {
    const payload = { id: 1, cached: true };

    // First request
    (global.fetch as jest.Mock).mockResolvedValueOnce(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: {
          'Content-Type': 'application/json',
          'ETag': '"cached-etag"',
        },
      }),
    );

    await apiGetWithCache('/resource');

    // Second request returns 304
    (global.fetch as jest.Mock).mockResolvedValueOnce(
      new Response(null, { status: 304 }),
    );

    const result = await apiGetWithCache<typeof payload>('/resource');

    expect(result).toEqual(payload);
  });

  it('throws ApiError on non-2xx non-304 response', async () => {
    (global.fetch as jest.Mock).mockResolvedValue(
      new Response('Not Found', { status: 404 }),
    );

    await expect(apiGetWithCache('/missing')).rejects.toBeInstanceOf(ApiError);
  });
});

// ─── clearEtagCache ───────────────────────────────────────────────────────────

describe('clearEtagCache', () => {
  beforeEach(() => {
    configureApiClient({
      getToken: async () => null,
      refreshToken: async () => null,
    });
    clearEtagCache();
  });

  it('clears cache for specific path', async () => {
    const payload = { id: 1 };

    // Cache a response
    (global.fetch as jest.Mock).mockResolvedValueOnce(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: {
          'Content-Type': 'application/json',
          'ETag': '"etag1"',
        },
      }),
    );

    await apiGetWithCache('/path1');
    clearEtagCache('/path1');

    // Next request should not have If-None-Match
    (global.fetch as jest.Mock).mockResolvedValueOnce(
      new Response(JSON.stringify(payload), { status: 200 }),
    );

    await apiGetWithCache('/path1');

    const headers: Headers = (global.fetch as jest.Mock).mock.calls[1][1].headers;
    expect(headers.get('If-None-Match')).toBeNull();
  });

  it('clears entire cache when no path specified', async () => {
    const payload = { id: 1 };

    (global.fetch as jest.Mock)
      .mockResolvedValueOnce(
        new Response(JSON.stringify(payload), {
          status: 200,
          headers: { 'ETag': '"etag1"' },
        }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify(payload), {
          status: 200,
          headers: { 'ETag': '"etag2"' },
        }),
      );

    await apiGetWithCache('/path1');
    await apiGetWithCache('/path2');

    clearEtagCache();

    // Subsequent requests should not have If-None-Match
    (global.fetch as jest.Mock)
      .mockResolvedValueOnce(new Response(JSON.stringify(payload), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(payload), { status: 200 }));

    await apiGetWithCache('/path1');
    await apiGetWithCache('/path2');

    const headers1: Headers = (global.fetch as jest.Mock).mock.calls[2][1].headers;
    const headers2: Headers = (global.fetch as jest.Mock).mock.calls[3][1].headers;
    expect(headers1.get('If-None-Match')).toBeNull();
    expect(headers2.get('If-None-Match')).toBeNull();
  });
});
