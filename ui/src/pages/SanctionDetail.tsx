import * as React from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  getSanctionById,
  getSanctionAudit,
  disburseSanction,
  rejectSanction,
  cancelSanction,
  SanctionRequestDto,
  SanctionAuditLogDto
} from '../services/sanctions';

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

const SanctionDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [sanction, setSanction] = React.useState<SanctionRequestDto | null>(null);
  const [audit, setAudit] = React.useState<SanctionAuditLogDto[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [actionLoading, setActionLoading] = React.useState(false);
  const [reason, setReason] = React.useState('');

  const load = React.useCallback(async () => {
    if (!id) return;
    setError(null);
    setLoading(true);
    try {
      const [s, a] = await Promise.all([getSanctionById(id), getSanctionAudit(id)]);
      setSanction(s);
      setAudit(a);
    } catch (err: any) {
      setError(err.message || 'Failed to load sanction details');
    } finally {
      setLoading(false);
    }
  }, [id]);

  React.useEffect(() => { load(); }, [load]);

  const handleDisburse = async () => {
    if (!id) return;
    setActionLoading(true);
    setError(null);
    try {
      await disburseSanction(id);
      await load();
    } catch (err: any) {
      setError(err.message);
    } finally {
      setActionLoading(false);
    }
  };

  const handleReject = async () => {
    if (!id || !reason.trim()) return;
    setActionLoading(true);
    setError(null);
    try {
      await rejectSanction(id, reason.trim());
      setReason('');
      await load();
    } catch (err: any) {
      setError(err.message);
    } finally {
      setActionLoading(false);
    }
  };

  const handleCancel = async () => {
    if (!id || !reason.trim()) return;
    setActionLoading(true);
    setError(null);
    try {
      await cancelSanction(id, reason.trim());
      setReason('');
      await load();
    } catch (err: any) {
      setError(err.message);
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) return <p>Loading…</p>;
  if (!sanction) return <p>Sanction request not found.</p>;

  const canDisburse = sanction.status === 'Approved';
  const canReject = ['Submitted', 'Screening', 'Underwriting'].includes(sanction.status);
  const canCancel = ['Draft', 'Submitted'].includes(sanction.status);

  return (
    <div>
      <button className="btn btn-secondary" onClick={() => navigate('/sanctions')} style={{ marginBottom: 16 }}>
        ← Back to Sanctions
      </button>

      {error && <div className="alert alert-error" role="alert">{error}</div>}

      <div className="card" style={{ padding: 24, marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h2 style={{ margin: 0 }}>Sanction Request</h2>
          <span style={{
            padding: '4px 16px', borderRadius: 12, fontWeight: 600,
            background: statusColors[sanction.status] || '#4b5563', color: '#fff'
          }}>
            {sanction.status}
          </span>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, fontSize: '0.95em' }}>
          <div><strong>Project ID:</strong> {sanction.externalProjectId}</div>
          <div><strong>Tenant ID:</strong> {sanction.externalTenantId}</div>
          <div><strong>User ID:</strong> {sanction.userId}</div>
          <div><strong>Account ID:</strong> {sanction.accountId}</div>
          <div><strong>Requested Amount:</strong> {sanction.requestedAmount} {sanction.currency}</div>
          <div><strong>Approved Amount:</strong> {sanction.approvedAmount ?? '—'}</div>
          <div><strong>Risk Score:</strong> {sanction.riskScore}</div>
          <div><strong>KYC:</strong> {sanction.kycStatus}</div>
          <div><strong>AML:</strong> {sanction.amlStatus}</div>
          <div><strong>Purpose:</strong> {sanction.purpose}</div>
          <div><strong>FTK Tx Ref:</strong> {sanction.ftkTransactionRef || '—'}</div>
          <div><strong>Decision Reason:</strong> {sanction.decisionReason || '—'}</div>
          <div><strong>Created:</strong> {new Date(sanction.createdAt).toLocaleString()}</div>
          <div><strong>Updated:</strong> {new Date(sanction.updatedAt).toLocaleString()}</div>
          <div><strong>Created By:</strong> {sanction.createdBy}</div>
          <div><strong>Idempotency Key:</strong> <code>{sanction.idempotencyKey}</code></div>
        </div>

        {(canDisburse || canReject || canCancel) && (
          <div style={{ marginTop: 20, display: 'flex', gap: 12, alignItems: 'flex-end', flexWrap: 'wrap' }}>
            {(canReject || canCancel) && (
              <label style={{ flex: 1, minWidth: 200 }}>
                Reason
                <input type="text" value={reason} onChange={e => setReason(e.target.value)} placeholder="Required for reject/cancel" />
              </label>
            )}
            {canDisburse && (
              <button className="btn btn-primary" onClick={handleDisburse} disabled={actionLoading}>
                {actionLoading ? 'Processing…' : 'Disburse'}
              </button>
            )}
            {canReject && (
              <button className="btn btn-secondary" onClick={handleReject} disabled={actionLoading || !reason.trim()} style={{ background: '#b91c1c', color: '#fff' }}>
                Reject
              </button>
            )}
            {canCancel && (
              <button className="btn btn-secondary" onClick={handleCancel} disabled={actionLoading || !reason.trim()}>
                Cancel
              </button>
            )}
          </div>
        )}
      </div>

      <div className="card" style={{ padding: 24 }}>
        <h3 style={{ marginTop: 0 }}>Audit Trail</h3>
        {audit.length === 0 ? (
          <p>No audit entries.</p>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--border, #ddd)', textAlign: 'left' }}>
                    <th style={{ padding: '6px 10px' }}>Time</th>
                    <th style={{ padding: '6px 10px' }}>From</th>
                    <th style={{ padding: '6px 10px' }}>To</th>
                    <th style={{ padding: '6px 10px' }}>By</th>
                    <th style={{ padding: '6px 10px' }}>Reason</th>
                    <th style={{ padding: '6px 10px' }}>Correlation ID</th>
                  </tr>
                </thead>
                <tbody>
                  {audit.map(a => (
                    <tr key={a.id} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                      <td style={{ padding: '6px 10px', whiteSpace: 'nowrap' }}>{new Date(a.timestamp).toLocaleString()}</td>
                      <td style={{ padding: '6px 10px' }}>
                        <span style={{ padding: '1px 8px', borderRadius: 8, fontSize: '0.85em', background: statusColors[a.fromStatus] || '#4b5563', color: '#fff' }}>{a.fromStatus}</span>
                      </td>
                      <td style={{ padding: '6px 10px' }}>
                        <span style={{ padding: '1px 8px', borderRadius: 8, fontSize: '0.85em', background: statusColors[a.toStatus] || '#4b5563', color: '#fff' }}>{a.toStatus}</span>
                      </td>
                      <td style={{ padding: '6px 10px' }}>{a.changedBy}</td>
                      <td style={{ padding: '6px 10px' }}>{a.reason}</td>
                      <td style={{ padding: '6px 10px', fontSize: '0.8em', fontFamily: 'monospace' }}>{a.correlationId}</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};

export default SanctionDetail;
