import * as React from 'react';
import { TransactionResult, waitForTransaction, getEtherscanTxUrl } from '../services/crypto';

interface Props {
  transaction: TransactionResult | null;
  provider: any;
  onConfirmed?: (result: TransactionResult) => void;
  onFailed?: (error: string) => void;
}

const TransactionStatus: React.FC<Props> = ({ transaction, provider, onConfirmed, onFailed }) => {
  const [status, setStatus] = React.useState<'pending' | 'confirming' | 'confirmed' | 'failed'>('pending');
  const [confirmations, setConfirmations] = React.useState(0);
  const [gasUsed, setGasUsed] = React.useState<string | null>(null);
  const [blockNumber, setBlockNumber] = React.useState<number | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!transaction || !provider) return;

    let cancelled = false;

    const trackTransaction = async () => {
      setStatus('confirming');
      
      try {
        const result = await waitForTransaction(provider, transaction.hash, 1);
        
        if (cancelled) return;

        if (result.status === 'confirmed') {
          setStatus('confirmed');
          setConfirmations(1);
          setGasUsed(result.gasUsed ?? null);
          setBlockNumber(result.blockNumber ?? null);
          onConfirmed?.({
            ...transaction,
            status: 'confirmed',
            gasUsed: result.gasUsed,
            blockNumber: result.blockNumber
          });
        } else {
          setStatus('failed');
          setError('Transaction reverted on-chain');
          onFailed?.('Transaction reverted');
        }
      } catch (err: any) {
        if (cancelled) return;
        setStatus('failed');
        const message = err.message || 'Failed to confirm transaction';
        setError(message);
        onFailed?.(message);
      }
    };

    trackTransaction();

    return () => {
      cancelled = true;
    };
  }, [transaction?.hash, provider]);

  if (!transaction) return null;

  const shortenHash = (hash: string) => `${hash.slice(0, 10)}...${hash.slice(-8)}`;

  const getStatusColor = () => {
    switch (status) {
      case 'pending':
      case 'confirming':
        return '#f59e0b';
      case 'confirmed':
        return '#22c55e';
      case 'failed':
        return '#ef4444';
    }
  };

  const getStatusIcon = () => {
    switch (status) {
      case 'pending':
      case 'confirming':
        return '⏳';
      case 'confirmed':
        return '✅';
      case 'failed':
        return '❌';
    }
  };

  const getStatusText = () => {
    switch (status) {
      case 'pending':
        return 'Transaction Pending';
      case 'confirming':
        return 'Waiting for Confirmation...';
      case 'confirmed':
        return 'Transaction Confirmed!';
      case 'failed':
        return 'Transaction Failed';
    }
  };

  return (
    <div
      className="card"
      style={{
        marginTop: 16,
        padding: 16,
        backgroundColor: `${getStatusColor()}10`,
        borderLeft: `4px solid ${getStatusColor()}`,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
        <span style={{ fontSize: '1.5rem' }}>{getStatusIcon()}</span>
        <span style={{ fontWeight: 600, color: getStatusColor() }}>
          {getStatusText()}
        </span>
      </div>

      <div style={{ display: 'grid', gap: 8, fontSize: '0.9rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
          <span style={{ color: '#666' }}>Transaction Hash:</span>
          <a
            href={transaction.etherscanUrl}
            target="_blank"
            rel="noopener noreferrer"
            style={{ color: 'var(--primary)', fontFamily: 'monospace' }}
          >
            {shortenHash(transaction.hash)} ↗
          </a>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
          <span style={{ color: '#666' }}>From:</span>
          <span style={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>
            {shortenHash(transaction.from)}
          </span>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
          <span style={{ color: '#666' }}>To:</span>
          <span style={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>
            {shortenHash(transaction.to)}
          </span>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
          <span style={{ color: '#666' }}>Amount:</span>
          <span style={{ fontWeight: 600 }}>{transaction.amount} FTK</span>
        </div>

        {gasUsed && (
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: '#666' }}>Gas Used:</span>
            <span>{gasUsed}</span>
          </div>
        )}

        {blockNumber && (
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: '#666' }}>Block Number:</span>
            <span>{blockNumber.toLocaleString()}</span>
          </div>
        )}

        {confirmations > 0 && (
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: '#666' }}>Confirmations:</span>
            <span style={{ color: '#22c55e' }}>{confirmations}</span>
          </div>
        )}
      </div>

      {error && (
        <div style={{ marginTop: 12, color: '#ef4444', fontSize: '0.85rem' }}>
          Error: {error}
        </div>
      )}

      <div style={{ marginTop: 16, textAlign: 'center' }}>
        <a
          href={transaction.etherscanUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="btn btn-secondary"
          style={{ textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: 6 }}
        >
          🔍 View on Etherscan
        </a>
      </div>

      {status === 'confirmed' && (
        <div
          style={{
            marginTop: 12,
            padding: '8px 12px',
            backgroundColor: 'rgba(34, 197, 94, 0.1)',
            borderRadius: 4,
            fontSize: '0.85rem',
            textAlign: 'center',
          }}
        >
          🎉 This transaction is now permanently recorded on the Sepolia blockchain and can be verified on Etherscan.
        </div>
      )}
    </div>
  );
};

export default TransactionStatus;
