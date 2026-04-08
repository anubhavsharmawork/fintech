import React, { useEffect, useState, useMemo } from 'react';
import { Check } from 'lucide-react';
import { getKycStatus, getSarReports, SarReport } from '../services/complianceService';
import { isCorporateUser, getOrganisationId } from '../auth';
import { usePagination } from '../hooks/usePagination';
import Pagination from '../components/Pagination';
import TableSkeleton from '../components/TableSkeleton';

type Tab = 'kyc' | 'sar';

// Badge pill component for status values
const StatusBadge: React.FC<{ status: string }> = ({ status }) => {
  let bgColor = 'rgba(107, 114, 128, 0.1)';
  let textColor = '#4b5563';

  const lowerStatus = status.toLowerCase();
  if (lowerStatus === 'verified' || lowerStatus === 'resolved' || lowerStatus === 'completed') {
    bgColor = 'rgba(16, 185, 129, 0.1)';
    textColor = '#065f46';
  } else if (lowerStatus === 'pending' || lowerStatus === 'underreview' || lowerStatus === 'under review') {
    bgColor = 'rgba(245, 158, 11, 0.1)';
    textColor = '#92400e';
  } else if (lowerStatus === 'rejected' || lowerStatus === 'flagged' || lowerStatus === 'open') {
    bgColor = 'rgba(239, 68, 68, 0.1)';
    textColor = '#991b1b';
  }

  return (
    <span style={{
      display: 'inline-block',
      padding: '2px 10px',
      borderRadius: 999,
      fontSize: '0.75rem',
      fontWeight: 600,
      background: bgColor,
      color: textColor,
    }}>
      {status}
    </span>
  );
};

// Risk level badge with matching colors
const RiskBadge: React.FC<{ level: string }> = ({ level }) => {
  let bgColor = 'rgba(245, 158, 11, 0.1)';
  let textColor = '#92400e';

  const lowerLevel = level.toLowerCase();
  if (lowerLevel === 'low') {
    bgColor = 'rgba(16, 185, 129, 0.1)';
    textColor = '#065f46';
  } else if (lowerLevel === 'high') {
    bgColor = 'rgba(239, 68, 68, 0.1)';
    textColor = '#991b1b';
  }

  return (
    <span style={{
      display: 'inline-block',
      padding: '2px 10px',
      borderRadius: 999,
      fontSize: '0.75rem',
      fontWeight: 600,
      background: bgColor,
      color: textColor,
    }}>
      {level}
    </span>
  );
};

const kycDescriptions: Record<string, string> = {
  Pending: 'Your identity verification is pending. Please allow up to 48 hours for review.',
  Verified: 'Your identity has been successfully verified. You have full access to all features.',
  Rejected: 'Your identity verification was rejected. Please contact support for assistance.',
  UnderReview: 'Your account is under additional review by our compliance team.',
};

const kycBadgeColor: Record<string, string> = {
  Pending: '#92400e',
  Verified: '#065f46',
  Rejected: '#991b1b',
  UnderReview: '#9a3412',
};

const riskBadgeColor: Record<string, string> = {
  High: '#991b1b',
  Medium: '#92400e',
  Low: '#065f46',
};

const statusBadgeColor: Record<string, string> = {
  Open: '#991b1b',
  Resolved: '#065f46',
};

function Compliance() {
  const [tab, setTab] = useState<Tab>('kyc');
  const [kycStatus, setKycStatus] = useState<string | null>(null);
  const [kycLoading, setKycLoading] = useState(true);
  const [kycError, setKycError] = useState<string | null>(null);
  const [sarReports, setSarReports] = useState<SarReport[]>([]);
  const [sarLoading, setSarLoading] = useState(true);
  const [sarError, setSarError] = useState<string | null>(null);
  const pagination = usePagination({ defaultPageSize: 25, syncToUrl: true });

  // Update pagination total when SAR reports change
  useEffect(() => {
    pagination.setTotalCount(sarReports.length);
  }, [sarReports.length, pagination]);

  // Paginated SAR reports
  const paginatedSarReports = useMemo(() => {
    const start = (pagination.page - 1) * pagination.pageSize;
    return sarReports.slice(start, start + pagination.pageSize);
  }, [sarReports, pagination.page, pagination.pageSize]);

  useEffect(() => {
    getKycStatus()
      .then((data) => setKycStatus(data.status))
      .catch(() => setKycError('Unable to load KYC status'))
      .finally(() => setKycLoading(false));

    getSarReports()
      .then((data) => setSarReports(data))
      .catch(() => setSarError('Unable to load SAR reports'))
      .finally(() => setSarLoading(false));
  }, []);

  // Compute KPI metrics from loaded data
  const kpiMetrics = useMemo(() => {
    const today = new Date().toISOString().split('T')[0];
    const flaggedToday = sarReports.filter(r => r.flaggedAt.startsWith(today)).length;
    const awaitingReview = sarReports.filter(r => r.status === 'Open').length;
    const sanctionsHitsToday = sarReports.filter(r => 
      r.flaggedAt.startsWith(today) && r.riskLevel === 'High'
    ).length;
    return {
      totalRecords: sarReports.length,
      flaggedToday,
      awaitingReview,
      sanctionsHitsToday,
    };
  }, [sarReports]);

  return (
    <div className="compliance-page">
      <h2>Compliance</h2>

      {/* KPI Summary Row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 12, marginBottom: 20 }}>
        <div className="card" style={{ padding: 16, textAlign: 'center' }}>
          <div style={{ fontSize: 11, color: 'var(--muted, #6b7280)', marginBottom: 4 }}>Total Records</div>
          <div style={{ fontSize: 24, fontWeight: 700, color: 'var(--text, #111827)' }}>
            {sarLoading ? '—' : kpiMetrics.totalRecords}
          </div>
        </div>
        <div className="card" style={{ padding: 16, textAlign: 'center' }}>
          <div style={{ fontSize: 11, color: 'var(--muted, #6b7280)', marginBottom: 4 }}>Flagged Today</div>
          <div style={{ fontSize: 24, fontWeight: 700, color: '#b91c1c' }}>
            {sarLoading ? '—' : kpiMetrics.flaggedToday}
          </div>
        </div>
        <div className="card" style={{ padding: 16, textAlign: 'center' }}>
          <div style={{ fontSize: 11, color: 'var(--muted, #6b7280)', marginBottom: 4 }}>Awaiting Review</div>
          <div style={{ fontSize: 24, fontWeight: 700, color: '#92400e' }}>
            {sarLoading ? '—' : kpiMetrics.awaitingReview}
          </div>
        </div>
        <div className="card" style={{ padding: 16, textAlign: 'center' }}>
          <div style={{ fontSize: 11, color: 'var(--muted, #6b7280)', marginBottom: 4 }}>High Risk Today</div>
          <div style={{ fontSize: 24, fontWeight: 700, color: '#b91c1c' }}>
            {sarLoading ? '—' : kpiMetrics.sanctionsHitsToday}
          </div>
        </div>
      </div>

      {isCorporateUser() && (
        <div className="card" role="region" aria-label="Organisation Compliance" style={{ marginBottom: 16, borderLeft: '4px solid #2563eb' }}>
          <p className="small" style={{ margin: 0 }}>
            <strong>Corporate Compliance</strong> &mdash; Reports shown are filtered to your organisation context.
            Organisation: <code>{getOrganisationId()}</code>
          </p>
        </div>
      )}
      <div className="compliance-tabs" role="tablist">
        <button
          role="tab"
          aria-selected={tab === 'kyc'}
          className={`compliance-tab${tab === 'kyc' ? ' compliance-tab-active' : ''}`}
          onClick={() => setTab('kyc')}
        >
          My KYC Status
        </button>
        <button
          role="tab"
          aria-selected={tab === 'sar'}
          className={`compliance-tab${tab === 'sar' ? ' compliance-tab-active' : ''}`}
          onClick={() => setTab('sar')}
        >
          Suspicious Activity Reports
        </button>
      </div>

      {tab === 'kyc' && (
        <div className="compliance-panel" role="tabpanel">
          {kycLoading && <p>Loading KYC status…</p>}
          {kycError && <p className="alert-error" style={{ padding: '0.75rem 1rem', borderRadius: 4 }}>{kycError}</p>}
          {!kycLoading && !kycError && kycStatus && (
            <>
              {kycStatus === 'Verified' && (
                <div className="compliance-verified-banner">
                  <span aria-hidden="true"><Check size={18} color="currentColor" /></span> KYC Verified
                </div>
              )}
              <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 12 }}>
                <span className="compliance-status-label">Status:</span>
                <StatusBadge status={kycStatus} />
              </div>
              <p className="compliance-desc">{kycDescriptions[kycStatus] || ''}</p>
            </>
          )}
        </div>
      )}

      {tab === 'sar' && (
         <div className="compliance-panel" role="tabpanel">
           {sarLoading && <TableSkeleton rows={pagination.pageSize} columns={6} />}
           {sarError && <p className="alert-error" style={{ padding: '0.75rem 1rem', borderRadius: 4 }}>{sarError}</p>}
           {!sarLoading && !sarError && sarReports.length === 0 && (
             <p className="compliance-clear" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>No suspicious activity detected on your account <Check size={16} color="currentColor" /></p>
           )}
           {!sarLoading && !sarError && sarReports.length > 0 && (
             <>
               <div style={{ overflowX: 'auto' }}>
                 <table className="compliance-table">
                   <thead>
                     <tr>
                       <th>Date</th>
                       <th>Amount</th>
                       <th>Currency</th>
                       <th>Risk Level</th>
                       <th>Reason</th>
                       <th>Status</th>
                     </tr>
                   </thead>
                   <tbody>
                     {paginatedSarReports.map((r) => (
                      <tr key={r.id}>
                        <td>{new Date(r.flaggedAt).toLocaleDateString()}</td>
                        <td>{r.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                        <td>{r.currency}</td>
                        <td>
                          <RiskBadge level={r.riskLevel} />
                        </td>
                        <td>{r.reason}</td>
                        <td>
                          <StatusBadge status={r.status} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination pagination={pagination} />
              <p className="compliance-disclaimer">
                Flagged transactions are automatically reviewed by our compliance team in accordance with AML regulations.
              </p>
            </>
          )}
        </div>
      )}
    </div>
  );
}

export default Compliance;
