import * as React from 'react';
import { getAvailableBanks, connectBank, AvailableBank } from '../services/banking';

interface ConnectBankProps {
  onConnected: () => void;
}

const ConnectBank: React.FC<ConnectBankProps> = ({ onConnected }) => {
  const [banks, setBanks] = React.useState<AvailableBank[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [connecting, setConnecting] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [success, setSuccess] = React.useState<string | null>(null);
  const [selectedCountry, setSelectedCountry] = React.useState<string>('NZ');

  const loadBanks = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAvailableBanks(selectedCountry);
      setBanks(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load banks');
    } finally {
      setLoading(false);
    }
  }, [selectedCountry]);

  React.useEffect(() => { loadBanks(); }, [loadBanks]);

  const handleConnect = async (bank: AvailableBank) => {
    setConnecting(bank.id);
    setError(null);
    setSuccess(null);
    try {
      const result = await connectBank(bank.id);
      setSuccess(`Connected to ${bank.name}! Imported ${result.accountsImported} account(s).`);
      onConnected();
    } catch (err: any) {
      setError(err.message || 'Failed to connect bank');
    } finally {
      setConnecting(null);
    }
  };

  return (
    <div className="card">
      <h3>🏦 Connect Your Bank</h3>
      <p style={{ color: 'var(--text-secondary)', marginBottom: 16 }}>
        Securely link your bank accounts using Open Banking to see all your finances in one place.
      </p>

      <div className="form-group" style={{ marginBottom: 16 }}>
        <label>Country</label>
        <select 
          value={selectedCountry} 
          onChange={(e) => setSelectedCountry(e.target.value)}
          aria-label="Select country"
        >
          <option value="NZ">🇳🇿 New Zealand</option>
          <option value="AU">🇦🇺 Australia</option>
          <option value="UK">🇬🇧 United Kingdom</option>
        </select>
      </div>

      {error && <p style={{ color: 'var(--error)', marginBottom: 12 }}>{error}</p>}
      {success && <p style={{ color: 'var(--success)', marginBottom: 12 }}>{success}</p>}

      {loading ? (
        <p>Loading available banks...</p>
      ) : (
        <div style={{ display: 'grid', gap: 12 }}>
          {banks.map((bank) => (
            <div 
              key={bank.id}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: 12,
                background: 'var(--card-bg)',
                borderRadius: 8,
                border: '1px solid var(--border)'
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span style={{ fontSize: 24 }}>{bank.logo}</span>
                <span style={{ fontWeight: 500 }}>{bank.name}</span>
              </div>
              <button
                className="btn btn-primary"
                onClick={() => handleConnect(bank)}
                disabled={connecting !== null}
                aria-label={`Connect to ${bank.name}`}
              >
                {connecting === bank.id ? 'Connecting...' : 'Connect'}
              </button>
            </div>
          ))}
          {banks.length === 0 && (
            <p style={{ color: 'var(--text-secondary)' }}>No banks available for this country.</p>
          )}
        </div>
      )}

      <p style={{ color: 'var(--text-secondary)', fontSize: 12, marginTop: 16 }}>
        🔒 Your credentials are never stored. We use secure Open Banking protocols.
      </p>
    </div>
  );
};

export default ConnectBank;
