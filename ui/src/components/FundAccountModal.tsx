import * as React from 'react';
import { CheckCircle } from 'lucide-react';
import {
  getExternalAccounts,
  depositFromExternal,
  ExternalBankAccount
} from '../services/banking';

interface FundAccountModalProps {
  accountId: string;
  accountType: string;
  currency: string;
  onComplete: () => void;
  onCancel: () => void;
}

const MAX_DEPOSIT = 1_000_000;
const AMOUNT_PATTERN = /^\d+(\.\d{0,2})?$/;

const FundAccountModal: React.FC<FundAccountModalProps> = ({
  accountId,
  accountType,
  currency,
  onComplete,
  onCancel
}) => {
  const [externalAccounts, setExternalAccounts] = React.useState<ExternalBankAccount[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [selectedExternal, setSelectedExternal] = React.useState('');
  const [amount, setAmount] = React.useState('');
  const [submitting, setSubmitting] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [success, setSuccess] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const accounts = await getExternalAccounts();
        if (!cancelled) {
          setExternalAccounts(accounts);
          if (accounts.length > 0) setSelectedExternal(accounts[0].id);
        }
      } catch (err: any) {
        if (!cancelled) setError(err.message || 'Failed to load external accounts');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const parsedAmount = parseFloat(amount);
  const isAmountValid = amount.length > 0 && AMOUNT_PATTERN.test(amount) && parsedAmount > 0 && parsedAmount <= MAX_DEPOSIT;
  const selectedAccount = externalAccounts.find(a => a.id === selectedExternal);
  const insufficientExternal = selectedAccount ? parsedAmount > selectedAccount.balance : false;
  const canSubmit = isAmountValid && selectedExternal && !submitting && !insufficientExternal;

  const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: currency || 'NZD' });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setSubmitting(true);
    setError(null);
    try {
      await depositFromExternal(accountId, selectedExternal, parsedAmount);
      setSuccess(true);
      setTimeout(() => onComplete(), 1500);
    } catch (err: any) {
      setError(err.message || 'Deposit failed');
    } finally {
      setSubmitting(false);
    }
  };

  if (success) {
    return (
      <div style={{ padding: 20, background: '#ecfdf5', borderRadius: 10, border: '1px solid #10b98130', marginTop: 12 }}>
        <p style={{ margin: 0, color: '#065f46', fontWeight: 600 }}>
          <CheckCircle size={16} color="currentColor" style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4 }} /> {nzd.format(parsedAmount)} deposited successfully into your {accountType} account.
        </p>
      </div>
    );
  }

  if (loading) {
    return (
      <div style={{ padding: 16, marginTop: 12 }}>
        <span className="spinner" style={{ marginRight: 8 }} />Loading external accounts...
      </div>
    );
  }

  if (externalAccounts.length === 0) {
    return (
      <div style={{ padding: 16, background: '#FEF2F2', borderRadius: 10, border: '1px solid #ef444430', marginTop: 12 }}>
        <p style={{ margin: 0, color: '#991B1B', fontWeight: 500 }}>
          No external bank accounts found. Connect an external bank first using the <strong>External Bank Accounts</strong> section above, then return here to fund your account.
        </p>
        <button className="btn btn-secondary" onClick={onCancel} style={{ marginTop: 12, fontSize: 14, padding: '8px 16px' }}>
          Close
        </button>
      </div>
    );
  }

  return (
    <div style={{ marginTop: 12, padding: 16, background: 'var(--bg)', borderRadius: 10, border: '1px solid var(--border)' }}>
      <h4 style={{ margin: '0 0 12px 0' }}>Fund from External Bank</h4>
      {error && (
        <div style={{ padding: '10px 14px', background: '#FEF2F2', borderRadius: 8, border: '1px solid #ef444430', marginBottom: 12 }}>
          <p style={{ margin: 0, color: '#991B1B', fontSize: 14 }}>{error}</p>
        </div>
      )}
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor={`ext-source-${accountId}`}>Source Account</label>
          <select
            id={`ext-source-${accountId}`}
            value={selectedExternal}
            onChange={e => { setSelectedExternal(e.target.value); setError(null); }}
            aria-label="Select source external account"
          >
            {externalAccounts.map(acc => (
              <option key={acc.id} value={acc.id}>
                {acc.bankName} — {acc.accountName} ({acc.accountType}) • {nzd.format(acc.balance)}
              </option>
            ))}
          </select>
        </div>
        <div className="form-group">
          <label htmlFor={`fund-amount-${accountId}`}>Amount ({currency})</label>
          <input
            id={`fund-amount-${accountId}`}
            type="text"
            inputMode="decimal"
            placeholder="0.00"
            value={amount}
            onChange={e => {
              const val = e.target.value;
              if (val === '' || /^\d*\.?\d{0,2}$/.test(val)) {
                setAmount(val);
                setError(null);
              }
            }}
            aria-label="Deposit amount"
            aria-invalid={amount.length > 0 && !isAmountValid}
            style={{
              borderColor: amount.length > 0 && !isAmountValid ? '#b91c1c' : undefined
            }}
          />
          {amount.length > 0 && parsedAmount > MAX_DEPOSIT && (
            <p style={{ color: '#b91c1c', fontSize: 13, marginTop: 4 }}>
              Maximum single deposit is {nzd.format(MAX_DEPOSIT)}
            </p>
          )}
          {insufficientExternal && selectedAccount && (
            <p style={{ color: '#b91c1c', fontSize: 13, marginTop: 4 }}>
              Insufficient balance in source account ({nzd.format(selectedAccount.balance)} available)
            </p>
          )}
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button
            className="btn btn-primary"
            type="submit"
            disabled={!canSubmit}
            style={{ fontSize: 14, padding: '10px 20px' }}
          >
            {submitting ? (
              <><span className="spinner" style={{ marginRight: 6, width: 14, height: 14, borderWidth: 2 }} />Processing...</>
            ) : (
              `Deposit ${isAmountValid ? nzd.format(parsedAmount) : ''}`
            )}
          </button>
          <button
            className="btn"
            type="button"
            onClick={onCancel}
            style={{ fontSize: 14, padding: '10px 20px', background: 'var(--border)', color: 'var(--text)' }}
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
};

export default FundAccountModal;
