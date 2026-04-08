import * as React from 'react';
import { SendHorizonal } from 'lucide-react';

interface ConfirmPaymentModalProps {
  amount: string;
  payeeName: string;
  currency?: string;
  onConfirm: () => void;
  onCancel: () => void;
}

const ConfirmPaymentModal: React.FC<ConfirmPaymentModalProps> = ({
  amount,
  payeeName,
  currency = 'NZD',
  onConfirm,
  onCancel,
}) => {
  const formatted = React.useMemo(() => {
    const num = parseFloat(amount);
    if (Number.isNaN(num)) return amount;
    return new Intl.NumberFormat('en-NZ', { style: 'currency', currency }).format(num);
  }, [amount, currency]);

  React.useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCancel();
    };
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [onCancel]);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-payment-title"
      style={{
        position: 'fixed', inset: 0, zIndex: 1000,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: 'rgba(0,0,0,0.45)',
      }}
      onClick={(e) => { if (e.target === e.currentTarget) onCancel(); }}
    >
      <div
        className="card"
        style={{ maxWidth: 420, width: '90%', padding: '2rem', textAlign: 'center' }}
      >
        <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '0.75rem' }}><SendHorizonal size={40} color="currentColor" /></div>
        <h3 id="confirm-payment-title" style={{ marginTop: 0, marginBottom: '1rem' }}>
          Confirm Payment
        </h3>
        <p style={{ marginBottom: '1.5rem', fontSize: '1.05rem' }}>
          You are sending{' '}
          <strong style={{ color: 'var(--primary, #2563eb)' }}>{formatted}</strong>
          {' '}to{' '}
          <strong>{payeeName}</strong>
        </p>
        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'center' }}>
          <button
            className="btn btn-secondary"
            type="button"
            onClick={onCancel}
            style={{ minWidth: 100 }}
          >
            Cancel
          </button>
          <button
            className="btn btn-primary"
            type="button"
            onClick={onConfirm}
            style={{ minWidth: 100 }}
            autoFocus
          >
            Confirm
          </button>
        </div>
      </div>
    </div>
  );
};

export default ConfirmPaymentModal;
