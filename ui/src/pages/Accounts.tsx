import * as React from 'react';
import { Landmark, X } from 'lucide-react';
import { isCorporateUser, getOrganisationId } from '../auth';
import { useFMode } from '../hooks/useFMode';
import { useAsync } from '../hooks/useAsync';
import PageLoader from '../components/PageLoader';
import CryptoAccountSwitcher from '../components/CryptoAccountSwitcher';
import ConnectWallet from '../components/ConnectWallet';
import BalanceCard from '../components/BalanceCard';
import ConnectBank from '../components/ConnectBank';
import LinkedBanks from '../components/LinkedBanks';
import FundAccountModal from '../components/FundAccountModal';
import { apiRequest, apiPost } from '../api/apiClient';
import { API } from '../config/constants';

interface Account {
  id: string;
  accountNumber: string;
  accountType: string;
  balance: number;
  currency: string;
}

const Accounts: React.FC = () => {
  const { enabled: fModeEnabled } = useFMode();
  const [creating, setCreating] = React.useState(false);
  const [localError, setLocalError] = React.useState<string | null>(null);
  const [type, setType] = React.useState('Checking');
  const [showConnectBank, setShowConnectBank] = React.useState(false);
  const [bankRefreshTrigger, setBankRefreshTrigger] = React.useState(0);
  const [fundingAccountId, setFundingAccountId] = React.useState<string | null>(null);

  // Use useAsync for accounts loading
  const { data: accounts, loading, error: asyncError, refetch } = useAsync<Account[]>(
    async (signal) => {
      if (fModeEnabled) {
        return [];
      }
      const res = await apiRequest(API.ACCOUNTS, { signal });
      if (!res.ok) throw new Error(`Failed to load accounts (${res.status})`);
      return res.json();
    },
    [fModeEnabled]
  );

  const accountsList = React.useMemo(() => accounts ?? [], [accounts]);
  const error = localError || asyncError;

  const createAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    setLocalError(null);
    try {
      const payload = { accountType: type, currency: 'NZD' };
      await apiPost(API.ACCOUNTS, payload);
      refetch();
    } catch (err: any) {
      setLocalError(err.message || 'Failed to create account');
    } finally {
      setCreating(false);
    }
  };

  const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' });

  return (
    <div>
      <h2>{fModeEnabled ? 'Crypto Wallet' : 'My Accounts'}</h2>

      {!fModeEnabled && isCorporateUser() && (
        <div className="card" role="region" aria-label="Organisation Accounts" style={{ marginBottom: 16, borderLeft: '4px solid #2563eb' }}>
          <p className="small" style={{ margin: 0 }}>
            <strong>Corporate Account</strong> &mdash; These accounts belong to your organisation.
            Organisation ID: <code>{getOrganisationId()}</code>
          </p>
        </div>
      )}

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
              <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}><Landmark size={20} color="currentColor" /> External Bank Accounts</h3>
              <button
                className="btn btn-primary"
                onClick={() => setShowConnectBank(!showConnectBank)}
                aria-label={showConnectBank ? 'Cancel bank connection' : 'Connect a new bank'}
              >
                {showConnectBank ? <><X size={16} color="currentColor" /> Cancel</> : '+ Connect Bank'}
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
              <button className="btn btn-primary" type="submit" disabled={creating}>{creating ? 'Creating...' : 'Add New Account'}</button>
            </form>
          </div>

          {loading && <PageLoader />}
           {error && <p style={{ color: '#b91c1c' }}>{error}</p>}

          {!loading && !error && accountsList.length > 0 && (
            <>
              {/* Total Balance Card */}
              <BalanceCard
                total={accountsList.reduce((sum, a) => sum + a.balance, 0)}
                currency="NZD"
                status={accountsList.reduce((sum, a) => sum + a.balance, 0) > 5000 ? 'healthy' : 'warning'}
                showTrend={false}
              />
            </>
          )}

          {!loading && !error && accountsList.length === 0 && (
            <p>No internal accounts yet. Create one above, then fund it using your connected external banks.</p>
          )}

          <h3 style={{ marginTop: 24, marginBottom: 16 }}>Your Internal Accounts</h3>
          {accountsList.map((account) => (
            <div key={account.id} className="card">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                  <h4 style={{ margin: '0 0 4px 0' }}>{account.accountType} Account</h4>
                  <p style={{ margin: '0 0 4px 0', fontSize: 14, color: 'var(--muted)' }}>
                    {account.accountNumber ?? 'N/A'}
                  </p>
                  <p style={{ margin: 0, fontSize: 22, fontWeight: 700 }}>{nzd.format(account.balance)}</p>
                </div>
                <button
                  className="btn btn-primary"
                  onClick={() => setFundingAccountId(fundingAccountId === account.id ? null : account.id)}
                  aria-label={`Fund ${account.accountType} account`}
                  style={{ fontSize: 14, padding: '8px 16px', whiteSpace: 'nowrap' }}
                >
                  {fundingAccountId === account.id ? <><X size={16} color="currentColor" /> Close</> : '+ Fund Account'}
                </button>
              </div>
              {fundingAccountId === account.id && (
                <FundAccountModal
                  accountId={account.id}
                  accountType={account.accountType}
                  currency={account.currency || 'NZD'}
                  onComplete={() => { setFundingAccountId(null); refetch(); }}
                  onCancel={() => setFundingAccountId(null)}
                />
              )}
            </div>
          ))}
        </>
      )}
    </div>
  );
};

export default Accounts;
