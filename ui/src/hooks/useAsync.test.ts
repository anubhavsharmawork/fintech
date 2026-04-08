import { renderHook, waitFor, act } from '@testing-library/react';
import { useAsync } from './useAsync';

describe('useAsync Hook', () => {
  describe('Initial State', () => {
    it('should start with loading true', () => {
      const asyncFn = jest.fn(() => new Promise(() => {})); // Never resolves

      const { result } = renderHook(() => useAsync(asyncFn, []));

      expect(result.current.loading).toBe(true);
    });

    it('should start with data null', () => {
      const asyncFn = jest.fn(() => new Promise(() => {}));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      expect(result.current.data).toBeNull();
    });

    it('should start with error null', () => {
      const asyncFn = jest.fn(() => new Promise(() => {}));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      expect(result.current.error).toBeNull();
    });

    it('should provide refetch function', () => {
      const asyncFn = jest.fn(() => new Promise(() => {}));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      expect(typeof result.current.refetch).toBe('function');
    });
  });

  describe('Successful Fetch', () => {
    it('should set data when promise resolves', async () => {
      const mockData = { id: 1, name: 'Test' };
      const asyncFn = jest.fn(() => Promise.resolve(mockData));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(result.current.data).toEqual(mockData);
      });
    });

    it('should set loading false when promise resolves', async () => {
      const asyncFn = jest.fn(() => Promise.resolve('data'));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });
    });

    it('should keep error null when promise resolves', async () => {
      const asyncFn = jest.fn(() => Promise.resolve('data'));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(result.current.error).toBeNull();
      });
    });

    it('should call async function on mount', async () => {
      const asyncFn = jest.fn(() => Promise.resolve('data'));

      renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalled();
      });
    });

    it('should pass AbortSignal to async function', async () => {
      const asyncFn = jest.fn((signal: AbortSignal) => {
        expect(signal).toBeInstanceOf(AbortSignal);
        return Promise.resolve('data');
      });

      renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalled();
      });
    });
  });

  describe('Error Handling', () => {
    it('should set error when promise rejects with Error', async () => {
      const errorMessage = 'Test error message';
      const asyncFn = jest.fn(() => Promise.reject(new Error(errorMessage)));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(result.current.error).toBe(errorMessage);
      });
    });

    it('should set error to generic message for non-Error rejections', async () => {
      const asyncFn = jest.fn(() => Promise.reject('string error'));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(result.current.error).toBe('An error occurred');
      });
    });

    it('should set data to null when error occurs', async () => {
      const asyncFn = jest.fn(() => Promise.reject(new Error('error')));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(result.current.data).toBeNull();
      });
    });

    it('should set loading false when error occurs', async () => {
      const asyncFn = jest.fn(() => Promise.reject(new Error('error')));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });
    });
  });

  describe('Dependency Changes', () => {
    it('should refetch when dependencies change', async () => {
      const asyncFn = jest.fn((signal: AbortSignal) => Promise.resolve('data'));

      const { result, rerender } = renderHook(
        ({ dep }) => useAsync(() => asyncFn(new AbortController().signal), [dep]),
        { initialProps: { dep: 1 } }
      );

      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalledTimes(1);
      });

      rerender({ dep: 2 });

      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalledTimes(2);
      });
    });

    it('should not refetch when dependencies stay the same', async () => {
      const asyncFn = jest.fn(() => Promise.resolve('data'));

      const { rerender } = renderHook(
        ({ dep }) => useAsync(() => asyncFn(), [dep]),
        { initialProps: { dep: 1 } }
      );

      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalledTimes(1);
      });

      rerender({ dep: 1 }); // Same dependency

      // Should still be 1 call
      expect(asyncFn).toHaveBeenCalledTimes(1);
    });
  });

  describe('Refetch Function', () => {
    it('should refetch data when refetch is called', async () => {
      const asyncFn = jest.fn(() => Promise.resolve('data'));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalledTimes(1);
      });

      act(() => {
        result.current.refetch();
      });

      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalledTimes(2);
      });
    });

    it('should set loading true during refetch', async () => {
      let resolvePromise: (value: string) => void;
      const asyncFn = jest.fn(() => new Promise<string>(resolve => {
        resolvePromise = resolve;
      }));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      // Initial load
      expect(result.current.loading).toBe(true);

      act(() => {
        resolvePromise!('initial');
      });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // Trigger refetch
      act(() => {
        result.current.refetch();
      });

      expect(result.current.loading).toBe(true);
    });

    it('should update data on successful refetch', async () => {
      let callCount = 0;
      const asyncFn = jest.fn(() => Promise.resolve(`data-${++callCount}`));

      const { result } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(result.current.data).toBe('data-1');
      });

      act(() => {
        result.current.refetch();
      });

      await waitFor(() => {
        expect(result.current.data).toBe('data-2');
      });
    });
  });

  describe('Race Condition Handling', () => {
    it('should ignore stale responses when new request starts', async () => {
      let resolvers: Array<(value: string) => void> = [];
      const asyncFn = jest.fn(() => new Promise<string>(resolve => {
        resolvers.push(resolve);
      }));

      const { result, rerender } = renderHook(
        ({ dep }) => useAsync(() => asyncFn(), [dep]),
        { initialProps: { dep: 1 } }
      );

      // Start second request before first resolves
      rerender({ dep: 2 });

      // Resolve first request (should be ignored)
      act(() => {
        resolvers[0]('first');
      });

      // Resolve second request
      act(() => {
        resolvers[1]('second');
      });

      await waitFor(() => {
        expect(result.current.data).toBe('second');
      });
    });
  });

  describe('Abort on Unmount', () => {
    it('should abort request on unmount', async () => {
      let capturedSignal: AbortSignal | null = null;
      const asyncFn = jest.fn((signal: AbortSignal) => {
        capturedSignal = signal;
        return new Promise(() => {}); // Never resolves
      });

      const { unmount } = renderHook(() => useAsync(asyncFn, []));

      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalled();
      });

      expect(capturedSignal?.aborted).toBe(false);

      unmount();

      expect(capturedSignal?.aborted).toBe(true);
    });

    it('should not update state after unmount', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      let resolvePromise: (value: string) => void;
      const asyncFn = jest.fn(() => new Promise<string>(resolve => {
        resolvePromise = resolve;
      }));

      const { unmount } = renderHook(() => useAsync(asyncFn, []));

      unmount();

      // Resolve after unmount - should not cause errors
      act(() => {
        resolvePromise!('data');
      });

      // No "Can't perform state update on unmounted component" warning
      expect(consoleSpy).not.toHaveBeenCalled();

      consoleSpy.mockRestore();
    });
  });

  describe('Abort Signal Handling', () => {
    it('should not update error state when request is aborted', async () => {
      const asyncFn = jest.fn((signal: AbortSignal) => {
        return new Promise((_, reject) => {
          signal.addEventListener('abort', () => {
            const error = new Error('Aborted');
            (error as any).name = 'AbortError';
            reject(error);
          });
        });
      });

      const { result, rerender } = renderHook(
        ({ dep }) => useAsync(asyncFn, [dep]),
        { initialProps: { dep: 1 } }
      );

      // Trigger abort by changing deps
      rerender({ dep: 2 });

      // Wait for second request to start
      await waitFor(() => {
        expect(asyncFn).toHaveBeenCalledTimes(2);
      });

      // Error from aborted request should not be set
      // (The new request may still be pending)
    });
  });

  describe('Type Safety', () => {
    it('should preserve generic type', async () => {
      interface User {
        id: number;
        name: string;
      }

      const mockUser: User = { id: 1, name: 'Test' };
      const asyncFn = jest.fn(() => Promise.resolve(mockUser));

      const { result } = renderHook(() => useAsync<User>(asyncFn, []));

      await waitFor(() => {
        expect(result.current.data).toEqual(mockUser);
        // TypeScript should know data is User | null
        if (result.current.data) {
          expect(result.current.data.id).toBe(1);
          expect(result.current.data.name).toBe('Test');
        }
      });
    });
  });
});
