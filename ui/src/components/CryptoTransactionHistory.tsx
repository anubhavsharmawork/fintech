import * as React from 'react';
import { getRecentTransfers, getEtherscanAddressUrl, getEtherscanTokenUrl, FTK_TOKEN_ADDRESS } from '../services/crypto';
import { usePagination } from '../hooks/usePagination';
import Pagination from './Pagination';

/* ─── Inline SVG Icons ─── */
const IconArrowUpRight = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="7" y1="17" x2="17" y2="7"/><polyline points="7 7 17 7 17 17"/></svg>
);
const IconArrowDownLeft = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="17" y1="7" x2="7" y2="17"/><polyline points="17 17 7 17 7 7"/></svg>
);
const IconLoader = () => (
  <svg className="crypto-icon-spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 12a9 9 0 1 1-6.219-8.56"/></svg>
);
const IconRefreshCw = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/></svg>
);
const IconInbox = () => (
  <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="22 12 16 12 14 15 10 15 8 12 2 12"/><path d="M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/></svg>
);
const IconExternalLink = () => (
  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg>
);
const IconLink = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>
);

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
  const pagination = usePagination({ defaultPageSize: 10, syncToUrl: false });

  // Update pagination total when transfers change
  React.useEffect(() => {
    pagination.setTotalCount(transfers.length);
  }, [transfers.length, pagination]);

  // Paginated transfers
  const paginatedTransfers = React.useMemo(() => {
    const start = (pagination.page - 1) * pagination.pageSize;
    return transfers.slice(start, start + pagination.pageSize);
  }, [transfers, pagination.page, pagination.pageSize]);

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

  return (
    <div className="card" style={{ marginTop: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ display: 'flex', color: 'var(--muted)' }}><IconLink /></span>
          On-Chain FTK Transfers
        </h3>
        <div style={{ display: 'flex', gap: 8 }}>
          <a
            href={getEtherscanTokenUrl()}
            target="_blank"
            rel="noopener noreferrer"
            className="btn btn-secondary"
            style={{ fontSize: '0.8rem', padding: '4px 8px', textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: 4 }}
          >
            View Token <IconExternalLink />
          </a>
          <button
            className="btn btn-secondary"
            onClick={loadTransfers}
            disabled={loading}
            style={{ fontSize: '0.8rem', padding: '4px 8px', display: 'inline-flex', alignItems: 'center', gap: 4 }}
          >
            {loading ? <IconLoader /> : <IconRefreshCw />} Refresh
          </button>
        </div>
      </div>

      <div className="small" style={{ marginBottom: 12, color: '#666' }}>
        Real blockchain transactions from Sepolia testnet • 
        <a 
          href={getEtherscanAddressUrl(address)} 
          target="_blank" 
          rel="noopener noreferrer"
          style={{ marginLeft: 4, color: 'var(--primary)', display: 'inline-flex', alignItems: 'center', gap: 3 }}
        >
          View all on Etherscan <IconExternalLink />
        </a>
      </div>

      {error && (
        <div style={{ color: '#991b1b', padding: '8px', backgroundColor: 'rgba(239, 68, 68, 0.1)', borderRadius: 4, marginBottom: 12 }}>
          {error}
        </div>
      )}

      {loading && transfers.length === 0 && (
        <div style={{ textAlign: 'center', padding: 24, color: '#666', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8 }}>
          <IconLoader />
          Loading on-chain transfers...
        </div>
      )}

      {!loading && transfers.length === 0 && (
        <div style={{ textAlign: 'center', padding: 24, color: '#666', display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
          <div style={{ marginBottom: 8, color: 'var(--muted)', opacity: 0.5 }}><IconInbox /></div>
          No FTK transfers found for this address.
          <div style={{ marginTop: 8, fontSize: '0.85rem' }}>
            Make a transfer to see it appear here!
          </div>
        </div>
      )}

      {paginatedTransfers.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {paginatedTransfers.map((transfer, index) => (
            <a
              key={`${transfer.txHash}-${index}`}
              href={transfer.etherscanUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="crypto-tx-row"
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span className={`crypto-tx-icon ${isOutgoing(transfer) ? 'crypto-tx-icon--out' : 'crypto-tx-icon--in'}`}>
                  {isOutgoing(transfer) ? <IconArrowUpRight /> : <IconArrowDownLeft />}
                </span>
                <div>
                  <div style={{ fontWeight: 500, fontSize: '0.9rem' }}>
                    {isOutgoing(transfer) ? 'Sent to' : 'Received from'}{' '}
                    <span style={{ fontFamily: 'monospace', color: 'var(--primary)' }}>
                      {shortenAddress(isOutgoing(transfer) ? transfer.to : transfer.from)}
                    </span>
                  </div>
                  <div className="small" style={{ color: '#6b7280', marginTop: 2 }}>
                    Block #{transfer.blockNumber.toLocaleString()} • {shortenHash(transfer.txHash)}
                  </div>
                </div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ 
                  fontWeight: 600, 
                  color: isOutgoing(transfer) ? '#b91c1c' : '#047857',
                  fontSize: '1rem'
                }}>
                  {isOutgoing(transfer) ? '-' : '+'}{parseFloat(transfer.amount).toFixed(2)} FTK
                </div>
                <div className="small" style={{ color: 'var(--primary)', display: 'inline-flex', alignItems: 'center', gap: 3 }}>
                  View <IconExternalLink />
                </div>
              </div>
            </a>
          ))}
        </div>
      )}

      {transfers.length > 0 && <Pagination pagination={pagination} />}

      {transfers.length > 0 && (
        <div style={{ marginTop: 12, padding: '8px', backgroundColor: 'rgba(59, 130, 246, 0.1)', borderRadius: 4, fontSize: '0.8rem', textAlign: 'center' }}>
          All transactions shown are real Sepolia testnet transactions verifiable on Etherscan
        </div>
      )}
    </div>
  );
};

export default CryptoTransactionHistory;
