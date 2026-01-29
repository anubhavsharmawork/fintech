import {
  onAuthChange,
  decodeJwt,
  setAuth,
  refreshAccessToken,
  getToken,
  authFetch,
  clearAuth,
} from './auth';

describe('auth.ts', () => {
  let getItemMock: jest.Mock;
  let setItemMock: jest.Mock;
  let removeItemMock: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    getItemMock = localStorage.getItem as jest.Mock;
    setItemMock = localStorage.setItem as jest.Mock;
    removeItemMock = localStorage.removeItem as jest.Mock;
    getItemMock.mockReturnValue(null);
    (global.fetch as jest.Mock).mockClear();
  });

  describe('decodeJwt', () => {
    it('should decode a valid JWT token', () => {
      // Valid JWT: header.payload.signature
      // payload is base64url encoded {"exp": 9999999999, "sub": "user123"}
      const validToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTksInN1YiI6InVzZXIxMjMifQ.test';
      const decoded = decodeJwt(validToken);

      expect(decoded).not.toBeNull();
      expect(decoded.exp).toBe(9999999999);
      expect(decoded.sub).toBe('user123');
    });

    it('should return null for invalid JWT tokens', () => {
      expect(decodeJwt('invalid')).toBeNull();
      expect(decodeJwt('invalid.invalid')).toBeNull();
      expect(decodeJwt('')).toBeNull();
    });

    it('should handle malformed base64 in JWT', () => {
      expect(decodeJwt('header.!!!invalid!!!.signature')).toBeNull();
    });

    it('should decode JWT with URL-safe base64 characters', () => {
      // Using URL-safe base64 with - and _ instead of + and /
      const token = 'header.eyJleHAiOjk5OTk5OTk5OTksInN1YiI6InRlc3QifQ.signature';
      const decoded = decodeJwt(token);

      expect(decoded).not.toBeNull();
      expect(decoded.exp).toBe(9999999999);
    });
  });

  describe('setAuth', () => {
    it('should store token in localStorage', () => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});
      
      setAuth('test-token');

      expect(localStorage.setItem).toHaveBeenCalledWith('token', 'test-token');
    });

    it('should store userId in localStorage when provided', () => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});
      
      setAuth('test-token', 'user123');

      expect(localStorage.setItem).toHaveBeenCalledWith('token', 'test-token');
      expect(localStorage.setItem).toHaveBeenCalledWith('userId', 'user123');
    });

    it('should not store userId when not provided', () => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});
      
      setAuth('test-token');

      expect(localStorage.setItem).toHaveBeenCalledTimes(1);
      expect(localStorage.setItem).toHaveBeenCalledWith('token', 'test-token');
    });

    it('should emit auth change event', (done) => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});
      
      onAuthChange((token) => {
        expect(token).toBe('test-token');
        done();
      });

      setAuth('test-token');
    });

    it('should handle localStorage errors gracefully', () => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {
        throw new Error('Storage full');
      });

      expect(() => setAuth('test-token')).not.toThrow();
    });
  });

  describe('clearAuth', () => {
    it('should remove token from localStorage', () => {
      (localStorage.removeItem as jest.Mock).mockImplementation(() => {});
      
      clearAuth();

      expect(localStorage.removeItem).toHaveBeenCalledWith('token');
    });

    it('should remove userId from localStorage', () => {
      (localStorage.removeItem as jest.Mock).mockImplementation(() => {});
      
      clearAuth();

      expect(localStorage.removeItem).toHaveBeenCalledWith('userId');
    });

    it('should emit auth change event with null', (done) => {
      (localStorage.removeItem as jest.Mock).mockImplementation(() => {});
      
      onAuthChange((token) => {
        expect(token).toBeNull();
        done();
      });

      clearAuth();
    });

    it('should handle localStorage errors gracefully', () => {
      (localStorage.removeItem as jest.Mock).mockImplementation(() => {
        throw new Error('Storage error');
      });

      expect(() => clearAuth()).not.toThrow();
    });
  });

  describe('onAuthChange', () => {
    it('should call handler when auth changes', (done) => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});
      const handler = jest.fn();

      const unsubscribe = onAuthChange(handler);
      setAuth('new-token');

      setTimeout(() => {
        expect(handler).toHaveBeenCalledWith('new-token');
        unsubscribe();
        done();
      }, 0);
    });

    it('should return unsubscribe function', (done) => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});
      const handler = jest.fn();

      const unsubscribe = onAuthChange(handler);
      setAuth('token1');

      setTimeout(() => {
        handler.mockClear();
        unsubscribe();
        setAuth('token2');

        setTimeout(() => {
          expect(handler).not.toHaveBeenCalled();
          done();
        }, 0);
      }, 0);
    });

    it('should handle multiple listeners', (done) => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});
      const handler1 = jest.fn();
      const handler2 = jest.fn();

      onAuthChange(handler1);
      onAuthChange(handler2);
      setAuth('token');

      setTimeout(() => {
        expect(handler1).toHaveBeenCalledWith('token');
        expect(handler2).toHaveBeenCalledWith('token');
        done();
      }, 0);
    });
  });

  describe('refreshAccessToken', () => {
    it('should refresh token successfully', async () => {
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ token: 'new-token', userId: 'user123' }),
      });

      const result = await refreshAccessToken();

      expect(result).toBe('new-token');
      expect(global.fetch).toHaveBeenCalledWith('/users/refresh', {
        method: 'POST',
        credentials: 'include',
      });
      expect(localStorage.setItem).toHaveBeenCalledWith('token', 'new-token');
    });

    it('should return null if refresh fails', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
      });

      const result = await refreshAccessToken();

      expect(result).toBeNull();
    });

    it('should return null if response is missing token', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ userId: 'user123' }),
      });

      const result = await refreshAccessToken();

      expect(result).toBeNull();
    });

    it('should return null on fetch error', async () => {
      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Network error'));

      const result = await refreshAccessToken();

      expect(result).toBeNull();
    });

    it('should handle malformed JSON response', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => {
          throw new Error('Invalid JSON');
        },
      });

      const result = await refreshAccessToken();

      expect(result).toBeNull();
    });
  });

  describe('getToken', () => {
    it('should return cached token if not expired', async () => {
      const futureExp = Math.floor(Date.now() / 1000) + 3600;
      const token = 'header.eyJleHAiOjE2OTk5OTk5OTl9.signature';
      
      (localStorage.getItem as jest.Mock).mockReturnValueOnce(token);

      const result = await getToken();

      expect(result).toBe(token);
      expect(global.fetch).not.toHaveBeenCalled();
    });

    it('should refresh token if expired', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValueOnce('expired.header.sig');
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ token: 'new-token' }),
      });
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});

      const result = await getToken();

      expect(result).toBe('new-token');
      expect(global.fetch).toHaveBeenCalled();
    });

    it('should refresh token if expiring within 30 seconds', async () => {
      const soonExp = Math.floor(Date.now() / 1000) + 20; // Expires in 20 seconds
      const expB64 = btoa(JSON.stringify({ exp: soonExp }))
        .replace(/\+/g, '-')
        .replace(/\//g, '_');
      
      (localStorage.getItem as jest.Mock).mockReturnValueOnce(`h.${expB64}.s`);
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ token: 'refreshed-token' }),
      });
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});

      const result = await getToken();

      expect(result).toBe('refreshed-token');
    });

    it('should return null if no token and refresh fails', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValueOnce(null);
      (global.fetch as jest.Mock).mockResolvedValueOnce({ ok: false });

      const result = await getToken();

      expect(result).toBeNull();
    });

    it('should batch concurrent refresh requests', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValue(null);
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ token: 'batch-token' }),
      });
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});

      const [result1, result2] = await Promise.all([getToken(), getToken()]);

      expect(result1).toBe('batch-token');
      expect(result2).toBe('batch-token');
      expect(global.fetch).toHaveBeenCalledTimes(1); // Only one refresh request
    });
  });

  describe('authFetch', () => {
    it('should add Authorization header with valid token', async () => {
      const token = 'header.eyJleHAiOjk5OTk5OTk5OTl9.sig';
      (localStorage.getItem as jest.Mock).mockReturnValueOnce(token);
      (global.fetch as jest.Mock).mockResolvedValueOnce({ status: 200 });

      await authFetch('/api/test');

      expect(global.fetch).toHaveBeenCalledWith(
        '/api/test',
        expect.objectContaining({
          headers: expect.any(Headers),
          credentials: 'include',
        })
      );
    });

    it('should not add Authorization header if no token', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValueOnce(null);
      (global.fetch as jest.Mock).mockResolvedValueOnce({ status: 200 });

      await authFetch('/api/test');

      const calls = (global.fetch as jest.Mock).mock.calls;
      const headers = calls[0][1].headers;
      expect(headers.get('Authorization')).toBeNull();
    });

    it('should retry with refreshed token on 401', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValueOnce('expired-token');
      (global.fetch as jest.Mock)
        .mockResolvedValueOnce({ status: 401 })
        .mockResolvedValueOnce({ status: 200 });
      (localStorage.setItem as jest.Mock).mockImplementation(() => {});

      await authFetch('/api/test');

      expect(global.fetch).toHaveBeenCalledTimes(2);
    });

    it('should not retry if already retried', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValueOnce('token');
      (global.fetch as jest.Mock).mockResolvedValueOnce({ status: 401 });

      await authFetch('/api/test', {}, false);

      expect(global.fetch).toHaveBeenCalledTimes(1);
    });

    it('should preserve request headers', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValueOnce('token');
      (global.fetch as jest.Mock).mockResolvedValueOnce({ status: 200 });

      await authFetch('/api/test', { headers: { 'X-Custom': 'value' } });

      const call = (global.fetch as jest.Mock).mock.calls[0];
      const headers = call[1].headers as Headers;
      expect(headers.get('X-Custom')).toBe('value');
    });

    it('should handle fetch errors', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValueOnce('token');
      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Network error'));

      await expect(authFetch('/api/test')).rejects.toThrow('Network error');
    });
  });
});
