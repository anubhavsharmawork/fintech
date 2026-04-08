import * as React from 'react';
import { useToast } from './Toast';
import { fetchNotifications as fetchNotificationsApi, markAllRead as markAllReadApi } from '../services/notificationService';
import { useAppContext } from '../context/AppContext';

interface NotificationEvent {
  id: string;
  eventType: string;
  message: string;
  timestamp: string;
  read: boolean;
}

const NotificationBell: React.FC = () => {
  const { unreadNotificationCount, setUnreadNotificationCount } = useAppContext();
  const toast = useToast();
  const [open, setOpen] = React.useState(false);
  const [notifications, setNotifications] = React.useState<NotificationEvent[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const dropdownRef = React.useRef<HTMLDivElement>(null);
  const surfacedRef = React.useRef<Set<string>>(new Set());

  // Close on outside click
  React.useEffect(() => {
    if (!open) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [open]);

  // Fetch notifications when dropdown opens
  React.useEffect(() => {
    if (!open) return;
    const controller = new AbortController();

    const fetchNotifications = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await fetchNotificationsApi();
        if (controller.signal.aborted) return;
        setNotifications(data);

        // Surface high-severity events as toasts (only once per notification)
        for (const n of data as NotificationEvent[]) {
          if (n.read || surfacedRef.current.has(n.id)) continue;
          surfacedRef.current.add(n.id);
          if (n.eventType === 'SuspiciousActivityFlagged') {
            toast.critical('Suspicious activity detected on your account. Please review your recent transactions immediately.');
          } else if (n.eventType === 'KycStatusChanged' && /reject/i.test(n.message)) {
            toast.critical('Your KYC verification was rejected. Please contact compliance support.');
          } else if (n.eventType === 'KycStatusChanged') {
            toast.warning(n.message);
          } else {
            toast.info(n.message);
          }
        }
      } catch (err: any) {
        if (err.name !== 'AbortError') {
          setError(err.message || 'Failed to load notifications');
        }
      } finally {
        setLoading(false);
      }
    };

    fetchNotifications();
    return () => controller.abort();
  }, [open]);

  const handleMarkAllRead = () => {
    setUnreadNotificationCount(0);
    setNotifications(prev => prev.map(n => ({ ...n, read: true })));
    markAllReadApi();
  };

  const formatTimestamp = (ts: string): string => {
    try {
      const date = new Date(ts);
      const now = new Date();
      const diffMs = now.getTime() - date.getTime();
      const diffMin = Math.floor(diffMs / 60000);
      const diffHr = Math.floor(diffMin / 60);
      const diffDay = Math.floor(diffHr / 24);

      if (diffMin < 1) return 'Just now';
      if (diffMin < 60) return `${diffMin}m ago`;
      if (diffHr < 24) return `${diffHr}h ago`;
      if (diffDay < 7) return `${diffDay}d ago`;
      return date.toLocaleDateString();
    } catch {
      return ts;
    }
  };

  const formatEventName = (eventType: string): string => {
    return eventType
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, s => s.toUpperCase())
      .trim();
  };

  return (
    <div ref={dropdownRef} style={{ position: 'relative', display: 'inline-flex', alignItems: 'center' }}>
      <button
        type="button"
        onClick={() => setOpen(prev => !prev)}
        aria-label="Notifications"
        aria-expanded={open}
        style={{
          background: 'transparent',
          border: 'none',
          cursor: 'pointer',
          padding: '6px',
          borderRadius: 8,
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          position: 'relative',
          color: 'var(--primary)',
          transition: 'background 0.2s ease'
        }}
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.73 21a2 2 0 0 1-3.46 0" />
        </svg>
        {unreadNotificationCount > 0 && (
          <span
            aria-label={`${unreadNotificationCount} unread notifications`}
            style={{
              position: 'absolute',
              top: 4,
              right: 4,
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: '#ef4444',
              zIndex: 'var(--z-overlay)'
            }}
          />
        )}
      </button>

      {open && (
        <div
          style={{
            position: 'absolute',
            top: '100%',
            right: 0,
            marginTop: 8,
            minWidth: 320,
            maxWidth: 360,
            background: 'var(--card)',
            border: '1px solid var(--border)',
            borderRadius: 'var(--radius-card)',
            boxShadow: 'var(--shadow)',
            zIndex: 'var(--z-overlay)',
            overflow: 'hidden'
          }}
        >
          <div style={{
            padding: '0.75rem 1rem',
            borderBottom: '1px solid var(--border)',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center'
          }}>
            <span style={{ fontWeight: 600, fontSize: '0.9rem', color: 'var(--text)' }}>Notifications</span>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleMarkAllRead}
              style={{ padding: '4px 10px', fontSize: '0.75rem' }}
            >
              Mark all read
            </button>
          </div>

          <div style={{ maxHeight: 320, overflowY: 'auto' }}>
            {loading && (
              <div style={{ padding: '1rem' }}>
                <div className="skeleton-row">
                  <div className="skeleton-cell" style={{ flex: 1 }} />
                </div>
                <div className="skeleton-row">
                  <div className="skeleton-cell" style={{ flex: 1 }} />
                </div>
              </div>
            )}

            {error && (
              <div style={{ padding: '1rem', color: '#b91c1c', fontSize: '0.85rem' }}>
                {error}
              </div>
            )}

            {!loading && !error && notifications.length === 0 && (
              <div style={{ padding: '1.5rem 1rem', textAlign: 'center', color: 'var(--muted)', fontSize: '0.85rem' }}>
                No recent notifications
              </div>
            )}

            {!loading && !error && notifications.map(n => (
              <div
                key={n.id}
                style={{
                  padding: '0.75rem 1rem',
                  borderBottom: '1px solid var(--border)',
                  background: n.read ? 'transparent' : 'rgba(37, 99, 235, 0.04)'
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                  <span style={{
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    color: 'var(--primary)',
                    textTransform: 'uppercase',
                    letterSpacing: '0.02em'
                  }}>
                    {formatEventName(n.eventType)}
                  </span>
                  <span style={{ fontSize: '0.7rem', color: 'var(--muted)', whiteSpace: 'nowrap' }}>
                    {formatTimestamp(n.timestamp)}
                  </span>
                </div>
                <p style={{ margin: '4px 0 0', fontSize: '0.85rem', color: 'var(--text)', lineHeight: 1.4 }}>
                  {n.message}
                </p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

export default NotificationBell;
