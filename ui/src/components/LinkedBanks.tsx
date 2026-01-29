import * as React from 'react';
import { 
  getConnectedBanks, 
  getExternalAccounts, 
  disconnectBank, 
  syncBankAccounts,
  BankConnection, 
  ExternalBankAccount 
} from '../services/banking';

interface LinkedBanksProps {
  refreshTrigger?: number;
}

const LinkedBanks: React.FC<LinkedBanksProps> = ({ refreshTrigger }) => {
  const [connections, setConnections] = React.useState<BankConnection[]>([]);
  const [accounts, setAccounts] = React.useState<ExternalBankAccount[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [syncing, setSyncing] = React.useState<string | null>(null);
  const [disconnecting, setDisconnecting] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [connectionsData, accountsData] = await Promise.all([
        getConnectedBanks(),
        getExternalAccounts()
      ]);
      setConnections(connectionsData);
      setAccounts(accountsData);
    } catch (err: any) {
      setError(err.message || 'Failed to load bank data');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => { load(); }, [load, refreshTrigger]);

  const handleSync = async (connectionId: string) => {
    setSyncing(connectionId);
    try {
      await syncBankAccounts(connectionId);
      await load();
    } catch (err: any) {
      setError(err.message || 'Failed to sync accounts');
    } finally {
      setSyncing(null);
    }
  };

  const handleDisconnect = async (connectionId: string, bankName: string) => {
    if (!window.confirm(`Are you sure you want to disconnect ${bankName}?`)) return;
    
    setDisconnecting(connectionId);
    try {
      await disconnectBank(connectionId);
      await load();
    } catch (err: any) {
      setError(err.message || 'Failed to disconnect bank');
    } finally {
      setDisconnecting(null);
    }
  };

  const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' });

  const totalBalance = accounts.reduce((sum, acc) => sum + acc.balance, 0);

  if (loading) return <p>Loading connected banks...</p>;
  if (error) return <p style={{ color: 'var(--error)' }}>{error}</p>;
  if (connections.length === 0) return null;

  return (
    <div>
      {/* Total External Balance */}
      <div className="card" style={{ marginBottom: 16, background: 'linear-gradient(135deg, var(--primary), var(--accent))' }}>
        <h3 style={{ color: 'white', marginBottom: 8 }}>💰 Connected Bank Total</h3>
        <p style={{ color: 'white', fontSize: 28, fontWeight: 'bold', margin: 0 }}>
          {nzd.format(totalBalance)}
        </p>
        <p style={{ color: 'rgba(255,255,255,0.8)', fontSize: 12, marginTop: 4 }}>
          Across {connections.length} bank(s) • {accounts.length} account(s)
        </p>
      </div>

      {/* Connected Banks */}
      <h3 style={{ marginBottom: 12 }}>🔗 Linked Banks</h3>
      {connections.map((conn) => {
        const bankAccounts = accounts.filter(a => a.bankName === conn.bankName);
        const bankTotal = bankAccounts.reduce((sum, a) => sum + a.balance, 0);

        return (
          <div key={conn.id} className="card" style={{ marginBottom: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span style={{ fontSize: 24 }}>{conn.bankLogo}</span>
                <div>
                  <h4 style={{ margin: 0 }}>{conn.bankName}</h4>
                  <span 
                    style={{ 
                      fontSize: 11, 
                      padding: '2px 6px', 
                      borderRadius: 4, 
                      background: conn.status === 'Active' ? 'var(--success)' : 'var(--warning)',
                      color: 'white'
                    }}
                  >
                    {conn.status}
                  </span>
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  className="btn btn-secondary"
                  onClick={() => handleSync(conn.id)}
                  disabled={syncing !== null}
                  aria-label={`Sync ${conn.bankName} accounts`}
                  style={{ fontSize: 12, padding: '6px 12px' }}
                >
                  {syncing === conn.id ? '⟳ Syncing...' : '⟳ Sync'}
                </button>
                <button
                  className="btn"
                  onClick={() => handleDisconnect(conn.id, conn.bankName)}
                  disabled={disconnecting !== null}
                  aria-label={`Disconnect ${conn.bankName}`}
                  style={{ fontSize: 12, padding: '6px 12px', background: 'var(--error)', color: 'white' }}
                >
                  {disconnecting === conn.id ? 'Disconnecting...' : 'Disconnect'}
                </button>
              </div>
            </div>

            {/* Bank Accounts */}
            <div style={{ borderTop: '1px solid var(--border)', paddingTop: 12 }}>
              {bankAccounts.map((account) => (
                <div 
                  key={account.id}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    padding: '8px 0',
                    borderBottom: '1px solid var(--border)'
                  }}
                >
                  <div>
                    <p style={{ margin: 0, fontWeight: 500 }}>{account.accountName}</p>
                    <p style={{ margin: 0, fontSize: 12, color: 'var(--text-secondary)' }}>
                      {account.accountType} • {account.accountNumber}
                    </p>
                  </div>
                  <p style={{ margin: 0, fontWeight: 600, fontSize: 16 }}>
                    {nzd.format(account.balance)}
                  </p>
                </div>
              ))}
              <div style={{ display: 'flex', justifyContent: 'space-between', paddingTop: 8 }}>
                <span style={{ fontWeight: 500 }}>Bank Total</span>
                <span style={{ fontWeight: 600 }}>{nzd.format(bankTotal)}</span>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};

export default LinkedBanks;
