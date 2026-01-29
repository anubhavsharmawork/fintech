import * as React from 'react';
import { getETHBalance, getFTKBalance, getTokenInfo, getEtherscanAddressUrl, getEtherscanTokenUrl, isValidAddress, DEMO_WALLET } from '../services/crypto';

interface Props {
  address?: string;
  demoAddress?: string;
  showTokenLink?: boolean;
  isDemo?: boolean;
}

const CryptoAccountSwitcher: React.FC<Props> = ({ address, demoAddress, showTokenLink = true, isDemo = false }) => {
  const [ethBalance, setEthBalance] = React.useState<string>('0');
  const [ftkBalance, setFtkBalance] = React.useState<string>('0');
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [tokenName, setTokenName] = React.useState<string>('FTK');
  const [lastUpdated, setLastUpdated] = React.useState<Date | null>(null);

  const effectiveAddress = address || demoAddress;

  const refresh = React.useCallback(async () => {
    if (!effectiveAddress) return;

    // In demo mode, use mock balances
    if (isDemo) {
      setEthBalance(DEMO_WALLET.ethBalance);
      setFtkBalance(DEMO_WALLET.ftkBalance);
      setTokenName('FTK');
      setLastUpdated(new Date());
      return;
    }

    if (!isValidAddress(effectiveAddress)) {
      setError('Invalid wallet address');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const [eth, ftk, tokenInfo] = await Promise.all([
        getETHBalance(effectiveAddress),
        getFTKBalance(effectiveAddress),
        getTokenInfo().catch(() => ({ name: 'FTK', symbol: 'FTK', decimals: 18 }))
      ]);

      setEthBalance(eth);
      setFtkBalance(ftk);
      setTokenName(tokenInfo.symbol);
      setLastUpdated(new Date());
    } catch (err: any) {
      setError(err.message || 'Failed to fetch balances');
    } finally {
      setLoading(false);
    }
  }, [effectiveAddress, isDemo]);

  React.useEffect(() => {
    refresh();
  }, [refresh]);

  const formatBalance = (balance: string, decimals: number = 4) => {
    const num = parseFloat(balance);
    if (isNaN(num)) return '0';
    if (num === 0) return '0';
    if (num < 0.0001) return '<0.0001';
    return num.toFixed(decimals);
  };

  const shortenAddress = (addr: string) => 
    `${addr.slice(0, 8)}...${addr.slice(-6)}`;

  return (
    <div className="card" style={{ marginBottom: 12 }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ fontWeight: 600, fontSize: '1.1rem', display: 'flex', alignItems: 'center', gap: 8 }}>
            <span>💰</span>
            Crypto Balances
          </div>
          {lastUpdated && (
            <span className="small" style={{ color: '#888' }}>
              Updated: {lastUpdated.toLocaleTimeString()}
            </span>
          )}
        </div>

        {effectiveAddress ? (
          <>
            <div style={{ 
              backgroundColor: 'var(--bg)', 
              padding: '8px 12px', 
              borderRadius: 8, 
              display: 'flex', 
              alignItems: 'center', 
              justifyContent: 'space-between' 
            }}>
              <span className="small" style={{ color: '#666' }}>Wallet:</span>
              {isDemo ? (
                <span style={{ fontFamily: 'monospace', color: '#d97706', fontSize: '0.85rem' }}>
                  {shortenAddress(effectiveAddress)} (Demo)
                </span>
              ) : (
                <a 
                  href={getEtherscanAddressUrl(effectiveAddress)}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{ fontFamily: 'monospace', color: 'var(--primary)', fontSize: '0.85rem' }}
                >
                  {shortenAddress(effectiveAddress)} ↗
                </a>
              )}
            </div>

            {error && (
              <div style={{ color: '#ef4444', padding: '8px', backgroundColor: 'rgba(239, 68, 68, 0.1)', borderRadius: 4, fontSize: '0.85rem' }}>
                ⚠️ {error}
              </div>
            )}

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
              {/* SepoliaETH Balance */}
              <div style={{ 
                backgroundColor: 'rgba(99, 102, 241, 0.1)', 
                padding: 16, 
                borderRadius: 12,
                textAlign: 'center'
              }}>
                <div style={{ fontSize: '0.75rem', color: '#666', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  SepoliaETH
                </div>
                <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--primary)', marginTop: 4 }}>
                  {loading ? '...' : formatBalance(ethBalance)}
                </div>
                <div className="small" style={{ color: '#888', marginTop: 4 }}>
                  For gas fees
                </div>
              </div>

              {/* FTK Token Balance */}
              <div style={{ 
                backgroundColor: 'rgba(34, 197, 94, 0.1)', 
                padding: 16, 
                borderRadius: 12,
                textAlign: 'center'
              }}>
                <div style={{ fontSize: '0.75rem', color: '#666', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  {tokenName} Token
                </div>
                <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#22c55e', marginTop: 4 }}>
                  {loading ? '...' : formatBalance(ftkBalance, 2)}
                </div>
                {showTokenLink && (
                  <a 
                    href={getEtherscanTokenUrl()}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="small"
                    style={{ color: 'var(--primary)', marginTop: 4, display: 'inline-block' }}
                  >
                    View contract ↗
                  </a>
                )}
              </div>
            </div>

            <button 
              className="btn btn-secondary" 
              type="button" 
              onClick={refresh} 
              disabled={loading || isDemo}
              style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6 }}
            >
              {isDemo ? '👁️ Demo Data' : loading ? '⏳ Refreshing…' : '🔄 Refresh Balances'}
            </button>

            <div className="small" style={{ color: '#888', textAlign: 'center' }}>
              {isDemo 
                ? 'Sample balances shown for demonstration purposes' 
                : 'Balances are fetched directly from Sepolia blockchain via RPC'}
            </div>
          </>
        ) : (
          <div style={{ textAlign: 'center', padding: 16, color: '#888' }}>
            <div style={{ fontSize: '2rem', marginBottom: 8 }}>👛</div>
            Connect your wallet to view real-time balances
          </div>
        )}
      </div>
    </div>
  );
};

export default CryptoAccountSwitcher;
