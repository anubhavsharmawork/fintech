import React from 'react';
import { authFetch } from '../auth';
import { useFMode } from '../hooks/useFMode';
import CryptoAccountSwitcher from '../components/CryptoAccountSwitcher';
import ConnectWallet from '../components/ConnectWallet';
import BalanceCard from '../components/BalanceCard';

type Account = { id: string; accountNumber: string; accountType: string; balance: number; currency: string };

const Dashboard: React.FC = () => {
  const { enabled: fModeEnabled } = useFMode();
  const [accounts, setAccounts] = React.useState<Account[]>([]);
  const [txCount, setTxCount] = React.useState<number>(0);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [walletAddress, setWalletAddress] = React.useState<string | null>(null);
  const [walletChecked, setWalletChecked] = React.useState(false);

  const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' });

  // Auto-detect if wallet is already connected
  React.useEffect(() => {
    const checkExistingConnection = async () => {
      if (typeof window === 'undefined' || !(window as any).ethereum) {
        setWalletChecked(true);
        return;
      }

      try {
        // Check if already connected (doesn't prompt user)
        const accounts = await (window as any).ethereum.request({ 
          method: 'eth_accounts' 
        });

        if (accounts && accounts.length > 0) {
          setWalletAddress(accounts[0]);
        }
      } catch (err) {
        console.error('Failed to check wallet connection:', err);
      } finally {
        setWalletChecked(true);
      }
    };

    if (fModeEnabled) {
      checkExistingConnection();
    } else {
      setWalletChecked(true);
    }
  }, [fModeEnabled]);

  React.useEffect(() => {
    const load = async () => {
      setError(null);
      setLoading(true);
      try {
        const [accRes, txRes] = await Promise.all([
          authFetch('/accounts'),
          authFetch('/transactions')
        ]);
        if (!accRes.ok) throw new Error('Failed to load accounts');
        if (!txRes.ok) throw new Error('Failed to load transactions');
        const accData = await accRes.json();
        const txData = await txRes.json();
        setAccounts(accData);
        setTxCount(Array.isArray(txData) ? txData.length :0);
      } catch (e: any) {
        setError(e.message || 'Failed to load dashboard');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const total = accounts.reduce((s, a) => s + (a.balance ||0),0);

  return (
    <div>
      <section className="hero" aria-label="Overview">
        <span className="hero-badge">{fModeEnabled ? 'F-Mode (DeFi)' : 'Overview'}</span>
        <h2>Welcome back</h2>
        <p className="small">
          {fModeEnabled 
            ? 'Manage your digital assets and FTK tokens.' 
            : 'Here is a quick snapshot of your finances. Use quick actions below to get things done faster.'}
        </p>
      </section>

      {/* F-Mode: Show crypto UI immediately (doesn't depend on fiat API loading) */}
      {fModeEnabled && (
        <div style={{ marginTop: 24 }}>
          <ConnectWallet 
            onConnected={(addr) => setWalletAddress(addr)} 
            onDisconnected={() => setWalletAddress(null)} 
          />
          {!walletChecked && (
            <div className="card" style={{ textAlign: 'center', padding: 24, marginTop: 12 }}>
              <p style={{ color: '#666' }}>Checking wallet connection...</p>
            </div>
          )}
          {walletChecked && walletAddress && (
            <CryptoAccountSwitcher address={walletAddress} />
          )}
          {walletChecked && !walletAddress && (
            <div className="card" style={{ textAlign: 'center', padding: 24, marginTop: 12 }}>
              <div style={{ fontSize: '2rem', marginBottom: 12 }}>👛</div>
              <p style={{ color: '#666' }}>Connect your MetaMask wallet to view your real FTK token balance</p>
            </div>
          )}

          {/* Quick Actions for F-Mode */}
          <div className="card" role="region" aria-label="Quick Actions" style={{ marginTop: 16 }}>
            <h3>Quick Actions</h3>
            <div style={{ display:'flex', gap:12, flexWrap:'wrap' }}>
              <RouterLink className="btn btn-primary" to="/transactions">
                Crypto Transfer
              </RouterLink>
              <a 
                href="https://sepolia.etherscan.io" 
                target="_blank" 
                rel="noopener noreferrer"
                className="btn btn-secondary"
                style={{ textDecoration: 'none' }}
              >
                View on Etherscan ↗
              </a>
            </div>
          </div>
        </div>
      )}

      {/* Fiat Mode: Show loading/error states and fiat content */}
      {!fModeEnabled && (
        <>
          {loading && <p>Loading...</p>}
          {error && <p style={{ color: 'red' }}>{error}</p>}

          {!loading && !error && (
            <>
              {/* Prominent Balance Card */}
              <BalanceCard
                total={total}
                currency="NZD"
                status={total > 10000 ? 'excellent' : total > 5000 ? 'healthy' : total > 1000 ? 'warning' : 'low'}
                showTrend={false}
              />

              {/* Summary KPIs */}
              <div className="skills" aria-label="Financial KPIs">
                <div className="skill" role="region" aria-label="Accounts">
                  <div className="skill-label">Accounts <span>{accounts.length}</span></div>
                  <div className={`status-indicator ${accounts.length > 0 ? 'status-info' : 'status-warning'}`}>
                    {accounts.length > 0 ? 'Verified Accounts' : 'Action Required'}
                  </div>
                </div>
                <div className="skill" role="region" aria-label="Transactions">
                  <div className="skill-label">Transactions <span>{txCount}</span></div>
                  <div className={`status-indicator ${txCount > 0 ? 'status-success' : 'status-info'}`}>
                    {txCount > 0 ? 'Sync Complete' : 'Awaiting Activity'}
                  </div>
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
                  <RouterLink className="btn btn-primary" to="/transactions">
                    Send Money
                  </RouterLink>
                  <RouterLink className="btn btn-secondary" to="/accounts">
                    Create Account
                  </RouterLink>
                </div>
              </div>
            </>
          )}
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