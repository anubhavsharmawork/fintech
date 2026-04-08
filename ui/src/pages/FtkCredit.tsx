import * as React from 'react';
import { Wallet } from 'lucide-react';
import { useFMode } from '../hooks/useFMode';
import { useToast } from '../components/Toast';
import ConnectWallet from '../components/ConnectWallet';
import ChartShell from '../components/charts/ChartShell';
import { CHART_COLORS, currencyFormatter } from '../components/charts/chartTheme';
import {
  RadialBarChart,
  RadialBar,
  ResponsiveContainer,
} from 'recharts';
import {
  getCreditFacility,
  requestDrawdown,
  submitRepayment,
  getRepayments,
  CreditFacilityDto,
  CreditRepaymentDto
} from '../services/credit';

const FtkCredit: React.FC = () => {
  const { enabled: fModeEnabled } = useFMode();
  const { success, error: toastError } = useToast();

  const [walletAddress, setWalletAddress] = React.useState<string | null>(null);
  const [walletChecked, setWalletChecked] = React.useState(false);
  const [facility, setFacility] = React.useState<CreditFacilityDto | null>(null);
  const [repayments, setRepayments] = React.useState<CreditRepaymentDto[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const [drawdownAmount, setDrawdownAmount] = React.useState('');
  const [repaymentAmount, setRepaymentAmount] = React.useState('');
  const [submitting, setSubmitting] = React.useState(false);

  const fmt = (n: number) => n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const formatDate = (d: string) => new Date(d).toLocaleDateString('en-NZ', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });

  React.useEffect(() => {
    const checkExisting = async () => {
      if (typeof window === 'undefined' || !(window as any).ethereum) {
        setWalletChecked(true);
        return;
      }
      try {
        const accounts = await (window as any).ethereum.request({ method: 'eth_accounts' });
        if (accounts && accounts.length > 0) {
          setWalletAddress(accounts[0]);
        }
      } catch {
        // ignore
      } finally {
        setWalletChecked(true);
      }
    };
    if (fModeEnabled) {
      checkExisting();
    } else {
      setWalletChecked(true);
    }
  }, [fModeEnabled]);

  const loadData = React.useCallback(async (wallet: string) => {
    setLoading(true);
    setError(null);
    try {
      const [fac, reps] = await Promise.all([
        getCreditFacility(wallet),
        getRepayments(wallet)
      ]);
      setFacility(fac);
      setRepayments(reps);
    } catch (err: any) {
      setError(err.message || 'Failed to load credit facility');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => {
    if (walletAddress) {
      loadData(walletAddress);
    }
  }, [walletAddress, loadData]);

  const handleDrawdown = async (e: React.FormEvent) => {
    e.preventDefault();
    const amt = parseFloat(drawdownAmount);
    if (isNaN(amt) || amt <= 0 || !walletAddress) return;
    setSubmitting(true);
    try {
      const updated = await requestDrawdown(walletAddress, amt);
      setFacility(updated);
      setDrawdownAmount('');
      success(`Drawdown of ${fmt(amt)} FTK processed successfully.`);
      loadData(walletAddress);
    } catch (err: any) {
      toastError(err.message || 'Drawdown failed');
    } finally {
      setSubmitting(false);
    }
  };

  const handleRepayment = async (e: React.FormEvent) => {
    e.preventDefault();
    const amt = parseFloat(repaymentAmount);
    if (isNaN(amt) || amt <= 0 || !walletAddress) return;
    setSubmitting(true);
    try {
      const result = await submitRepayment(walletAddress, amt);
      setFacility(result.facility);
      setRepaymentAmount('');
      success(`Repayment of ${fmt(amt)} FTK recorded successfully.`);
      loadData(walletAddress);
    } catch (err: any) {
      toastError(err.message || 'Repayment failed');
    } finally {
      setSubmitting(false);
    }
  };

  if (!fModeEnabled) {
    return (
      <div>
        <section className="hero" aria-label="FTK Credit">
          <span className="hero-badge">Fiat Mode</span>
          <h2>FTK Credit</h2>
          <p className="small">Switch to F-Mode to access DeFi credit facilities.</p>
        </section>
      </div>
    );
  }

  return (
    <div>
      <section className="hero" aria-label="FTK Credit">
        <span className="hero-badge">F-Mode (DeFi)</span>
        <h2>Drawdown &amp; Repayments</h2>
        <p className="small">Manage your FTK credit facility — draw funds and track repayments.</p>
      </section>

      <div style={{ marginTop: 24 }}>
        <ConnectWallet
          onConnected={(addr) => setWalletAddress(addr)}
          onDisconnected={() => { setWalletAddress(null); setFacility(null); setRepayments([]); }}
        />

        {!walletChecked && (
          <div className="card" style={{ textAlign: 'center', padding: 24, marginTop: 12 }}>
            <p style={{ color: '#666' }}>Checking wallet connection...</p>
          </div>
        )}

        {walletChecked && !walletAddress && (
          <div className="card" style={{ textAlign: 'center', padding: 24, marginTop: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 12 }}><Wallet size={32} color="currentColor" /></div>
            <p style={{ color: '#666' }}>Connect your MetaMask wallet to view your FTK credit facility.</p>
          </div>
        )}

        {walletAddress && loading && (
          <div className="card" style={{ textAlign: 'center', padding: 24, marginTop: 12 }}>
            <p>Loading credit facility...</p>
          </div>
        )}

        {walletAddress && error && (
          <div className="card" style={{ padding: 16, marginTop: 12 }}>
            <p style={{ color: '#b91c1c' }}>{error}</p>
            <button className="btn btn-primary" onClick={() => loadData(walletAddress)} style={{ marginTop: 8 }}>Retry</button>
          </div>
        )}

        {walletAddress && facility && !loading && (
          <>
            {/* Credit Facility Overview */}
            <div className="card" role="region" aria-label="Credit facility overview" style={{ marginTop: 12 }}>
              <h3 style={{ margin: '0 0 16px 0' }}>Credit Facility</h3>
              <div className="skills" style={{ marginTop: 0 }}>
                <div className="skill" role="region" aria-label="Credit limit">
                  <div className="skill-label">Credit Limit <span>{fmt(facility.creditLimit)} {facility.currency}</span></div>
                  <div className="status-indicator status-info">Total Facility</div>
                </div>
                <div className="skill" role="region" aria-label="Drawn amount">
                  <div className="skill-label">Drawn <span>{fmt(facility.drawnAmount)} {facility.currency}</span></div>
                  <div className={`status-indicator ${facility.drawnAmount > 0 ? 'status-warning' : 'status-success'}`}>
                    {facility.drawnAmount > 0 ? 'In Use' : 'No Drawdowns'}
                  </div>
                </div>
                <div className="skill" role="region" aria-label="Available credit">
                  <div className="skill-label">Available <span>{fmt(facility.availableCredit)} {facility.currency}</span></div>
                  <div className={`status-indicator ${facility.availableCredit > 0 ? 'status-success' : 'status-warning'}`}>
                    {facility.availableCredit > 0 ? 'Funds Available' : 'Fully Drawn'}
                  </div>
                </div>
                <div className="skill" role="region" aria-label="Outstanding balance">
                  <div className="skill-label">Outstanding <span>{fmt(facility.outstandingBalance)} {facility.currency}</span></div>
                  <div className={`status-indicator ${facility.outstandingBalance > 0 ? 'status-warning' : 'status-success'}`}>
                    {facility.outstandingBalance > 0 ? 'Balance Due' : 'Fully Repaid'}
                  </div>
                </div>
              </div>
            </div>

            {/* Credit Utilisation Chart */}
            {(() => {
              const utilisation = facility.creditLimit > 0 
                ? (facility.drawnAmount / facility.creditLimit) * 100 
                : 0;
              const utilisationColor = utilisation < 50 
                ? CHART_COLORS.accent 
                : utilisation < 80 
                  ? CHART_COLORS.amber 
                  : CHART_COLORS.red;
              const chartData = [{ name: 'Utilisation', value: utilisation, fill: utilisationColor }];

              return (
                <div style={{ marginTop: 16 }}>
                  <ChartShell title="Credit Utilisation">
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                      <div style={{ position: 'relative', width: 200, height: 200 }}>
                        <ResponsiveContainer width="100%" height="100%">
                          <RadialBarChart
                            cx="50%"
                            cy="50%"
                            innerRadius="70%"
                            outerRadius="100%"
                            barSize={20}
                            data={chartData}
                            startAngle={90}
                            endAngle={-270}
                          >
                            <RadialBar
                              background={{ fill: '#f3f4f6' }}
                              dataKey="value"
                              cornerRadius={10}
                            />
                          </RadialBarChart>
                        </ResponsiveContainer>
                        {/* Center label */}
                        <div style={{
                          position: 'absolute',
                          top: '50%',
                          left: '50%',
                          transform: 'translate(-50%, -50%)',
                          textAlign: 'center',
                          pointerEvents: 'none',
                        }}>
                          <div style={{ fontSize: 24, fontWeight: 600, color: utilisationColor }}>
                            {utilisation.toFixed(1)}%
                          </div>
                          <div style={{ fontSize: 12, color: 'var(--muted, #6b7280)' }}>Utilisation</div>
                        </div>
                      </div>
                      {/* Stats row */}
                      <div style={{ display: 'flex', gap: 32, marginTop: 16 }}>
                        <div style={{ textAlign: 'center' }}>
                          <div style={{ fontSize: 12, color: 'var(--muted, #6b7280)' }}>Used</div>
                          <div style={{ fontSize: 14, fontWeight: 500, color: 'var(--text, #111827)' }}>
                            {currencyFormatter(facility.drawnAmount)}
                          </div>
                        </div>
                        <div style={{ textAlign: 'center' }}>
                          <div style={{ fontSize: 12, color: 'var(--muted, #6b7280)' }}>Available</div>
                          <div style={{ fontSize: 14, fontWeight: 500, color: 'var(--text, #111827)' }}>
                            {currencyFormatter(facility.availableCredit)}
                          </div>
                        </div>
                      </div>
                    </div>
                  </ChartShell>
                </div>
              );
            })()}

            {/* Drawdown and Repayment Forms */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 16, marginTop: 16 }}>
              {/* Drawdown Form */}
              <div className="card" role="region" aria-label="Request drawdown">
                <h3 style={{ margin: '0 0 12px 0' }}>Request Drawdown</h3>
                <form onSubmit={handleDrawdown}>
                  <div style={{ marginBottom: 12 }}>
                    <label htmlFor="drawdown-amount" style={{ display: 'block', fontWeight: 600, marginBottom: 4, fontSize: '0.875rem' }}>
                      Amount ({facility.currency})
                    </label>
                    <input
                      id="drawdown-amount"
                      type="number"
                      step="0.01"
                      min="0.01"
                      max={facility.availableCredit}
                      value={drawdownAmount}
                      onChange={e => setDrawdownAmount(e.target.value)}
                      placeholder={`Max ${fmt(facility.availableCredit)}`}
                      required
                      disabled={submitting || facility.availableCredit <= 0}
                      style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: 8, fontSize: '0.9375rem' }}
                    />
                  </div>
                  <button
                    type="submit"
                    className="btn btn-primary"
                    disabled={submitting || facility.availableCredit <= 0 || !drawdownAmount}
                    style={{ width: '100%' }}
                  >
                    {submitting ? 'Processing...' : 'Request Drawdown'}
                  </button>
                </form>
              </div>

              {/* Repayment Form */}
              <div className="card" role="region" aria-label="Submit repayment">
                <h3 style={{ margin: '0 0 12px 0' }}>Submit Repayment</h3>
                <form onSubmit={handleRepayment}>
                  <div style={{ marginBottom: 12 }}>
                    <label htmlFor="repayment-amount" style={{ display: 'block', fontWeight: 600, marginBottom: 4, fontSize: '0.875rem' }}>
                      Amount ({facility.currency})
                    </label>
                    <input
                      id="repayment-amount"
                      type="number"
                      step="0.01"
                      min="0.01"
                      max={facility.outstandingBalance}
                      value={repaymentAmount}
                      onChange={e => setRepaymentAmount(e.target.value)}
                      placeholder={`Max ${fmt(facility.outstandingBalance)}`}
                      required
                      disabled={submitting || facility.outstandingBalance <= 0}
                      style={{ width: '100%', padding: '8px 12px', border: '1px solid var(--border)', borderRadius: 8, fontSize: '0.9375rem' }}
                    />
                  </div>
                  <button
                    type="submit"
                    className="btn btn-secondary"
                    disabled={submitting || facility.outstandingBalance <= 0 || !repaymentAmount}
                    style={{ width: '100%' }}
                  >
                    {submitting ? 'Processing...' : 'Submit Repayment'}
                  </button>
                </form>
              </div>
            </div>

            {/* Repayment History */}
            <div className="card" role="region" aria-label="Repayment history" style={{ marginTop: 16 }}>
              <h3 style={{ margin: '0 0 12px 0' }}>Repayment History</h3>
              {repayments.length === 0 ? (
                <p className="small">No repayments recorded yet.</p>
              ) : (
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                    <thead>
                      <tr style={{ borderBottom: '2px solid var(--border)', textAlign: 'left' }}>
                        <th style={{ padding: '8px 12px', fontWeight: 600 }}>Date</th>
                        <th style={{ padding: '8px 12px', fontWeight: 600 }}>Amount</th>
                        <th style={{ padding: '8px 12px', fontWeight: 600 }}>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {repayments.map(r => (
                        <tr key={r.id} style={{ borderBottom: '1px solid var(--border)' }}>
                          <td style={{ padding: '8px 12px' }}>{formatDate(r.createdAt)}</td>
                          <td style={{ padding: '8px 12px', fontWeight: 600 }}>{fmt(r.amount)} {r.currency}</td>
                          <td style={{ padding: '8px 12px' }}>
                            <span style={{
                              display: 'inline-block',
                              padding: '2px 8px',
                              borderRadius: 4,
                              fontSize: '0.75rem',
                              fontWeight: 600,
                              background: r.status === 'Completed' ? '#ecfdf5' : '#fef2f2',
                              color: r.status === 'Completed' ? '#065f46' : '#991b1b'
                            }}>
                              {r.status}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
};

export default FtkCredit;
