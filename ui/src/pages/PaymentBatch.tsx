import * as React from 'react';
import { Link } from 'react-router-dom';
import {
  getPaymentBatches,
  createPaymentBatch,
  submitBatchForApproval,
  PaymentBatch,
  PaymentBatchItem
} from '../services/corporate';
import { usePagination } from '../hooks/usePagination';
import Pagination from '../components/Pagination';
import TableSkeleton from '../components/TableSkeleton';

interface DraftItem {
  sourceAccountId: string;
  payeeName: string;
  payeeAccountNumber: string;
  amount: string;
  description: string;
}

const emptyItem: DraftItem = { sourceAccountId: '', payeeName: '', payeeAccountNumber: '', amount: '', description: '' };

const PaymentBatchPage: React.FC = () => {
  const [batches, setBatches] = React.useState<PaymentBatch[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [showCreate, setShowCreate] = React.useState(false);
  const [currency, setCurrency] = React.useState('$');
  const [items, setItems] = React.useState<DraftItem[]>([{ ...emptyItem }]);
  const [creating, setCreating] = React.useState(false);
  const [submitting, setSubmitting] = React.useState<string | null>(null);
  const pagination = usePagination({ defaultPageSize: 10, syncToUrl: true });

  // Update pagination total when batches change
  React.useEffect(() => {
    pagination.setTotalCount(batches.length);
  }, [batches.length, pagination]);

  // Paginated batches
  const paginatedBatches = React.useMemo(() => {
    const start = (pagination.page - 1) * pagination.pageSize;
    return batches.slice(start, start + pagination.pageSize);
  }, [batches, pagination.page, pagination.pageSize]);

  const fmtAmount = (amount: number, cur: string) => {
    if (cur === 'FTK') return `${amount.toLocaleString('en-NZ', { minimumFractionDigits: 2 })} FTK`;
    return `$${amount.toLocaleString('en-NZ', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getPaymentBatches();
      setBatches(data);
    } catch (e: any) {
      setError(e.message || 'Failed to load batches');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => { load(); }, [load]);

  const updateItem = (idx: number, field: keyof DraftItem, value: string) => {
    setItems(prev => prev.map((item, i) => i === idx ? { ...item, [field]: value } : item));
  };

  const addItem = () => setItems(prev => [...prev, { ...emptyItem }]);
  const removeItem = (idx: number) => setItems(prev => prev.filter((_, i) => i !== idx));

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    setError(null);
    try {
      const paymentItems: PaymentBatchItem[] = items
        .filter(i => i.payeeName && i.amount)
        .map(i => ({
          sourceAccountId: i.sourceAccountId || '',
          payeeName: i.payeeName,
          payeeAccountNumber: i.payeeAccountNumber || null,
          amount: parseFloat(i.amount),
          description: i.description || null
        }));
      if (paymentItems.length === 0) {
        setError('Add at least one payment item');
        return;
      }
      await createPaymentBatch(currency, paymentItems);
      setShowCreate(false);
      setItems([{ ...emptyItem }]);
      await load();
    } catch (e: any) {
      setError(e.message || 'Failed to create batch');
    } finally {
      setCreating(false);
    }
  };

  const handleSubmit = async (batchId: string) => {
    setSubmitting(batchId);
    setError(null);
    try {
      await submitBatchForApproval(batchId);
      await load();
    } catch (e: any) {
      setError(e.message || 'Failed to submit batch');
    } finally {
      setSubmitting(null);
    }
  };

  const statusColor: Record<string, string> = {
    Draft: '#6b7280',
    PendingApproval: '#eab308',
    Approved: '#059669',
    Rejected: '#dc2626',
    Executed: '#2563eb',
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h2>Payment Batches</h2>
        <button className="btn btn-primary" onClick={() => setShowCreate(!showCreate)}>
          {showCreate ? 'Cancel' : '+ New Batch'}
        </button>
      </div>

      {error && <div className="alert alert-error" style={{ marginBottom: 16 }}>{error}</div>}

      {showCreate && (
        <div className="card" style={{ marginBottom: 24 }}>
          <h3>Create Payment Batch</h3>
          <form onSubmit={handleCreate}>
            <div className="form-group">
              <label>Currency</label>
              <select value={currency} onChange={e => setCurrency(e.target.value)}>
                <option value="$">$ (Dollar)</option>
                <option value="FTK">FTK</option>
              </select>
            </div>

            <h4 style={{ marginTop: 16 }}>Payees</h4>
            {items.map((item, idx) => (
              <div key={idx} style={{ border: '1px solid var(--border)', borderRadius: 8, padding: 12, marginBottom: 12 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
                  <strong>Payment #{idx + 1}</strong>
                  {items.length > 1 && (
                    <button type="button" className="btn btn-secondary" style={{ fontSize: 12, padding: '4px 8px' }} onClick={() => removeItem(idx)}>
                      Remove
                    </button>
                  )}
                </div>
                <div className="form-group">
                  <label>Payee Name</label>
                  <input type="text" value={item.payeeName} onChange={e => updateItem(idx, 'payeeName', e.target.value)} required placeholder="Jane Smith" />
                </div>
                <div className="form-group">
                  <label>Account Number</label>
                  <input type="text" value={item.payeeAccountNumber} onChange={e => updateItem(idx, 'payeeAccountNumber', e.target.value)} placeholder="12-3456-7890123-00" />
                </div>
                <div className="form-group">
                  <label>Amount</label>
                  <input type="number" step="0.01" min="0.01" value={item.amount} onChange={e => updateItem(idx, 'amount', e.target.value)} required placeholder="1000.00" />
                </div>
                <div className="form-group">
                  <label>Description</label>
                  <input type="text" value={item.description} onChange={e => updateItem(idx, 'description', e.target.value)} placeholder="Invoice #123" />
                </div>
              </div>
            ))}
            <button type="button" className="btn btn-secondary" onClick={addItem} style={{ marginBottom: 16 }}>
              + Add Payee
            </button>
            <div>
              <button type="submit" className="btn btn-primary" disabled={creating}>
                {creating ? 'Creating...' : 'Create Batch'}
              </button>
            </div>
          </form>
        </div>
      )}

      {loading && <TableSkeleton rows={10} columns={4} />}

      {!loading && batches.length === 0 && !showCreate && (
        <div className="card" style={{ textAlign: 'center', padding: 32 }}>
          <p className="small">No payment batches yet. Create one to get started.</p>
        </div>
      )}

      {!loading && batches.length > 0 && (
        <div>
          {paginatedBatches.map(b => (
            <div key={b.id} className="card" style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                  <h4 style={{ margin: '0 0 4px 0' }}>{b.currency === 'FTK' ? 'FTK' : '$'} Payment Batch</h4>
                  <p className="small" style={{ margin: '0 0 4px 0' }}>
                    {b.itemCount} payment{b.itemCount !== 1 ? 's' : ''} &middot; Created {new Date(b.createdAt).toLocaleDateString()}
                  </p>
                  <span
                    style={{
                      display: 'inline-block',
                      padding: '2px 10px',
                      borderRadius: 12,
                      fontSize: '0.75rem',
                      fontWeight: 600,
                      color: '#fff',
                      background: statusColor[b.status] || '#6b7280'
                    }}
                  >
                    {b.status}
                  </span>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <p style={{ fontSize: 20, fontWeight: 700, margin: 0 }}>{fmtAmount(b.totalAmount, b.currency)}</p>
                  {b.status === 'Draft' && (
                    <button
                      className="btn btn-primary"
                      style={{ marginTop: 8, fontSize: 13, padding: '6px 14px' }}
                      onClick={() => handleSubmit(b.id)}
                      disabled={submitting === b.id}
                    >
                      {submitting === b.id ? 'Submitting...' : 'Submit for Approval'}
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}
          <Pagination pagination={pagination} />
        </div>
      )}
    </div>
  );
};

export default PaymentBatchPage;
