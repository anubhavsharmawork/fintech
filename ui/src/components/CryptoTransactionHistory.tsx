import * as React from 'react';
import { getRecentTransfers, getEtherscanAddressUrl, getEtherscanTokenUrl, FTK_TOKEN_ADDRESS } from '../services/crypto';

interface Transfer {
  txHash: string;
  from: string;
  to: string;
  amount: string;
  blockNumber: number;
  etherscanUrl: string;
}

interface Props {
  address: string | null;
  refreshTrigger?: number;
}

const CryptoTransactionHistory: React.FC<Props> = ({ address, refreshTrigger }) => {
  const [transfers, setTransfers] = React.useState<Transfer[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [expanded, setExpanded] = React.useState(false);

  const loadTransfers = React.useCallback(async () => {
    if (!address) return;
    
    setLoading(true);
    setError(null);
    
    try {
      const data = await getRecentTransfers(address, 5000);
      setTransfers(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load transfer history');
    } finally {
      setLoading(false);
    }
  }, [address]);

  React.useEffect(() => {
    loadTransfers();
  }, [loadTransfers, refreshTrigger]);

  const shortenAddress = (addr: string) => `${addr.slice(0, 6)}...${addr.slice(-4)}`;
  const shortenHash = (hash: string) => `${hash.slice(0, 10)}...${hash.slice(-6)}`;

  const isOutgoing = (transfer: Transfer) => 
    transfer.from.toLowerCase() === address?.toLowerCase();

  if (!address) {
    return null;
  }

  const displayTransfers = expanded ? transfers : transfers.slice(0, 5);

  return (
    <div className="card" style={{ marginTop: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
          <span>⛓️</span>
          On-Chain FTK Transfers
        </h3>
        <div style={{ display: 'flex', gap: 8 }}>
          <a
            href={getEtherscanTokenUrl()}
            target="_blank"
            rel="noopener noreferrer"
            className="btn btn-secondary"
            style={{ fontSize: '0.8rem', padding: '4px 8px', textDecoration: 'none' }}
          >
            View Token ↗
          </a>
          <button
            className="btn btn-secondary"
            onClick={loadTransfers}
            disabled={loading}
            style={{ fontSize: '0.8rem', padding: '4px 8px' }}
          >
            {loading ? '⏳' : '🔄'} Refresh
          </button>
        </div>
      </div>

      <div className="small" style={{ marginBottom: 12, color: '#666' }}>
        Real blockchain transactions from Sepolia testnet • 
        <a 
          href={getEtherscanAddressUrl(address)} 
          target="_blank" 
          rel="noopener noreferrer"
          style={{ marginLeft: 4, color: 'var(--primary)' }}
        >
          View all on Etherscan ↗
        </a>
      </div>

      {error && (
        <div style={{ color: '#ef4444', padding: '8px', backgroundColor: 'rgba(239, 68, 68, 0.1)', borderRadius: 4, marginBottom: 12 }}>
          {error}
        </div>
      )}

      {loading && transfers.length === 0 && (
        <div style={{ textAlign: 'center', padding: 24, color: '#666' }}>
          Loading on-chain transfers...
        </div>
      )}

      {!loading && transfers.length === 0 && (
        <div style={{ textAlign: 'center', padding: 24, color: '#666' }}>
          <div style={{ fontSize: '2rem', marginBottom: 8 }}>📭</div>
          No FTK transfers found for this address.
          <div style={{ marginTop: 8, fontSize: '0.85rem' }}>
            Make a transfer to see it appear here!
          </div>
        </div>
      )}

      {displayTransfers.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {displayTransfers.map((transfer, index) => (
            <a
              key={`${transfer.txHash}-${index}`}
              href={transfer.etherscanUrl}
              target="_blank"
              rel="noopener noreferrer"
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                padding: '12px',
                backgroundColor: 'var(--bg)',
                borderRadius: 8,
                textDecoration: 'none',
                color: 'inherit',
                border: '1px solid var(--border)',
                transition: 'background-color 0.2s',
              }}
              onMouseOver={(e) => {
                (e.currentTarget as HTMLElement).style.backgroundColor = 'rgba(99, 102, 241, 0.05)';
              }}
              onMouseOut={(e) => {
                (e.currentTarget as HTMLElement).style.backgroundColor = 'var(--bg)';
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span style={{ 
                  fontSize: '1.5rem',
                  opacity: 0.8
                }}>
                  {isOutgoing(transfer) ? '📤' : '📥'}
                </span>
                <div>
                  <div style={{ fontWeight: 500, fontSize: '0.9rem' }}>
                    {isOutgoing(transfer) ? 'Sent to' : 'Received from'}{' '}
                    <span style={{ fontFamily: 'monospace', color: 'var(--primary)' }}>
                      {shortenAddress(isOutgoing(transfer) ? transfer.to : transfer.from)}
                    </span>
                  </div>
                  <div className="small" style={{ color: '#888', marginTop: 2 }}>
                    Block #{transfer.blockNumber.toLocaleString()} • {shortenHash(transfer.txHash)}
                  </div>
                </div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ 
                  fontWeight: 600, 
                  color: isOutgoing(transfer) ? '#ef4444' : '#22c55e',
                  fontSize: '1rem'
                }}>
                  {isOutgoing(transfer) ? '-' : '+'}{parseFloat(transfer.amount).toFixed(2)} FTK
                </div>
                <div className="small" style={{ color: 'var(--primary)' }}>
                  View ↗
                </div>
              </div>
            </a>
          ))}
        </div>
      )}

      {transfers.length > 5 && (
        <button
          className="btn btn-secondary"
          onClick={() => setExpanded(!expanded)}
          style={{ width: '100%', marginTop: 12 }}
        >
          {expanded ? `Show Less` : `Show All ${transfers.length} Transfers`}
        </button>
      )}

      {transfers.length > 0 && (
        <div style={{ marginTop: 12, padding: '8px', backgroundColor: 'rgba(59, 130, 246, 0.1)', borderRadius: 4, fontSize: '0.8rem', textAlign: 'center' }}>
          💡 All transactions shown are real Sepolia testnet transactions verifiable on Etherscan
        </div>
      )}
    </div>
  );
};

export default CryptoTransactionHistory;
