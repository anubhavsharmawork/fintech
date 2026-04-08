import * as React from 'react';

/* ??? Types ??? */
export type ToastType = 'success' | 'error' | 'warning' | 'info' | 'critical';

export type Toast = {
  id: number;
  type: ToastType;
  message: string;
  action?: { label: string; onClick: () => void };
};

interface ToastContextValue {
  success: (message: string) => void;
  error: (message: string) => void;
  warning: (message: string, action?: { label: string; onClick: () => void }) => void;
  info: (message: string) => void;
  critical: (message: string) => void;
}

const noop = () => {};

const ToastContext = React.createContext<ToastContextValue>({
  success: noop,
  error: noop,
  warning: noop,
  info: noop,
  critical: noop,
});

export const useToast = () => {
  const ctx = React.useContext(ToastContext);
  return ctx;
};

/* ??? Auto-dismiss durations (ms) ??? */
const AUTO_DISMISS: Record<ToastType, number | null> = {
  success: 4000,
  error: null,
  warning: 8000,
  info: 5000,
  critical: null,
};

const MAX_VISIBLE = 4;

/* ??? Shield icon for critical banner ??? */
const ShieldIcon = () => (
  <svg
    width="20"
    height="20"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
  >
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
  </svg>
);

/* ??? Toast type icons ??? */
const ToastIcon: React.FC<{ type: string }> = ({ type }) => {
  const size = 16;
  const props = { width: size, height: size, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const, 'aria-hidden': true as const };
  switch (type) {
    case 'success':
      return <svg {...props} className="toast-icon toast-icon-success"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>;
    case 'error':
      return <svg {...props} className="toast-icon toast-icon-error"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>;
    case 'warning':
      return <svg {...props} className="toast-icon toast-icon-warning"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>;
    case 'info':
      return <svg {...props} className="toast-icon toast-icon-info"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>;
    default:
      return null;
  }
};

/* ??? Single toast item (non-critical) ??? */
const ToastItem: React.FC<{
  toast: Toast;
  onDismiss: (id: number) => void;
  exiting: boolean;
}> = ({ toast, onDismiss, exiting }) => {
  const classMap: Record<string, string> = {
    success: 'toast-success',
    error: 'toast-error',
    warning: 'toast-warning',
    info: 'toast-info',
  };

  return (
    <div
      className={`toast ${classMap[toast.type] || ''} ${exiting ? 'toast-exit' : ''}`}
      role={toast.type === 'error' || toast.type === 'warning' ? 'alert' : 'status'}
    >
      <ToastIcon type={toast.type} />
      <span className="toast-msg">{toast.message}</span>
      {toast.type === 'error' && (
        <span className="toast-support">Contact support</span>
      )}
      {toast.type === 'warning' && toast.action && (
        <button
          className="toast-action"
          onClick={() => {
            toast.action!.onClick();
            onDismiss(toast.id);
          }}
        >
          {toast.action.label}
        </button>
      )}
      <button
        className="toast-close"
        aria-label="Dismiss"
        onClick={() => onDismiss(toast.id)}
      >
        ×
      </button>
    </div>
  );
};

/* ??? Critical banner (full-width, top of viewport) ??? */
const CriticalBanner: React.FC<{
  toast: Toast;
  onDismiss: (id: number) => void;
}> = ({ toast, onDismiss }) => (
  <div className="toast-critical-banner" role="alert">
    <div className="toast-critical-banner-content">
      <ShieldIcon />
      <span className="toast-critical-banner-msg">{toast.message}</span>
      <button
        className="toast-critical-banner-dismiss"
        onClick={() => onDismiss(toast.id)}
      >
        Dismiss
      </button>
    </div>
  </div>
);

/* ??? Provider ??? */
export const ToastProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [toasts, setToasts] = React.useState<Toast[]>([]);
  const [criticalQueue, setCriticalQueue] = React.useState<Toast[]>([]);
  const [exitingIds, setExitingIds] = React.useState<Set<number>>(new Set());
  const idRef = React.useRef(0);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const timersRef = React.useRef(new Map<number, any>());

  const animateOut = React.useCallback((id: number) => {
    setExitingIds((prev) => new Set(prev).add(id));
    const prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const delay = prefersReduced ? 0 : 280;
    window.setTimeout(() => {
      setToasts((t) => t.filter((x) => x.id !== id));
      setExitingIds((prev) => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
    }, delay);
  }, []);

  const remove = React.useCallback(
    (id: number) => {
      const timer = timersRef.current.get(id);
      if (timer) {
        clearTimeout(timer);
        timersRef.current.delete(id);
      }
      animateOut(id);
    },
    [animateOut],
  );

  const dismissCritical = React.useCallback((id: number) => {
    setCriticalQueue((q) => q.filter((x) => x.id !== id));
  }, []);

  const push = React.useCallback(
    (type: ToastType, message: string, action?: { label: string; onClick: () => void }) => {
      const id = ++idRef.current;
      const toast: Toast = { id, type, message, action };

      if (type === 'critical') {
        setCriticalQueue((q) => [...q, toast]);
        return;
      }

      setToasts((prev) => {
        let next = [...prev, toast];
        if (next.length > MAX_VISIBLE) {
          const nonPersistentIdx = next.findIndex(
            (t) => AUTO_DISMISS[t.type] !== null,
          );
          if (nonPersistentIdx !== -1) {
            const evicted = next[nonPersistentIdx];
            const evictTimer = timersRef.current.get(evicted.id);
            if (evictTimer) {
              clearTimeout(evictTimer);
              timersRef.current.delete(evicted.id);
            }
            next = next.filter((_, i) => i !== nonPersistentIdx);
          }
        }
        return next;
      });

      const duration = AUTO_DISMISS[type];
      if (duration !== null) {
        const timer = window.setTimeout(() => {
          timersRef.current.delete(id);
          animateOut(id);
        }, duration);
        timersRef.current.set(id, timer);
      }
    },
    [animateOut],
  );

  React.useEffect(() => {
    const currentTimers = timersRef.current;
    return () => {
      currentTimers.forEach((t) => clearTimeout(t));
    };
  }, []);

  const success = React.useCallback((message: string) => push('success', message), [push]);
  const error = React.useCallback((message: string) => push('error', message), [push]);
  const warning = React.useCallback(
    (message: string, action?: { label: string; onClick: () => void }) =>
      push('warning', message, action),
    [push],
  );
  const info = React.useCallback((message: string) => push('info', message), [push]);
  const critical = React.useCallback((message: string) => push('critical', message), [push]);

  const activeCritical = criticalQueue.length > 0 ? criticalQueue[0] : null;

  return (
    <ToastContext.Provider value={{ success, error, warning, info, critical }}>
      {children}

      {/* Critical banner × full-width, top of viewport, above everything */}
      {activeCritical && (
        <CriticalBanner toast={activeCritical} onDismiss={dismissCritical} />
      )}

      {/* Non-critical toast stack × top-right */}
      <div className="toast-container" aria-live="polite" aria-atomic="true">
        {toasts.map((t) => (
          <ToastItem
            key={t.id}
            toast={t}
            onDismiss={remove}
            exiting={exitingIds.has(t.id)}
          />
        ))}
      </div>
    </ToastContext.Provider>
  );
};
