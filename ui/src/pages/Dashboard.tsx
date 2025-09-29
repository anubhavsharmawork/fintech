import React from 'react';

type Account = { id: string; accountNumber: string; accountType: string; balance: number; currency: string };

const Dashboard: React.FC = () => {
  const [accounts, setAccounts] = React.useState<Account[]>([]);
  const [txCount, setTxCount] = React.useState<number>(0);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' });

  React.useEffect(() => {
    const load = async () => {
      setError(null);
      setLoading(true);
      try {
        const token = localStorage.getItem('token');
        const [accRes, txRes] = await Promise.all([
          fetch('/accounts', { headers: { 'Authorization': token ? `Bearer ${token}` : '' } }),
          fetch('/transactions', { headers: { 'Authorization': token ? `Bearer ${token}` : '' } })
        ]);
        if (!accRes.ok) throw new Error('Failed to load accounts');
        if (!txRes.ok) throw new Error('Failed to load transactions');
        const accData = await accRes.json();
        const txData = await txRes.json();
        setAccounts(accData);
        setTxCount(Array.isArray(txData) ? txData.length : 0);
      } catch (e: any) {
        setError(e.message || 'Failed to load dashboard');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const total = accounts.reduce((s, a) => s + (a.balance || 0), 0);

  return (
    <div>
      <section className="hero" aria-label="Overview">
        <span className="hero-badge">Overview</span>
        <h2>Welcome back</h2>
        <p className="small">Here is a quick snapshot of your finances. Use quick actions below to get things done faster.</p>
      </section>

      {loading && <p>Loading...</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}

      {!loading && !error && (
        <>
          <div className="skills" aria-label="KPIs">
            <div className="skill" role="region" aria-label="Total Balance">
              <div className="skill-label">Total Balance <span>{nzd.format(total)}</span></div>
              <div className="skill-bar"><div className="skill-fill" style={{ ['--w' as any]: '75%' }} /></div>
            </div>
            <div className="skill" role="region" aria-label="Accounts">
              <div className="skill-label">Accounts <span>{accounts.length}</span></div>
              <div className="skill-bar"><div className="skill-fill" style={{ ['--w' as any]: '50%' }} /></div>
            </div>
            <div className="skill" role="region" aria-label="Transactions">
              <div className="skill-label">Transactions <span>{txCount}</span></div>
              <div className="skill-bar"><div className="skill-fill" style={{ ['--w' as any]: '60%' }} /></div>
            </div>
          </div>

          <div className="spacer" />

          <div className="card" role="region" aria-label="Accounts Summary">
            <h3>Accounts</h3>
            {accounts.map(a => (
              <div key={a.id} style={{ display:'flex', justifyContent:'space-between', padding:'10px 0', borderBottom:'1px solid #eee' }}>
                <div>
                  <strong>{a.accountType}</strong>
                  <div className="small">{a.accountNumber}</div>
                </div>
                <div><strong>{nzd.format(a.balance)}</strong></div>
              </div>
            ))}
            {accounts.length === 0 && <p className="small">No accounts yet.</p>}
          </div>

          <div className="card" role="region" aria-label="Quick Actions">
            <h3>Quick Actions</h3>
            <div style={{ display:'flex', gap:12, flexWrap:'wrap' }}>
              <RouterLink className="btn btn-primary" to="/transactions">Send Money</RouterLink>
              <RouterLink className="btn btn-secondary" to="/accounts">Create Account</RouterLink>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

const RouterLink = (props: any) => {
  const R = require('react-router-dom');
  const Link = R.Link as any;
  return <Link {...props} />
};

export default Dashboard;