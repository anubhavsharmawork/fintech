import * as React from 'react';
import { useToast } from '../components/Toast';
import { useAppContext } from '../context/AppContext';
import type { VirtualCard } from '../services/cardService';
import { listCards, createCard, freezeCard, unfreezeCard, deleteCard } from '../services/cardService';

const formatExpiry = (month: number, year: number): string =>
  `${month.toString().padStart(2, '0')} / ${year}`;

/* ─── Card Network Logo ─── */
const CardNetworkLogo = () => (
  <img
    src="/money-security.svg"
    alt="Fintech Application"
    style={{ height: 28, width: 'auto', filter: 'brightness(0) invert(1)', opacity: 0.75 }}
  />
);

/* ─── VirtualCardVisual — Presentational Component ─── */
const VirtualCardVisual: React.FC<{
  card: VirtualCard;
  cardholderName: string;
}> = ({ card, cardholderName }) => {
  const isFrozen = card.status === 'frozen';
  const [tilt, setTilt] = React.useState({ rotateX: 0, rotateY: 0 });
  const cardRef = React.useRef<HTMLDivElement>(null);

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (isFrozen) return;
    const el = cardRef.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const centerX = rect.width / 2;
    const centerY = rect.height / 2;
    const rotateY = ((x - centerX) / centerX) * 8;
    const rotateX = ((centerY - y) / centerY) * 8;
    setTilt({ rotateX, rotateY });
  };

  const handleMouseLeave = () => {
    setTilt({ rotateX: 0, rotateY: 0 });
  };

  return (
    <div
      ref={cardRef}
      className={`vc-card-visual${isFrozen ? ' vc-card-visual--frozen' : ''}`}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
      style={{
        transform: `perspective(600px) rotateX(${tilt.rotateX}deg) rotateY(${tilt.rotateY}deg)`,
      }}
    >
      {isFrozen && <span className="vc-card-visual__frozen-badge">Frozen</span>}
      <div className="vc-card-visual__top">
        <div className="vc-card-visual__type">Virtual Debit</div>
        <div className="vc-card-visual__chip">
          <svg width="28" height="20" viewBox="0 0 28 20" fill="none">
            <rect x="1" y="1" width="26" height="18" rx="3" stroke="rgba(255,255,255,0.4)" strokeWidth="1"/>
            <line x1="1" y1="7" x2="27" y2="7" stroke="rgba(255,255,255,0.25)" strokeWidth="0.5"/>
            <line x1="1" y1="13" x2="27" y2="13" stroke="rgba(255,255,255,0.25)" strokeWidth="0.5"/>
            <line x1="10" y1="1" x2="10" y2="19" stroke="rgba(255,255,255,0.25)" strokeWidth="0.5"/>
            <line x1="18" y1="1" x2="18" y2="19" stroke="rgba(255,255,255,0.25)" strokeWidth="0.5"/>
          </svg>
        </div>
      </div>
      <div className="vc-card-visual__number">
        {'•••• •••• •••• ' + card.last4}
      </div>
      <div className="vc-card-visual__bottom">
        <div>
          <div className="vc-card-visual__label">EXPIRES</div>
          <div className="vc-card-visual__value">{formatExpiry(card.expiryMonth, card.expiryYear)}</div>
        </div>
        <div>
          <div className="vc-card-visual__label">CARDHOLDER</div>
          <div className="vc-card-visual__value">{cardholderName.toUpperCase()}</div>
        </div>
        <div className="vc-card-visual__network">
          <CardNetworkLogo />
        </div>
      </div>
    </div>
  );
};

const Cards: React.FC = () => {
  const { success, error: toastError } = useToast();
  const { user } = useAppContext();
  const cardholderName = user?.name ?? user?.email ?? '';

  const [cards, setCards] = React.useState<VirtualCard[]>([]);
  const [loading, setLoading] = React.useState(true);

  // Create flow
  const [showCreate, setShowCreate] = React.useState(false);
  const [nickname, setNickname] = React.useState('');
  const [creating, setCreating] = React.useState(false);

  // One-time reveal after creation (full number + CVV)
  const [createReveal, setCreateReveal] = React.useState<{
    nickname: string;
    cardNumber: string;
    cvv: string;
  } | null>(null);

  // Delete confirmation
  const [deleteTarget, setDeleteTarget] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const data = await listCards();
      setCards(data);
    } catch (e: any) {
      toastError(e.message || 'Failed to load cards');
    } finally {
      setLoading(false);
    }
  }, [toastError]);

  React.useEffect(() => { load(); }, [load]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = nickname.trim();
    if (!trimmed) return;
    setCreating(true);
    try {
      const result = await createCard(trimmed);
      setCreateReveal({
        nickname: result.card.nickname,
        cardNumber: result.cardNumber,
        cvv: result.cvv,
      });
      setShowCreate(false);
      setNickname('');
      await load();
      success('Virtual card created');
    } catch (e: any) {
      toastError(e.message || 'Failed to create card');
    } finally {
      setCreating(false);
    }
  };

  const handleFreeze = async (id: string) => {
    try {
      await freezeCard(id);
      await load();
      success('Card frozen');
    } catch (e: any) {
      toastError(e.message || 'Failed to freeze card');
    }
  };

  const handleUnfreeze = async (id: string) => {
    try {
      await unfreezeCard(id);
      await load();
      success('Card unfrozen');
    } catch (e: any) {
      toastError(e.message || 'Failed to unfreeze card');
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteCard(id);
      setDeleteTarget(null);
      await load();
      success('Card deleted');
    } catch (e: any) {
      toastError(e.message || 'Failed to delete card');
    }
  };

  const dismissReveal = () => setCreateReveal(null);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h2 style={{ margin: 0 }}>Virtual Cards</h2>
        <button
          className="btn btn-primary"
          onClick={() => { setShowCreate(!showCreate); setNickname(''); }}
        >
          {showCreate ? 'Cancel' : '+ New Card'}
        </button>
      </div>

      {/* Creation form */}
      {showCreate && (
        <div className="card" style={{ marginBottom: 24 }}>
          <h3 style={{ margin: '0 0 12px 0' }}>Create Virtual Card</h3>
          <form onSubmit={handleCreate}>
            <div className="form-group">
              <label htmlFor="card-nickname">Card Nickname</label>
              <input
                id="card-nickname"
                type="text"
                value={nickname}
                onChange={(e) => setNickname(e.target.value)}
                placeholder="e.g. Online Shopping"
                maxLength={40}
                required
                autoFocus
              />
            </div>
            <button className="btn btn-primary" type="submit" disabled={creating || !nickname.trim()}>
              {creating ? 'Creating…' : 'Create Card'}
            </button>
          </form>
        </div>
      )}

      {/* One-time card details reveal modal */}
      {createReveal && (
        <div className="vc-overlay" onClick={dismissReveal}>
          <div className="card vc-cvv-modal" onClick={(e) => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 4px 0' }}>Card Created</h3>
            <p className="small" style={{ margin: '0 0 16px 0' }}>{createReveal.nickname}</p>

            <div className="vc-cvv-display" style={{ marginBottom: 8 }}>
              <span className="vc-cvv-label">Card Number</span>
              <span className="vc-cvv-value" style={{ fontSize: 14, letterSpacing: 2 }}>
                {createReveal.cardNumber.replace(/(.{4})/g, '$1 ').trim()}
              </span>
            </div>

            <div className="vc-cvv-display">
              <span className="vc-cvv-label">CVV</span>
              <span className="vc-cvv-value">{createReveal.cvv}</span>
            </div>

            <p className="vc-cvv-notice">
              For security, your full card number and CVV are shown once and never stored.
              This mirrors the behaviour of real card issuers.
            </p>

            <button className="btn btn-primary" onClick={dismissReveal} style={{ width: '100%' }}>
              Done
            </button>
          </div>
        </div>
      )}

      {/* Loading */}
      {loading && <p className="small">Loading cards…</p>}

      {/* Empty state */}
      {!loading && cards.length === 0 && !showCreate && (
        <div className="card" style={{ textAlign: 'center', padding: 32 }}>
          <p className="small" style={{ margin: 0 }}>No virtual cards yet. Create one to get started.</p>
        </div>
      )}

      {/* Card list */}
      {!loading && cards.length > 0 && (
        <div className="vc-grid">
          {cards.map((c) => {
            const isFrozen = c.status === 'frozen';
            const isDeleting = deleteTarget === c.id;

            return (
              <div key={c.id} className="vc-container">
                {/* Nickname label */}
                <p className="vc-nickname">{c.nickname}</p>

                {/* Visual card */}
                <VirtualCardVisual card={c} cardholderName={cardholderName} />

                {/* Actions */}
                <div className="vc-actions">
                  {isFrozen ? (
                    <button className="btn btn-primary vc-action-btn" onClick={() => handleUnfreeze(c.id)}>
                      Unfreeze
                    </button>
                  ) : (
                    <button className="btn btn-secondary vc-action-btn" onClick={() => handleFreeze(c.id)}>
                      Freeze
                    </button>
                  )}
                  {!isDeleting ? (
                    <button className="btn btn-secondary vc-action-btn vc-action-delete" onClick={() => setDeleteTarget(c.id)}>
                      Delete
                    </button>
                  ) : (
                    <div className="vc-confirm-row">
                      <span className="small">Are you sure? This cannot be undone.</span>
                      <button className="btn btn-secondary vc-action-btn vc-action-delete" onClick={() => handleDelete(c.id)}>
                        Confirm
                      </button>
                      <button className="btn btn-secondary vc-action-btn" onClick={() => setDeleteTarget(null)}>
                        Cancel
                      </button>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

export default Cards;
