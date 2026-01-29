import * as React from 'react';
import { useFMode } from '../hooks/useFMode';
import CryptoAccountSwitcher from '../components/CryptoAccountSwitcher';
import ConnectWallet from '../components/ConnectWallet';
import BalanceCard from '../components/BalanceCard';
import ConnectBank from '../components/ConnectBank';
import LinkedBanks from '../components/LinkedBanks';

interface Account {
  id: string;
  accountNumber: string;
  accountType: string;
  balance: number;
  currency: string;
}

const Accounts: React.FC = () => {
  const { enabled: fModeEnabled } = useFMode();
  const [accounts, setAccounts] = React.useState<Account[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [initAmount, setInitAmount] = React.useState('');
  const [type, setType] = React.useState('Checking');
  const [showConnectBank, setShowConnectBank] = React.useState(false);
  const [bankRefreshTrigger, setBankRefreshTrigger] = React.useState(0);

  const load = React.useCallback(async () => {
    if (fModeEnabled) {
      setLoading(false);
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const token = localStorage.getItem('token');
      const res = await fetch('/accounts', { headers: { 'Authorization': token ? `Bearer ${token}` : '' } });
      if (!res.ok) throw new Error(`Failed to load accounts (${res.status})`);
      const data = await res.json();
      setAccounts(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load accounts');
    } finally {
      setLoading(false);
    }
  }, [fModeEnabled]);

  React.useEffect(() => { load(); }, [load]);

  const createAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    try {
      const token = localStorage.getItem('token');
      const payload = { accountType: type, currency: 'NZD', initialDeposit: parseFloat(initAmount || '0') };
      const res = await fetch('/accounts', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': token ? `Bearer ${token}` : '' },
        body: JSON.stringify(payload)
      });
      if (!res.ok) throw new Error(`Failed to create account (${res.status})`);
      await res.json();
      setInitAmount('');
      await load();
    } catch (err: any) {
      setError(err.message || 'Failed to create account');
    } finally {
      setCreating(false);
    }
  };

  const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' });

  return (
    <div>
      <h2>{fModeEnabled ? 'Crypto Wallet' : 'My Accounts'}</h2>

      {fModeEnabled ? (
        <div style={{ marginTop: 24 }}>
          <ConnectWallet onConnected={(addr) => console.log('Connected', addr)} />
          <CryptoAccountSwitcher demoAddress="0x742d35Cc6634C0532925a3b844Bc454e4438f44e" />
        </div>
      ) : (
        <>
          {/* Bank Connection Section */}
          <div style={{ marginBottom: 24 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <h3 style={{ margin: 0 }}>🏦 External Bank Accounts</h3>
              <button
                className="btn btn-primary"
                onClick={() => setShowConnectBank(!showConnectBank)}
                aria-label={showConnectBank ? 'Cancel bank connection' : 'Connect a new bank'}
              >
                {showConnectBank ? '✕ Cancel' : '+ Connect Bank'}
              </button>
            </div>

            {showConnectBank && (
              <ConnectBank 
                onConnected={() => {
                  setShowConnectBank(false);
                  setBankRefreshTrigger(prev => prev + 1);
                }} 
              />
            )}

            <LinkedBanks refreshTrigger={bankRefreshTrigger} />
          </div>

          <hr style={{ border: 'none', borderTop: '1px solid var(--border)', margin: '24px 0' }} />

          {/* Internal Accounts Section */}
          <div className="card" style={{ marginBottom: 16 }}>
            <h3>Create Internal Account</h3>
            <form onSubmit={createAccount}>
              <div className="form-group">
                <label>Type</label>
                <select value={type} onChange={e => setType(e.target.value)}>
                  <option>Checking</option>
                  <option>Savings</option>
                </select>
              </div>
              <div className="form-group">
                <label>Initial Deposit (NZD)</label>
                <input type="number" step="0.01" min="0" value={initAmount} onChange={e => setInitAmount(e.target.value)} />
              </div>
              <button className="btn btn-primary" type="submit" disabled={creating}>{creating ? 'Creating...' : 'Add New Account'}</button>
            </form>
          </div>

          {loading && <p>Loading...</p>}
          {error && <p style={{ color: 'red' }}>{error}</p>}
          
          {!loading && !error && accounts.length > 0 && (
            <>
              {/* Total Balance Card */}
              <BalanceCard
                total={accounts.reduce((sum, a) => sum + a.balance, 0)}
                currency="NZD"
                status={accounts.reduce((sum, a) => sum + a.balance, 0) > 5000 ? 'healthy' : 'warning'}
                showTrend={false}
              />
            </>
          )}
          
          {!loading && !error && accounts.length === 0 && (
            <p>No internal accounts yet. Create one above or connect your external banks.</p>
          )}

          <h3 style={{ marginTop: 24, marginBottom: 16 }}>Your Internal Accounts</h3>
          {accounts.map((account) => (
            <div key={account.id} className="card">
              <h4>{account.accountType} Account</h4>
              <p><strong>Account Number:</strong> {account.accountNumber ?? 'N/A'}</p>
              <p><strong>Balance:</strong> {nzd.format(account.balance)}</p>
            </div>
          ))}
        </>
      )}
    </div>
  );
};

export default Accounts;