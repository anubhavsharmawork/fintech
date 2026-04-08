import * as React from 'react';
import { getOrganisationRole } from '../auth';
import {
  getPendingApprovals,
  decideBatch,
  getPaymentBatches,
  PaymentBatch
} from '../services/corporate';
import { usePagination } from '../hooks/usePagination';
import Pagination from '../components/Pagination';
import TableSkeleton from '../components/TableSkeleton';

const ApprovalsPage: React.FC = () => {
  const [pending, setPending] = React.useState<PaymentBatch[]>([]);
  const [allBatches, setAllBatches] = React.useState<PaymentBatch[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [deciding, setDeciding] = React.useState<string | null>(null);
  const [tab, setTab] = React.useState<'pending' | 'history'>('pending');
  const pendingPagination = usePagination({ defaultPageSize: 10, syncToUrl: true });
  const historyPagination = usePagination({ defaultPageSize: 25, syncToUrl: false });

  const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' });
  const role = getOrganisationRole();

  const decidedBatches = React.useMemo(() => 
    allBatches.filter(b => b.status === 'Approved' || b.status === 'Rejected' || b.status === 'Executed'),
    [allBatches]
  );

  // Update pagination totals
  React.useEffect(() => {
    pendingPagination.setTotalCount(pending.length);
  }, [pending.length, pendingPagination]);

  React.useEffect(() => {
    historyPagination.setTotalCount(decidedBatches.length);
  }, [decidedBatches.length, historyPagination]);

  // Paginated results
  const paginatedPending = React.useMemo(() => {
    const start = (pendingPagination.page - 1) * pendingPagination.pageSize;
    return pending.slice(start, start + pendingPagination.pageSize);
  }, [pending, pendingPagination.page, pendingPagination.pageSize]);

  const paginatedHistory = React.useMemo(() => {
    const start = (historyPagination.page - 1) * historyPagination.pageSize;
    return decidedBatches.slice(start, start + historyPagination.pageSize);
  }, [decidedBatches, historyPagination.page, historyPagination.pageSize]);

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [pendingData, batchData] = await Promise.all([
        getPendingApprovals(),
        getPaymentBatches()
      ]);
      setPending(pendingData);
      setAllBatches(batchData);
    } catch (e: any) {
      setError(e.message || 'Failed to load approvals');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => { load(); }, [load]);

  const handleDecision = async (batchId: string, decision: string) => {
    setDeciding(batchId);
    setError(null);
    try {
      await decideBatch(batchId, decision);
      await load();
    } catch (e: any) {
      setError(e.message || `Failed to ${decision.toLowerCase()} batch`);
    } finally {
      setDeciding(null);
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
      <h2>Approvals</h2>

      {role !== 'Admin' && role !== 'Approver' && (
        <div className="alert alert-info" style={{ marginBottom: 16 }}>
          You need an Approver or Admin role to approve or reject payment batches.
        </div>
      )}

      {error && <div className="alert alert-error" style={{ marginBottom: 16 }}>{error}</div>}

      <div className="compliance-tabs" role="tablist" style={{ marginBottom: 16 }}>
        <button
          role="tab"
          aria-selected={tab === 'pending'}
          className={`compliance-tab${tab === 'pending' ? ' compliance-tab-active' : ''}`}
          onClick={() => setTab('pending')}
        >
          Pending ({pending.length})
        </button>
        <button
          role="tab"
          aria-selected={tab === 'history'}
          className={`compliance-tab${tab === 'history' ? ' compliance-tab-active' : ''}`}
          onClick={() => setTab('history')}
        >
          History
        </button>
      </div>

      {loading && <TableSkeleton rows={10} columns={4} />}

       {!loading && tab === 'pending' && (
         <div>
           {pending.length === 0 && (
             <div className="card" style={{ textAlign: 'center', padding: 32 }}>
               <p className="small">No pending approvals. All clear!</p>
             </div>
           )}
               {paginatedPending.map(b => (
                 <div key={b.id} className="card" style={{ marginBottom: 12 }}>
                   <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                     <div>
                       <h4 style={{ margin: '0 0 4px 0' }}>{b.currency} Payment Batch</h4>
                       <p className="small" style={{ margin: '0 0 4px 0' }}>
                         {b.itemCount} payment{b.itemCount !== 1 ? 's' : ''} &middot; Submitted {new Date(b.createdAt).toLocaleDateString()}
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
                       <p style={{ fontSize: 20, fontWeight: 700, margin: '0 0 8px 0' }}>{nzd.format(b.totalAmount)}</p>
                       {(role === 'Admin' || role === 'Approver') && (
                         <div style={{ display: 'flex', gap: 8 }}>
                           <button
                             className="btn btn-primary"
                             style={{ fontSize: 13, padding: '6px 14px' }}
                             onClick={() => handleDecision(b.id, 'Approved')}
                             disabled={deciding === b.id}
                           >
                             {deciding === b.id ? '...' : 'Approve'}
                           </button>
                           <button
                             className="btn btn-secondary"
                             style={{ fontSize: 13, padding: '6px 14px' }}
                             onClick={() => handleDecision(b.id, 'Rejected')}
                             disabled={deciding === b.id}
                           >
                             Reject
                           </button>
                         </div>
                       )}
                     </div>
                   </div>
                 </div>
               ))}
               {pending.length > 0 && <Pagination pagination={pendingPagination} />}
             </div>
           )}

      {!loading && tab === 'history' && (
        <div>
          {decidedBatches.length === 0 && (
            <div className="card" style={{ textAlign: 'center', padding: 32 }}>
              <p className="small">No approval history yet.</p>
            </div>
          )}
          {paginatedHistory.map(b => (
            <div key={b.id} className="card" style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <h4 style={{ margin: '0 0 4px 0' }}>{b.currency} Payment Batch</h4>
                  <p className="small" style={{ margin: 0 }}>
                    {b.itemCount} payment{b.itemCount !== 1 ? 's' : ''} &middot; {nzd.format(b.totalAmount)}
                  </p>
                </div>
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
            </div>
          ))}
          {decidedBatches.length > 0 && <Pagination pagination={historyPagination} />}
        </div>
      )}
    </div>
  );
};

export default ApprovalsPage;
