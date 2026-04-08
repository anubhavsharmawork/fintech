import * as React from 'react';
import { Landmark, Lock, Search, Shield, Check, X } from 'lucide-react';
import { getAvailableBanks, connectBank, AvailableBank } from '../services/banking';

// Official brand colours sourced from each bank's published brand guidelines.
// Icon URLs use vectorlogo.zone which is explicitly allow-listed in the server CSP.
const BANK_META: Record<string, { color: string; iconUrl: string | null; initials: string }> = {
    // ── New Zealand ──────────────────────────────────────────────────────────
    nz_anz: { color: '#E9ECED', iconUrl: 'https://www.vectorlogo.zone/logos/anzcomau/anzcomau-icon.svg', initials: 'ANZ' },
    nz_asb: { color: '#E87722', iconUrl: null, initials: 'ASB' },
    nz_bnz: { color: '#D4002A', iconUrl: 'https://www.vectorlogo.zone/logos/bnzconz/bnzconz-icon.svg', initials: 'BNZ' },
    nz_westpac: { color: '#D5002B', iconUrl: 'https://www.vectorlogo.zone/logos/westpaccomau/westpaccomau-icon.svg', initials: 'WPC' },
    nz_kiwibank: { color: '#00A859', iconUrl: null, initials: 'KWB' },
    // ── Australia ─────────────────────────────────────────────────────────────
    au_commbank: { color: '#FFD200', iconUrl: 'https://www.vectorlogo.zone/logos/commbankcomau/commbankcomau-icon.svg', initials: 'CBA' },
    au_nab: { color: '#CC0000', iconUrl: 'https://www.vectorlogo.zone/logos/nabcomau/nabcomau-icon.svg', initials: 'NAB' },
    au_westpac: { color: '#D5002B', iconUrl: 'https://www.vectorlogo.zone/logos/westpaccomau/westpaccomau-icon.svg', initials: 'WPC' },
    // ── United Kingdom ────────────────────────────────────────────────────────
    uk_hsbc: { color: '#DB0011', iconUrl: 'https://www.vectorlogo.zone/logos/hsbc/hsbc-icon.svg', initials: 'HSBC' },
    uk_barclays: { color: '#00AEEF', iconUrl: 'https://www.vectorlogo.zone/logos/barclays/barclays-icon.svg', initials: 'BARC' },
};

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
    const [searchQuery, setSearchQuery] = React.useState('');
    const [selectedBank, setSelectedBank] = React.useState<AvailableBank | null>(null);

    const loadBanks = React.useCallback(async () => {
        setLoading(true);
        setError(null);
        setSelectedBank(null);
        setSearchQuery('');
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

    const filteredBanks = React.useMemo(() => {
        if (!searchQuery.trim()) return banks;
        const q = searchQuery.toLowerCase();
        return banks.filter((b) => b.name.toLowerCase().includes(q));
    }, [banks, searchQuery]);

    const handleConnect = async (bank: AvailableBank) => {
        setConnecting(bank.id);
        setError(null);
        setSuccess(null);
        try {
            const result = await connectBank(bank.id);
            setSuccess(`Connected to ${bank.name}! Imported ${result.accountsImported} account(s).`);
            setSelectedBank(null);
            onConnected();
        } catch (err: any) {
            setError(err.message || 'Failed to connect bank');
        } finally {
            setConnecting(null);
        }
    };

    const getMeta = (bankId: string) =>
        BANK_META[bankId.toLowerCase()] ?? { color: '#1a2332', iconUrl: null, initials: bankId.slice(-3).toUpperCase() };

    const [imgErrors, setImgErrors] = React.useState<Record<string, boolean>>({});

    return (
        <div className="cb-container">
            <div className="cb-header">
                <div className="cb-header-badge">
                    <Landmark size={20} color="#fff" />
                </div>
                <div>
                    <h3 className="cb-title">Connect Your Bank</h3>
                    <p className="cb-subtitle">
                        Securely link your bank accounts using Open Banking to see all your finances in one place.
                    </p>
                </div>
            </div>

            <div className="cb-controls">
                <div className="cb-field">
                    <label className="cb-label">Region</label>
                    <select
                        value={selectedCountry}
                        onChange={(e) => setSelectedCountry(e.target.value)}
                        aria-label="Select country"
                        className="cb-select"
                    >
                        <option value="NZ">NZ — New Zealand</option>
                        <option value="AU">AU — Australia</option>
                        <option value="UK">UK — United Kingdom</option>
                    </select>
                </div>
                <div className="cb-field cb-field--grow">
                    <label className="cb-label">Institution</label>
                    <div className="cb-search-wrap">
                        <Search size={15} className="cb-search-icon" />
                        <input
                            type="text"
                            className="cb-search-input"
                            placeholder="Filter by name..."
                            value={searchQuery}
                            onChange={(e) => setSearchQuery(e.target.value)}
                            aria-label="Search banks"
                        />
                    </div>
                </div>
            </div>

            {error && <div className="cb-alert cb-alert--error">{error}</div>}
            {success && (
                <div className="cb-alert cb-alert--success">
                    <Check size={15} /> {success}
                </div>
            )}

            {loading ? (
                <div className="cb-loading">
                    <div className="spinner" />
                    <span>Loading available banks...</span>
                </div>
            ) : (
                <>
                    <div className="cb-grid">
                        {filteredBanks.map((bank) => {
                            const isSelected = selectedBank?.id === bank.id;
                            const meta = getMeta(bank.id);
                            const iconUrl = meta.iconUrl && !imgErrors[bank.id] ? meta.iconUrl : null;
                            return (
                                <div
                                    key={bank.id}
                                    className={`cb-tile${isSelected ? ' cb-tile--selected' : ''}`}
                                    onClick={() => setSelectedBank(bank)}
                                    tabIndex={0}
                                    onKeyDown={(e) => {
                                        if (e.key === 'Enter' || e.key === ' ') {
                                            e.preventDefault();
                                            setSelectedBank(bank);
                                        }
                                    }}
                                >
                                    <div
                                        className="cb-tile-logo"
                                        style={{ background: meta.color }}
                                        data-initials={meta.initials}
                                    >
                                        {iconUrl && (
                                            <img
                                                src={iconUrl}
                                                alt={`${bank.name} logo`}
                                                className="cb-tile-logo-img"
                                                onError={() => setImgErrors((prev) => ({ ...prev, [bank.id]: true }))}
                                            />
                                        )}
                                    </div>
                                    <div className="cb-tile-info">
                                        <span className="cb-tile-name">{bank.name}</span>
                                        <span className="cb-tile-emoji">{bank.logo}</span>
                                    </div>
                                    {isSelected && (
                                        <span className="cb-tile-check">
                                            <Check size={16} />
                                        </span>
                                    )}
                                    <button
                                        className="cb-tile-connect"
                                        onClick={(e) => {
                                            e.stopPropagation();
                                            handleConnect(bank);
                                        }}
                                        disabled={connecting !== null}
                                        aria-label={`Connect to ${bank.name}`}
                                    >
                                        {connecting === bank.id ? 'Connecting...' : 'Connect'}
                                    </button>
                                </div>
                            );
                        })}
                    </div>
                    {filteredBanks.length === 0 && (
                        <p className="cb-empty">
                            No banks available for this country.
                        </p>
                    )}
                </>
            )}

            {selectedBank && !connecting && !success && (
                <div className="cb-confirmation">
                    <div className="cb-confirmation-inner">
                        {(() => {
                            const meta = getMeta(selectedBank.id);
                            const iconUrl = meta.iconUrl && !imgErrors[selectedBank.id] ? meta.iconUrl : null;
                            return (
                                <div
                                    className="cb-confirmation-logo"
                                    style={{ background: meta.color }}
                                    data-initials={meta.initials}
                                >
                                    {iconUrl && (
                                        <img
                                            src={iconUrl}
                                            alt={`${selectedBank.name} logo`}
                                            className="cb-tile-logo-img"
                                            onError={() => setImgErrors((prev) => ({ ...prev, [selectedBank.id]: true }))}
                                        />
                                    )}
                                </div>
                            );
                        })()}
                        <div className="cb-confirmation-text">
                            <strong>Ready to link {selectedBank.name}</strong>
                            <span>Your accounts will be securely connected via Open Banking</span>
                        </div>
                    </div>
                    <div className="cb-confirmation-actions">
                        <button
                            className="cb-btn-ghost"
                            onClick={() => setSelectedBank(null)}
                        >
                            Cancel
                        </button>
                        <button
                            className="cb-btn-proceed"
                            onClick={() => handleConnect(selectedBank)}
                            disabled={connecting !== null}
                        >
                            <Shield size={14} /> Proceed to Connect
                        </button>
                    </div>
                </div>
            )}

            <div className="cb-trust-badge">
                <div className="cb-trust-lock">
                    <Lock size={13} />
                </div>
                <span className="cb-trust-text">Secured by Open Banking protocols. Your credentials are never stored.</span>
            </div>
        </div>
    );
};

export default ConnectBank;
