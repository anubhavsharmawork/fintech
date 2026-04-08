import * as React from 'react';
import { TransactionResult, waitForTransaction, getEtherscanTxUrl } from '../services/crypto';

/* ─── Institutional badge status types ─── */
type BadgeStatus = 'pending' | 'processing' | 'cleared' | 'completed' | 'flagged' | 'reversed';

interface StatusConfig {
  label: string;
  color: string;
  bg: string;
  border: string;
  surface: string;
  icon: React.ReactNode;
}

/* ─── SVG icons (12×12, currentColor) ─── */
const ClockIcon = () => (
  <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden="true">
    <circle cx="6" cy="6" r="5" stroke="currentColor" strokeWidth="1.5" fill="none" />
    <path d="M6 3v3l2 1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
  </svg>
);

const CheckIcon = () => (
  <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden="true">
    <path d="M2.5 6l2.5 2.5L9.5 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const WarningIcon = () => (
  <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden="true">
    <path d="M6 1.5l4.5 8H1.5L6 1.5z" stroke="currentColor" strokeWidth="1.2" strokeLinejoin="round" fill="none" />
    <line x1="6" y1="5" x2="6" y2="7" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
    <circle cx="6" cy="8.5" r="0.5" fill="currentColor" />
  </svg>
);

const UndoIcon = () => (
  <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden="true">
    <path d="M2.5 4.5h5a2.5 2.5 0 010 5H5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    <path d="M4.5 2.5l-2 2 2 2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const PulseDot = () => (
  <span
    aria-hidden="true"
    className="tx-pulse-dot"
    style={{
      display: 'inline-block',
      width: 6,
      height: 6,
      borderRadius: '50%',
      backgroundColor: 'currentColor',
    }}
  />
);

/* ─── Status configuration map ─── */
const STATUS_MAP: Record<BadgeStatus, StatusConfig> = {
  pending:    { label: 'Pending',    color: 'var(--tx-amber)',   bg: 'transparent',       border: 'var(--tx-amber)',  surface: 'var(--tx-surface-pending)',    icon: <ClockIcon /> },
  processing: { label: 'Processing', color: 'var(--tx-on-fill)', bg: 'var(--tx-blue)',    border: 'var(--tx-blue)',   surface: 'var(--tx-surface-processing)', icon: <PulseDot /> },
  cleared:    { label: 'Cleared',    color: 'var(--tx-on-fill)', bg: 'var(--tx-green)',   border: 'var(--tx-green)',  surface: 'var(--tx-surface-success)',    icon: <CheckIcon /> },
  completed:  { label: 'Completed',  color: 'var(--tx-on-fill)', bg: 'var(--tx-green)',   border: 'var(--tx-green)',  surface: 'var(--tx-surface-success)',    icon: <CheckIcon /> },
  flagged:    { label: 'Flagged',    color: 'var(--tx-on-fill)', bg: 'var(--tx-red)',     border: 'var(--tx-red)',    surface: 'var(--tx-surface-error)',      icon: <WarningIcon /> },
  reversed:   { label: 'Reversed',   color: 'var(--tx-gray)',    bg: 'transparent',       border: 'var(--tx-gray)',   surface: 'var(--tx-surface-neutral)',    icon: <UndoIcon /> },
};

function resolveBadgeStatus(s: 'pending' | 'confirming' | 'confirmed' | 'failed'): BadgeStatus {
  switch (s) {
    case 'pending':    return 'pending';
    case 'confirming': return 'processing';
    case 'confirmed':  return 'completed';
    case 'failed':     return 'flagged';
  }
}

/* ─── StatusBadge (20px height, text-xs) ─── */
const StatusBadge: React.FC<{ status: BadgeStatus }> = ({ status }) => {
  const cfg = STATUS_MAP[status];
  return (
    <span
      role="status"
      aria-label={`Transaction status: ${cfg.label}`}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 4,
        height: 20,
        padding: '0 8px',
        fontSize: 12,
        fontWeight: 500,
        lineHeight: 1,
        borderRadius: 4,
        border: `1px solid ${cfg.border}`,
        backgroundColor: cfg.bg,
        color: cfg.color,
        whiteSpace: 'nowrap',
      }}
    >
      {cfg.icon}
      {cfg.label}
    </span>
  );
};

/* ─── Pulse keyframe injection ─── */
const PULSE_STYLE_ID = 'tx-status-pulse-style';
function ensurePulseStyle() {
  if (typeof document === 'undefined') return;
  if (document.getElementById(PULSE_STYLE_ID)) return;
  const style = document.createElement('style');
  style.id = PULSE_STYLE_ID;
  style.textContent =
    '@keyframes txStatusPulse{0%,100%{opacity:1}50%{opacity:.3}}' +
    '.tx-pulse-dot{animation:txStatusPulse 1.5s ease-in-out infinite}';
  document.head.appendChild(style);
}

interface Props {
  transaction: TransactionResult | null;
  provider: any;
  onConfirmed?: (result: TransactionResult) => void;
  onFailed?: (error: string) => void;
}

const TransactionStatus: React.FC<Props> = ({ transaction, provider, onConfirmed, onFailed }) => {
  const [status, setStatus] = React.useState<'pending' | 'confirming' | 'confirmed' | 'failed'>('pending');
  const [confirmations, setConfirmations] = React.useState(0);
  const [gasUsed, setGasUsed] = React.useState<string | null>(null);
  const [blockNumber, setBlockNumber] = React.useState<number | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [showFlagToast, setShowFlagToast] = React.useState(false);

  React.useEffect(() => { ensurePulseStyle(); }, []);

  React.useEffect(() => {
    if (!transaction || !provider) return;

    let cancelled = false;

    const trackTransaction = async () => {
      setStatus('confirming');
      
      try {
        const result = await waitForTransaction(provider, transaction.hash, 1);
        
        if (cancelled) return;

        if (result.status === 'confirmed') {
          setStatus('confirmed');
          setConfirmations(1);
          setGasUsed(result.gasUsed ?? null);
          setBlockNumber(result.blockNumber ?? null);
          onConfirmed?.({
            ...transaction,
            status: 'confirmed',
            gasUsed: result.gasUsed,
            blockNumber: result.blockNumber
          });
        } else {
          setStatus('failed');
          setError('Transaction reverted on-chain');
          onFailed?.('Transaction reverted');
        }
      } catch (err: any) {
        if (cancelled) return;
        setStatus('failed');
        const message = err.message || 'Failed to confirm transaction';
        setError(message);
        onFailed?.(message);
      }
    };

    trackTransaction();

    return () => {
      cancelled = true;
    };
  }, [transaction?.hash, provider]);

  /* Flagged toast auto-dismiss */
  React.useEffect(() => {
    if (status === 'failed') {
      setShowFlagToast(true);
      const timer = setTimeout(() => setShowFlagToast(false), 5000);
      return () => clearTimeout(timer);
    }
  }, [status]);

  if (!transaction) return null;

  const shortenHash = (hash: string) => `${hash.slice(0, 10)}...${hash.slice(-8)}`;

  const badge = resolveBadgeStatus(status);
  const cfg = STATUS_MAP[badge];

  return (
    <>
      {showFlagToast && (
        <div
          role="alert"
          aria-live="assertive"
          style={{
            position: 'fixed',
            top: 16,
            right: 16,
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            padding: '10px 16px',
            backgroundColor: 'var(--tx-red)',
            color: 'var(--tx-on-fill)',
            borderRadius: 6,
            fontSize: 13,
            fontWeight: 500,
            boxShadow: '0 4px 12px var(--tx-red-shadow)',
            zIndex: 60,
          }}
        >
          <WarningIcon />
          Transaction flagged — review required
        </div>
      )}

      <div
        className="card"
        style={{
          marginTop: 16,
          padding: 16,
          backgroundColor: cfg.surface,
          boxShadow: 'var(--tx-card-elevation)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
          <StatusBadge status={badge} />
        </div>

        <div style={{ display: 'grid', gap: 8, fontSize: '0.875rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: 'var(--muted)' }}>Transaction Hash:</span>
            <a
              href={transaction.etherscanUrl}
              target="_blank"
              rel="noopener noreferrer"
              style={{ color: 'var(--primary)', fontFamily: 'monospace' }}
            >
              {shortenHash(transaction.hash)} ↗
            </a>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: 'var(--muted)' }}>From:</span>
            <span style={{ fontFamily: 'monospace', fontSize: '0.8125rem' }}>
              {shortenHash(transaction.from)}
            </span>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: 'var(--muted)' }}>To:</span>
            <span style={{ fontFamily: 'monospace', fontSize: '0.8125rem' }}>
              {shortenHash(transaction.to)}
            </span>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: 'var(--muted)' }}>Amount:</span>
            <span style={{ fontWeight: 600 }}>{transaction.amount} FTK</span>
          </div>

          {gasUsed && (
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: 'var(--muted)' }}>Gas Used:</span>
              <span>{gasUsed}</span>
            </div>
          )}

          {blockNumber && (
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: 'var(--muted)' }}>Block Number:</span>
              <span>{blockNumber.toLocaleString()}</span>
            </div>
          )}

          {confirmations > 0 && (
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: 'var(--muted)' }}>Confirmations:</span>
              <span style={{ color: 'var(--tx-green)' }}>{confirmations}</span>
            </div>
          )}
        </div>

        {error && (
          <div style={{ marginTop: 12, color: 'var(--tx-red)', fontSize: '0.8125rem' }}>
            {error}
          </div>
        )}

        <div style={{ marginTop: 16, textAlign: 'center' }}>
          <a
            href={transaction.etherscanUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="btn btn-secondary"
            style={{ textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: 6 }}
          >
            View on Etherscan
          </a>
        </div>

        {status === 'confirmed' && (
          <div
            style={{
              marginTop: 12,
              padding: '8px 12px',
              backgroundColor: 'var(--tx-success-subtle)',
              borderRadius: 4,
              fontSize: '0.8125rem',
              textAlign: 'center',
              color: 'var(--tx-green)',
            }}
          >
            Transaction recorded on Sepolia and verified on Etherscan.
          </div>
        )}
      </div>
    </>
  );
};

export default TransactionStatus;
