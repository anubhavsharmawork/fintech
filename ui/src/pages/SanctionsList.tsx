import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { getSanctions, createSanction, SanctionRequestDto } from '../services/sanctions';
import { usePagination } from '../hooks/usePagination';
import Pagination from '../components/Pagination';
import TableSkeleton from '../components/TableSkeleton';

interface AccountInfo {
  id: string;
  accountNumber?: string;
  accountType?: string;
  currency?: string;
}

const statusColors: Record<string, string> = {
  Draft: '#4b5563',
  Submitted: 'var(--primary)',
  Screening: '#9a3412',
  Underwriting: '#9a3412',
  Approved: '#047857',
  Rejected: '#b91c1c',
  Disbursed: '#047857',
  Cancelled: '#4b5563'
};

const allStatuses = ['Draft', 'Submitted', 'Screening', 'Underwriting', 'Approved', 'Rejected', 'Disbursed', 'Cancelled'];

const SanctionsList: React.FC = () => {
  const navigate = useNavigate();
  const [sanctions, setSanctions] = React.useState<SanctionRequestDto[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [showForm, setShowForm] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);
  const pagination = usePagination({ defaultPageSize: 25, syncToUrl: true });

  const [filterStatus, setFilterStatus] = React.useState('');
  const [filterProjectId, setFilterProjectId] = React.useState('');
  const [filterDateFrom, setFilterDateFrom] = React.useState('');
  const [filterDateTo, setFilterDateTo] = React.useState('');

  // Reset pagination on filter changes
  const prevFiltersRef = React.useRef({ filterStatus, filterProjectId, filterDateFrom, filterDateTo });
  React.useEffect(() => {
    const prev = prevFiltersRef.current;
    if (prev.filterStatus !== filterStatus || prev.filterProjectId !== filterProjectId ||
        prev.filterDateFrom !== filterDateFrom || prev.filterDateTo !== filterDateTo) {
      pagination.resetToFirstPage();
      prevFiltersRef.current = { filterStatus, filterProjectId, filterDateFrom, filterDateTo };
    }
  }, [filterStatus, filterProjectId, filterDateFrom, filterDateTo, pagination]);

  const [form, setForm] = React.useState({
    externalProjectId: '',
    externalTenantId: '',
    accountId: '',
    requestedAmount: '',
    purpose: ''
  });
  const [accounts, setAccounts] = React.useState<AccountInfo[]>([]);

  const load = React.useCallback(async () => {
    setError(null);
    setLoading(true);
    try {
      const data = await getSanctions();
      setSanctions(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load sanctions');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => { load(); }, [load]);

  React.useEffect(() => {
    const fetchAccounts = async () => {
      try {
        const token = localStorage.getItem('token');
        const res = await fetch('/accounts', { headers: { 'Authorization': token ? `Bearer ${token}` : '' } });
        if (res.ok) {
          const data = await res.json();
          setAccounts(data);
        }
      } catch { /* ignore */ }
    };
    fetchAccounts();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const userId = localStorage.getItem('userId') || '';
      if (!userId) {
        setError('User not authenticated. Please log in again.');
        setSubmitting(false);
        return;
      }
      await createSanction({
        externalProjectId: form.externalProjectId,
        externalTenantId: form.externalTenantId,
        userId: userId,
        accountId: form.accountId,
        requestedAmount: parseFloat(form.requestedAmount),
        purpose: form.purpose,
        idempotencyKey: crypto.randomUUID()
      });
      setForm({ externalProjectId: '', externalTenantId: '', accountId: '', requestedAmount: '', purpose: '' });
      setShowForm(false);
      await load();
    } catch (err: any) {
         setError(err.message || 'Failed to create sanction request');
        } finally {
          setSubmitting(false);
        }
      };

      const allFiltered = React.useMemo(() => {
        return sanctions.filter(s => {
          if (filterStatus && s.status !== filterStatus) return false;
          if (filterProjectId && !s.externalProjectId.toLowerCase().includes(filterProjectId.toLowerCase())) return false;
          if (filterDateFrom) {
            const from = new Date(filterDateFrom);
            if (new Date(s.createdAt) < from) return false;
          }
          if (filterDateTo) {
            const to = new Date(filterDateTo);
            to.setHours(23, 59, 59, 999);
            if (new Date(s.createdAt) > to) return false;
          }
          return true;
        });
      }, [sanctions, filterStatus, filterProjectId, filterDateFrom, filterDateTo]);

      // Update pagination total
      React.useEffect(() => {
        pagination.setTotalCount(allFiltered.length);
      }, [allFiltered.length, pagination]);

      // Paginated filtered results
      const filtered = React.useMemo(() => {
        const start = (pagination.page - 1) * pagination.pageSize;
        return allFiltered.slice(start, start + pagination.pageSize);
      }, [allFiltered, pagination.page, pagination.pageSize]);

      const fmtAmount = (amount: number, currency: string) => {
        try {
          return new Intl.NumberFormat('en-NZ', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount) + ' ' + currency;
        } catch {
          return `${amount.toFixed(2)} ${currency}`;
        }
      };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h2>FTK Sanctions</h2>
        <button className="btn btn-primary" onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Cancel' : '+ New Request'}
        </button>
      </div>

      {error && <div className="alert alert-error" role="alert">{error}</div>}

      {showForm && (
        <form onSubmit={handleSubmit} className="card" style={{ marginBottom: 24, padding: 20 }}>
          <h3 style={{ marginTop: 0 }}>New Sanction Request</h3>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <label>
              Project ID
              <input type="text" value={form.externalProjectId} onChange={e => setForm(f => ({ ...f, externalProjectId: e.target.value }))} required />
            </label>
            <label>
              Tenant ID
              <input type="text" value={form.externalTenantId} onChange={e => setForm(f => ({ ...f, externalTenantId: e.target.value }))} required />
            </label>
            <label>
              Account
              <select value={form.accountId} onChange={e => setForm(f => ({ ...f, accountId: e.target.value }))} required>
                <option value="">Select account…</option>
                {accounts.map(a => (
                  <option key={a.id} value={a.id}>
                    {a.accountType || 'Account'} — {a.accountNumber || a.id.substring(0, 8)} ({a.currency || 'NZD'})
                  </option>
                ))}
              </select>
            </label>
            <label>
              Amount
              <input type="number" step="0.01" min="0.01" value={form.requestedAmount} onChange={e => setForm(f => ({ ...f, requestedAmount: e.target.value }))} required />
            </label>
            <label style={{ gridColumn: '1 / -1' }}>
              Purpose
              <input type="text" value={form.purpose} onChange={e => setForm(f => ({ ...f, purpose: e.target.value }))} required />
            </label>
          </div>
          <button type="submit" className="btn btn-primary" style={{ marginTop: 12 }} disabled={submitting}>
            {submitting ? 'Submitting…' : 'Submit'}
          </button>
        </form>
      )}

      <div className="card" style={{ padding: 16, marginBottom: 16, display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
        <label style={{ minWidth: 140 }}>
          Status
          <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
            <option value="">All</option>
            {allStatuses.map(st => <option key={st} value={st}>{st}</option>)}
          </select>
        </label>
        <label style={{ minWidth: 160 }}>
             Project ID
              <input type="text" placeholder="Search…" value={filterProjectId} onChange={e => setFilterProjectId(e.target.value)} />
            </label>
            <label style={{ minWidth: 140 }}>
              From
              <input type="date" value={filterDateFrom} onChange={e => setFilterDateFrom(e.target.value)} />
            </label>
            <label style={{ minWidth: 140 }}>
              To
              <input type="date" value={filterDateTo} onChange={e => setFilterDateTo(e.target.value)} />
            </label>
            {(filterStatus || filterProjectId || filterDateFrom || filterDateTo) && (
              <button className="btn btn-secondary" style={{ alignSelf: 'flex-end' }} onClick={() => { setFilterStatus(''); setFilterProjectId(''); setFilterDateFrom(''); setFilterDateTo(''); }}>
                Clear
              </button>
            )}
          </div>

          {loading ? (
            <TableSkeleton rows={pagination.pageSize} columns={8} />
          ) : allFiltered.length === 0 ? (
            <p>No sanction requests found.</p>
          ) : (
            <>
            <div className="card" style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
              <tr style={{ borderBottom: '2px solid var(--border, #ddd)', textAlign: 'left' }}>
                <th style={{ padding: '8px 12px' }}>Project</th>
                <th style={{ padding: '8px 12px' }}>User</th>
                <th style={{ padding: '8px 12px' }}>Requested</th>
                <th style={{ padding: '8px 12px' }}>Approved</th>
                <th style={{ padding: '8px 12px' }}>Currency</th>
                <th style={{ padding: '8px 12px' }}>Risk</th>
                <th style={{ padding: '8px 12px' }}>Status</th>
                <th style={{ padding: '8px 12px' }}>Created</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(s => (
                <tr
                  key={s.id}
                  onClick={() => navigate(`/sanctions/${s.id}`)}
                  style={{ borderBottom: '1px solid var(--border, #eee)', cursor: 'pointer' }}
                >
                  <td style={{ padding: '8px 12px' }}>{s.externalProjectId}</td>
                  <td style={{ padding: '8px 12px', fontSize: '0.85em' }}>{s.userId.substring(0, 8)}…</td>
                  <td style={{ padding: '8px 12px' }}>{fmtAmount(s.requestedAmount, s.currency)}</td>
                  <td style={{ padding: '8px 12px' }}>{s.approvedAmount != null ? fmtAmount(s.approvedAmount, s.currency) : '—'}</td>
                  <td style={{ padding: '8px 12px' }}>{s.currency}</td>
                  <td style={{ padding: '8px 12px' }}>{s.riskScore}</td>
                  <td style={{ padding: '8px 12px' }}>
                    <span style={{
                      padding: '2px 10px', borderRadius: 12, fontSize: '0.85em', fontWeight: 600,
                      background: statusColors[s.status] || '#4b5563', color: '#fff'
                    }}>
                      {s.status}
                    </span>
                  </td>
                  <td style={{ padding: '8px 12px' }}>{new Date(s.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pagination pagination={pagination} />
        </>
      )}
    </div>
  );
};

export default SanctionsList;
