import * as React from 'react';
import { getNetworkInfo, switchToSepolia, SEPOLIA_CHAIN_ID, ETHERSCAN_BASE_URL, NetworkInfo } from '../services/crypto';

interface Props {
  onNetworkChange?: (info: NetworkInfo) => void;
}

const NetworkStatus: React.FC<Props> = ({ onNetworkChange }) => {
  const [networkInfo, setNetworkInfo] = React.useState<NetworkInfo | null>(null);
  const [switching, setSwitching] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const checkNetwork = React.useCallback(async () => {
    try {
      const info = await getNetworkInfo();
      setNetworkInfo(info);
      onNetworkChange?.(info);
      setError(null);
    } catch (err: any) {
      setError(err.message || 'Failed to detect network');
    }
  }, [onNetworkChange]);

  React.useEffect(() => {
    checkNetwork();

    // Listen for network changes
    const ethereum = (window as any).ethereum;
    if (ethereum?.on) {
      const handleChainChanged = () => {
        checkNetwork();
      };
      ethereum.on('chainChanged', handleChainChanged);
      return () => {
        ethereum.removeListener('chainChanged', handleChainChanged);
      };
    }
  }, [checkNetwork]);

  const handleSwitch = async () => {
    setSwitching(true);
    setError(null);
    try {
      await switchToSepolia();
      await checkNetwork();
    } catch (err: any) {
      setError(err.message || 'Failed to switch network');
    } finally {
      setSwitching(false);
    }
  };

  if (!networkInfo) {
    return null;
  }

  const isCorrect = networkInfo.chainId === SEPOLIA_CHAIN_ID;

  return (
    <div
      className="card"
      style={{
        marginBottom: 12,
        padding: '8px 12px',
        backgroundColor: isCorrect ? 'rgba(34, 197, 94, 0.1)' : 'rgba(239, 68, 68, 0.1)',
        borderLeft: `4px solid ${isCorrect ? '#22c55e' : '#ef4444'}`,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span
            style={{
              width: 10,
              height: 10,
              borderRadius: '50%',
              backgroundColor: isCorrect ? '#22c55e' : '#ef4444',
              display: 'inline-block',
            }}
          />
          <div>
            <span style={{ fontWeight: 600, fontSize: '0.9rem' }}>
              {networkInfo.name}
            </span>
            {isCorrect && (
              <a
                href={ETHERSCAN_BASE_URL}
                target="_blank"
                rel="noopener noreferrer"
                className="small"
                style={{ marginLeft: 8, color: 'var(--primary)' }}
              >
                View on Etherscan ↗
              </a>
            )}
          </div>
        </div>

        {!isCorrect && (
          <button
            className="btn btn-primary"
            onClick={handleSwitch}
            disabled={switching}
            style={{ fontSize: '0.85rem', padding: '6px 12px' }}
          >
            {switching ? 'Switching…' : 'Switch to Sepolia'}
          </button>
        )}
      </div>

      {error && (
        <div className="small" style={{ color: '#ef4444', marginTop: 6 }}>
          {error}
        </div>
      )}

      {!isCorrect && (
        <div className="small" style={{ marginTop: 6, color: '#666' }}>
          Please switch to Sepolia Testnet to use blockchain features. Your transactions will be verifiable on Etherscan.
        </div>
      )}
    </div>
  );
};

export default NetworkStatus;
