import * as React from 'react';
import { connectWallet, getEtherscanAddressUrl, isValidAddress, isMetaMaskAvailable, DEMO_WALLET, NetworkInfo } from '../services/crypto';
import NetworkStatus from './NetworkStatus';

interface Props {
  onConnected?: (address: string, signer: any, provider: any, isDemo?: boolean) => void;
  onDisconnected?: () => void;
}

const ConnectWallet: React.FC<Props> = ({ onConnected, onDisconnected }) => {
  const [address, setAddress] = React.useState<string | null>(null);
  const [isDemo, setIsDemo] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [networkOk, setNetworkOk] = React.useState(false);
  const [hasMetaMask] = React.useState(() => isMetaMaskAvailable());

  const handleNetworkChange = React.useCallback((info: NetworkInfo) => {
    setNetworkOk(info.isCorrectNetwork);
  }, []);

  const handleDemoMode = () => {
    setAddress(DEMO_WALLET.address);
    setIsDemo(true);
    setError(null);
    // Pass null for signer/provider in demo mode, with isDemo flag
    onConnected?.(DEMO_WALLET.address, null, null, true);
  };

  const handleConnect = async () => {
    setLoading(true);
    setError(null);
    try {
      const { signer, provider, address: addr } = await connectWallet();

      if (!isValidAddress(addr)) {
        throw new Error('Invalid wallet address returned');
      }

      setAddress(addr);
      onConnected?.(addr, signer, provider);
    } catch (err: any) {
      const message = err.message || 'Failed to connect wallet';
      setError(message);

      // Clear state on error
      setAddress(null);
    } finally {
      setLoading(false);
    }
  };

  const disconnect = () => {
    setAddress(null);
    setIsDemo(false);
    setError(null);
    onDisconnected?.();
  };

  // Listen for account changes
  React.useEffect(() => {
    const ethereum = (window as any).ethereum;
    if (!ethereum?.on) return;

    const handleAccountsChanged = (accounts: string[]) => {
      if (accounts.length === 0) {
        disconnect();
      } else if (address && accounts[0].toLowerCase() !== address.toLowerCase()) {
        // Account switched - reconnect
        handleConnect();
      }
    };

    ethereum.on('accountsChanged', handleAccountsChanged);
    return () => {
      ethereum.removeListener('accountsChanged', handleAccountsChanged);
    };
  }, [address]);

  const shortenAddress = (addr: string) => 
    `${addr.slice(0, 6)}...${addr.slice(-4)}`;

  return (
    <div className="card" style={{ marginBottom: 12 }}>
      <NetworkStatus onNetworkChange={handleNetworkChange} />

      {isDemo && (
        <div style={{ 
          marginTop: 12,
          padding: '10px 14px', 
          backgroundColor: 'rgba(251, 191, 36, 0.15)', 
          border: '1px solid rgba(251, 191, 36, 0.4)',
          borderRadius: 8,
          display: 'flex',
          alignItems: 'center',
          gap: 8
        }}>
          <span style={{ fontSize: '1.1rem' }}>👁️</span>
          <div>
            <strong style={{ color: '#d97706' }}>Demo Mode</strong>
            <div className="small" style={{ color: '#92400e' }}>
              Viewing with sample data. Transfers are disabled. Install MetaMask for real transactions.
            </div>
          </div>
        </div>
      )}

      <div style={{ display: 'flex', alignItems: 'center', gap: 12, justifyContent: 'space-between', marginTop: 12 }}>
        <div style={{ flex: 1 }}>
          <div style={{ fontWeight: 600, display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: '1.2rem' }}>{isDemo ? '👁️' : '🦊'}</span>
            Wallet {isDemo && <span style={{ fontSize: '0.75rem', color: '#d97706', fontWeight: 500 }}>(Demo)</span>}
          </div>
          {address ? (
            <div className="small" style={{ marginTop: 4 }}>
              <span style={{ 
                backgroundColor: isDemo ? 'rgba(251, 191, 36, 0.15)' : 'rgba(34, 197, 94, 0.1)', 
                color: isDemo ? '#d97706' : '#22c55e', 
                padding: '2px 8px', 
                borderRadius: 4, 
                fontSize: '0.75rem',
                marginRight: 8
              }}>
                {isDemo ? 'Demo Mode' : 'Connected'}
              </span>
              {isDemo ? (
                <span style={{ fontFamily: 'monospace', color: '#666' }}>
                  {shortenAddress(address)}
                </span>
              ) : (
                <a 
                  href={getEtherscanAddressUrl(address)} 
                  target="_blank" 
                  rel="noopener noreferrer"
                  style={{ color: 'var(--primary)', textDecoration: 'none' }}
                  title="View on Etherscan"
                >
                  {shortenAddress(address)} ↗
                </a>
              )}
            </div>
          ) : (
            <div className="small" style={{ color: '#888', marginTop: 4 }}>
              {hasMetaMask 
                ? 'Connect MetaMask to send real testnet transactions' 
                : 'MetaMask not detected. Use Demo Mode to preview or install MetaMask.'}
            </div>
          )}
        </div>
        {!address ? (
          <div style={{ display: 'flex', gap: 8 }}>
            {hasMetaMask ? (
              <button 
                className="btn btn-primary" 
                onClick={handleConnect} 
                disabled={loading}
                style={{ display: 'flex', alignItems: 'center', gap: 6 }}
              >
                {loading ? 'Connecting…' : '🔗 Connect Wallet'}
              </button>
            ) : (
              <button 
                className="btn btn-primary" 
                onClick={handleDemoMode}
                style={{ display: 'flex', alignItems: 'center', gap: 6 }}
              >
                👁️ View Demo
              </button>
            )}
            {hasMetaMask && (
              <button 
                className="btn btn-secondary" 
                onClick={handleDemoMode}
                style={{ fontSize: '0.85rem' }}
              >
                Demo
              </button>
            )}
          </div>
        ) : (
          <button className="btn btn-secondary" onClick={disconnect}>
            {isDemo ? 'Exit Demo' : 'Disconnect'}
          </button>
        )}
      </div>

      {error && (
        <div className="small" style={{ color: '#ef4444', marginTop: 8, padding: '8px', backgroundColor: 'rgba(239, 68, 68, 0.1)', borderRadius: 4 }}>
          ⚠️ {error}
        </div>
      )}

      {address && !isDemo && networkOk && (
        <div className="small" style={{ marginTop: 8, color: '#666', padding: '8px', backgroundColor: 'rgba(59, 130, 246, 0.1)', borderRadius: 4 }}>
          ✅ Ready for real blockchain transactions on Sepolia testnet. All transactions are verifiable on Etherscan.
        </div>
      )}
    </div>
  );
};

export default ConnectWallet;
