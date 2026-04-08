import { useState, useEffect, useCallback, useRef } from 'react';

interface AsyncState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

/**
 * Generic hook for handling async operations with proper race condition handling.
 * Uses AbortController cleanup in useEffect to prevent state updates on unmounted components.
 */
export function useAsync<T>(
  fn: (signal: AbortSignal) => Promise<T>,
  deps: React.DependencyList
): AsyncState<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const mountedRef = useRef(true);
  const fetchIdRef = useRef(0);

  const execute = useCallback(async (signal: AbortSignal) => {
    const fetchId = ++fetchIdRef.current;

    setLoading(true);
    setError(null);

    try {
      const result = await fn(signal);

      // Only update state if this is the latest request and component is mounted
      if (fetchId === fetchIdRef.current && mountedRef.current && !signal.aborted) {
        setData(result);
        setError(null);
      }
    } catch (err: unknown) {
      // Don't update state if aborted
      if (signal.aborted) return;

      if (fetchId === fetchIdRef.current && mountedRef.current) {
        const message = err instanceof Error ? err.message : 'An error occurred';
        setError(message);
        setData(null);
      }
    } finally {
      if (fetchId === fetchIdRef.current && mountedRef.current && !signal.aborted) {
        setLoading(false);
      }
    }
  }, [fn]);

  useEffect(() => {
    mountedRef.current = true;
    const controller = new AbortController();

    execute(controller.signal);

    return () => {
      mountedRef.current = false;
      controller.abort();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  const refetch = useCallback(() => {
    const controller = new AbortController();
    execute(controller.signal);

    // Return cleanup function for manual abort if needed
    return () => controller.abort();
  }, [execute]);

  return { data, loading, error, refetch };
}

export default useAsync;
