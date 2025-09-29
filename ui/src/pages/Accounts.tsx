import * as React from 'react';

interface Account {
  id: string;
  accountNumber: string;
  accountType: string;
  balance: number;
  currency: string;
}

const Accounts: React.FC = () => {
  const [accounts, setAccounts] = React.useState<Account[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [initAmount, setInitAmount] = React.useState('');
  const [type, setType] = React.useState('Checking');

  const load = React.useCallback(async () => {
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
  }, []);

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
      <h2>My Accounts</h2>

      <div className="card" style={{ marginBottom: 16 }}>
        <h3>Create Account</h3>
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
      {!loading && !error && accounts.length === 0 && (
        <p>No accounts yet.</p>
      )}
      {accounts.map((account) => (
        <div key={account.id} className="card">
          <h3>{account.accountType} Account</h3>
          <p><strong>Account Number:</strong> {account.accountNumber ?? '—'}</p>
          <p><strong>Balance:</strong> {nzd.format(account.balance)}</p>
        </div>
      ))}
    </div>
  );
};

export default Accounts;