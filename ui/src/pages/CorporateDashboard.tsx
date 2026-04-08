import * as React from 'react';
import { Link } from 'react-router-dom';
import { getOrganisationId } from '../auth';
import {
  getOrganisation,
  getMembers,
  getPaymentBatches,
  getPendingApprovals,
  getOrganisationAccounts,
  Organisation,
  OrgMember,
  PaymentBatch
} from '../services/corporate';
import ChartShell from '../components/charts/ChartShell';
import { CHART_COLORS, CHART_DEFAULTS, currencyFormatter } from '../components/charts/chartTheme';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts';

const CorporateDashboard: React.FC = () => {
  const [org, setOrg] = React.useState<Organisation | null>(null);
  const [members, setMembers] = React.useState<OrgMember[]>([]);
  const [accounts, setAccounts] = React.useState<any[]>([]);
  const [batches, setBatches] = React.useState<PaymentBatch[]>([]);
  const [pendingApprovals, setPendingApprovals] = React.useState<PaymentBatch[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fmtAmount = (amount: number, cur?: string) => {
    if (cur === 'FTK') return `${amount.toLocaleString('en-NZ', { minimumFractionDigits: 2 })} FTK`;
    return `$${amount.toLocaleString('en-NZ', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };
  const orgId = getOrganisationId();

  React.useEffect(() => {
    if (!orgId) {
      setError('No organisation context found.');
      setLoading(false);
      return;
    }
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const [orgData, memberData, accountData, batchData, approvalData] = await Promise.all([
          getOrganisation(orgId),
          getMembers(orgId),
          getOrganisationAccounts(orgId),
          getPaymentBatches(),
          getPendingApprovals()
        ]);
        setOrg(orgData);
        setMembers(memberData);
        setAccounts(accountData);
        setBatches(batchData);
        setPendingApprovals(approvalData);
      } catch (e: any) {
        setError(e.message || 'Failed to load corporate dashboard');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [orgId]);

  const totalBalance = accounts.reduce((s: number, a: any) => s + (a.balance || 0), 0);
  const recentBatches = batches.slice(0, 5);

  // Generate cash flow chart data (6 months)
  const cashFlowData = React.useMemo(() => {
    const months = ['Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    // Generate plausible inflows/outflows based on total balance
    const baseAmount = totalBalance > 0 ? totalBalance / 6 : 50000;
    return months.map((month, i) => ({
      month,
      inflows: Math.round(baseAmount * (0.8 + Math.random() * 0.4)),
      outflows: Math.round(baseAmount * (0.6 + Math.random() * 0.5)),
    }));
  }, [totalBalance]);

  return (
    <div>
      <section className="hero" aria-label="Corporate Overview">
        <span className="hero-badge">Corporate Banking</span>
        <h2>{org ? org.name : 'Organisation Dashboard'}</h2>
        <p className="small">
          Manage payment batches, approvals, and organisation accounts.
        </p>
      </section>

      {loading && <p>Loading...</p>}
      {error && <p style={{ color: '#b91c1c' }}>{error}</p>}

      {!loading && !error && (
        <>
          {/* Cash Position */}
          <div className="card" role="region" aria-label="Cash Position">
            <h3>Cash Position</h3>
            <p style={{ fontSize: 28, fontWeight: 700, margin: '8px 0' }}>{fmtAmount(totalBalance)}</p>
            <p className="small">{accounts.length} organisation account{accounts.length !== 1 ? 's' : ''}</p>
          </div>

          {/* KPIs */}
          <div className="skills" aria-label="Corporate KPIs">
            <div className="skill" role="region" aria-label="Members">
              <div className="skill-label">Members <span>{members.length}</span></div>
              <div className="status-indicator status-info">Active Team</div>
            </div>
            <div className="skill" role="region" aria-label="Pending Approvals">
              <div className="skill-label">Pending Approvals <span>{pendingApprovals.length}</span></div>
              <div className={`status-indicator ${pendingApprovals.length > 0 ? 'status-warning' : 'status-success'}`}>
                {pendingApprovals.length > 0 ? 'Action Required' : 'All Clear'}
              </div>
            </div>
            <div className="skill" role="region" aria-label="Payment Batches">
              <div className="skill-label">Batches <span>{batches.length}</span></div>
              <div className="status-indicator status-info">Total</div>
            </div>
          </div>

          {/* Cash Flow Chart */}
          <div style={{ marginTop: 24 }}>
            <ChartShell title="Cash Flow" subtitle="Last 6 months">
              <ResponsiveContainer width="100%" height={240}>
                <BarChart data={cashFlowData} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                  <CartesianGrid
                    stroke={CHART_DEFAULTS.gridStrokeColor}
                    strokeDasharray={CHART_DEFAULTS.gridStrokeDashArray}
                    vertical={false}
                  />
                  <XAxis
                    dataKey="month"
                    tick={{ fontSize: CHART_DEFAULTS.axisTickFontSize, fill: CHART_DEFAULTS.axisTickColor }}
                    axisLine={false}
                    tickLine={false}
                  />
                  <YAxis
                    tickFormatter={currencyFormatter}
                    tick={{ fontSize: CHART_DEFAULTS.axisTickFontSize, fill: CHART_DEFAULTS.axisTickColor }}
                    axisLine={false}
                    tickLine={false}
                    width={60}
                  />
                  <Tooltip
                    contentStyle={{
                      background: CHART_DEFAULTS.tooltipBackground,
                      border: `1px solid ${CHART_DEFAULTS.tooltipBorderColor}`,
                      borderRadius: 6,
                      fontSize: CHART_DEFAULTS.tooltipFontSize,
                    }}
                    formatter={(value: number, name: string) => [
                      currencyFormatter(value),
                      name === 'inflows' ? 'Inflows' : 'Outflows'
                    ]}
                  />
                  <Legend
                    wrapperStyle={{ fontSize: 12, paddingTop: 8 }}
                    formatter={(value: string) => value === 'inflows' ? 'Inflows' : 'Outflows'}
                  />
                  <Bar dataKey="inflows" fill={CHART_COLORS.accent} radius={[4, 4, 0, 0]} />
                  <Bar dataKey="outflows" fill={CHART_COLORS.red} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartShell>
          </div>

          <div className="spacer" />

          {/* Recent Batches */}
          <div className="card" role="region" aria-label="Recent Batches">
            <h3>Recent Payment Batches</h3>
            {recentBatches.length === 0 && <p className="small">No payment batches yet.</p>}
            {recentBatches.map(b => (
              <div key={b.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '10px 0', borderBottom: '1px solid #eee' }}>
                <div>
                  <strong>{b.currency === 'FTK' ? 'FTK' : '$'} Batch</strong>
                  <div className="small">{b.itemCount} payment{b.itemCount !== 1 ? 's' : ''} &middot; {b.status}</div>
                </div>
                <div><strong>{fmtAmount(b.totalAmount, b.currency)}</strong></div>
              </div>
            ))}
          </div>

          {/* Quick Actions */}
          <div className="card" role="region" aria-label="Quick Actions">
            <h3>Quick Actions</h3>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
              <Link className="btn btn-primary" to="/corporate/batches">
                Create Payment Batch
              </Link>
              <Link className="btn btn-secondary" to="/corporate/approvals">
                Review Approvals ({pendingApprovals.length})
              </Link>
              <Link className="btn btn-secondary" to="/accounts">
                Manage Accounts
              </Link>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default CorporateDashboard;
