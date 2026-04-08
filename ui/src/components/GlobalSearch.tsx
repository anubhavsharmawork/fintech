// @ts-nocheck
import * as React from 'react';
import { useInRouterContext, useNavigate } from 'react-router-dom';
import { GlobalSearchState, SearchResultItem } from '../hooks/useGlobalSearch';

interface GlobalSearchProps {
  search: GlobalSearchState;
}

/* Highlight matching text segments in bold */
function HighlightMatch({ text, query }: { text: string; query: string }) {
  if (!query.trim()) return <>{text}</>;
  const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const parts = text.split(new RegExp(`(${escaped})`, 'gi'));
  return (
    <>
      {parts.map((part, i) =>
        part.toLowerCase() === query.toLowerCase()
          ? <strong key={i} className="gs-highlight">{part}</strong>
          : <span key={i}>{part}</span>
      )}
    </>
  );
}

/* Mask account number, showing only last 4 digits */
function maskAccountNumber(num: string): string {
  if (num.length <= 4) return num;
  return '••••\u2009' + num.slice(-4);
}

/* Format currency amount */
function formatCurrency(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-NZ', { style: 'currency', currency: currency || 'NZD' }).format(amount);
  } catch {
    return `$${amount.toFixed(2)}`;
  }
}

/* Human-readable date */
function formatDate(dateStr: string): string {
  try {
    const d = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - d.getTime();
    const diffDays = Math.floor(diffMs / 86400000);
    if (diffDays === 0) return 'Today';
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 7) return `${diffDays} days ago`;
    return d.toLocaleDateString('en-NZ', { day: 'numeric', month: 'short', year: 'numeric' });
  } catch {
    return dateStr;
  }
}

/* Skeleton loading rows */
function SkeletonGroup() {
  return (
    <div className="gs-skeleton-group">
      <div className="gs-skeleton-heading" />
      {[0, 1, 2].map(i => (
        <div key={i} className="gs-skeleton-row">
          <div className="gs-skeleton-avatar" />
          <div className="gs-skeleton-lines">
            <div className="gs-skeleton-line gs-skeleton-line--long" />
            <div className="gs-skeleton-line gs-skeleton-line--short" />
          </div>
        </div>
      ))}
    </div>
  );
}

/* Empty state */
function EmptyState({ query }: { query: string }) {
  return (
    <div className="gs-empty">
      <div className="gs-empty-icon" aria-hidden="true">
        <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
          <circle cx="22" cy="22" r="14" stroke="var(--border)" strokeWidth="3" />
          <line x1="32.5" y1="32.5" x2="42" y2="42" stroke="var(--border)" strokeWidth="3" strokeLinecap="round" />
        </svg>
      </div>
      <p className="gs-empty-title">No results for &ldquo;{query}&rdquo;</p>
      <p className="gs-empty-hint">Try searching by account name, payee, or transaction amount</p>
    </div>
  );
}

const GlobalSearch: React.FC<GlobalSearchProps> = ({ search }) => {
  const navigate = useNavigate();
  const inputRef = React.useRef<HTMLInputElement>(null);
  const listRef = React.useRef<HTMLDivElement>(null);

  const {
    isOpen,
    query,
    loading,
    accounts,
    transactions,
    payees,
    activeIndex,
    flatItems,
    close,
    setQuery,
    setActiveIndex,
    onKeyDown,
    getNavigationPath,
  } = search;

  // Auto-focus input when modal opens
  React.useEffect(() => {
    if (isOpen) {
      requestAnimationFrame(() => inputRef.current?.focus());
    }
  }, [isOpen]);

  // Scroll active item into view
  React.useEffect(() => {
    if (activeIndex < 0 || !listRef.current) return;
    const el = listRef.current.querySelector(`[data-gs-index="${activeIndex}"]`);
    if (el) el.scrollIntoView({ block: 'nearest' });
  }, [activeIndex]);

  const handleItemClick = (item: SearchResultItem) => {
    close();
    navigate(getNavigationPath(item));
  };

  if (!isOpen) return null;

  const hasQuery = query.trim().length > 0;
  const hasResults = accounts.length > 0 || transactions.length > 0 || payees.length > 0;
  const showEmpty = hasQuery && !loading && !hasResults;

  /* Compute the flat-index offset for each group */
  let accountOffset = 0;
  let transactionOffset = accounts.length;
  let payeeOffset = accounts.length + transactions.length;

  return (
    <>
      <div className="gs-overlay" onClick={close} aria-hidden="true" />
      <div
        className="gs-modal"
        role="dialog"
        aria-modal="true"
        aria-label="Global search"
        onKeyDown={onKeyDown}
      >
        {/* Search input */}
        <div className="gs-input-wrap">
          <svg className="gs-input-icon" width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
            <circle cx="7.5" cy="7.5" r="5.5" stroke="currentColor" strokeWidth="1.5" />
            <line x1="11.5" y1="11.5" x2="16" y2="16" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
          </svg>
          <input
            ref={inputRef}
            className="gs-input"
            type="text"
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Search accounts, transactions, payees…"
            aria-label="Search"
            autoComplete="off"
            spellCheck={false}
          />
          <kbd className="gs-kbd" aria-hidden="true">ESC</kbd>
        </div>

        {/* Results area */}
        <div className="gs-results" ref={listRef}>
          {loading && (
            <>
              <SkeletonGroup />
              <SkeletonGroup />
            </>
          )}

          {!loading && hasResults && (
            <>
              {accounts.length > 0 && (
                <div className="gs-group" role="group" aria-label="Accounts">
                  <div className="gs-group-label">Accounts</div>
                  {accounts.map((a, i) => {
                    const idx = accountOffset + i;
                    return (
                      <div
                        key={a.id}
                        className={`gs-item${idx === activeIndex ? ' gs-item--active' : ''}`}
                        data-gs-index={idx}
                        onMouseEnter={() => setActiveIndex(idx)}
                        onClick={() => handleItemClick({ kind: 'account', data: a })}
                        role="option"
                        aria-selected={idx === activeIndex}
                      >
                        <div className="gs-item-icon gs-item-icon--account" aria-hidden="true">
                          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                            <rect x="1.5" y="3.5" width="13" height="9" rx="2" stroke="currentColor" strokeWidth="1.2" />
                            <line x1="1.5" y1="6.5" x2="14.5" y2="6.5" stroke="currentColor" strokeWidth="1.2" />
                          </svg>
                        </div>
                        <div className="gs-item-body">
                          <span className="gs-item-primary">
                            <HighlightMatch text={`${a.accountType} Account`} query={query} />
                          </span>
                          <span className="gs-item-secondary">
                            {maskAccountNumber(a.accountNumber)}
                            <span className="gs-item-dot" aria-hidden="true" />
                            {formatCurrency(a.balance, a.currency)}
                          </span>
                        </div>
                        <svg className="gs-item-arrow" width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true"><path d="M5 3l4 4-4 4" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round"/></svg>
                      </div>
                    );
                  })}
                </div>
              )}

              {transactions.length > 0 && (
                <div className="gs-group" role="group" aria-label="Transactions">
                  <div className="gs-group-label">Transactions</div>
                  {transactions.map((t, i) => {
                    const idx = transactionOffset + i;
                    return (
                      <div
                        key={t.id}
                        className={`gs-item${idx === activeIndex ? ' gs-item--active' : ''}`}
                        data-gs-index={idx}
                        onMouseEnter={() => setActiveIndex(idx)}
                        onClick={() => handleItemClick({ kind: 'transaction', data: t })}
                        role="option"
                        aria-selected={idx === activeIndex}
                      >
                        <div className={`gs-item-icon gs-item-icon--tx ${t.type === 'credit' ? 'gs-item-icon--credit' : 'gs-item-icon--debit'}`} aria-hidden="true">
                          {t.type === 'credit' ? '↓' : '↑'}
                        </div>
                        <div className="gs-item-body">
                          <span className="gs-item-primary">
                            <HighlightMatch text={t.description || 'Transaction'} query={query} />
                          </span>
                          <span className="gs-item-secondary">
                            <span className={t.type === 'credit' ? 'gs-amount--credit' : 'gs-amount--debit'}>
                              {t.type === 'credit' ? '+' : '−'}{formatCurrency(Math.abs(t.amount), t.currency)}
                            </span>
                            <span className="gs-item-dot" aria-hidden="true" />
                            {formatDate(t.createdAt)}
                          </span>
                        </div>
                        <svg className="gs-item-arrow" width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true"><path d="M5 3l4 4-4 4" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round"/></svg>
                      </div>
                    );
                  })}
                </div>
              )}

              {payees.length > 0 && (
                <div className="gs-group" role="group" aria-label="Payees">
                  <div className="gs-group-label">Payees</div>
                  {payees.map((p, i) => {
                    const idx = payeeOffset + i;
                    const initials = p.name
                      .split(/\s+/)
                      .slice(0, 2)
                      .map(w => w.charAt(0).toUpperCase())
                      .join('');
                    return (
                      <div
                        key={p.id}
                        className={`gs-item${idx === activeIndex ? ' gs-item--active' : ''}`}
                        data-gs-index={idx}
                        onMouseEnter={() => setActiveIndex(idx)}
                        onClick={() => handleItemClick({ kind: 'payee', data: p })}
                        role="option"
                        aria-selected={idx === activeIndex}
                      >
                        <div className="gs-avatar" aria-hidden="true">{initials}</div>
                        <div className="gs-item-body">
                          <span className="gs-item-primary">
                            <HighlightMatch text={p.name} query={query} />
                          </span>
                          <span className="gs-item-secondary">
                            {maskAccountNumber(p.accountNumber)}
                          </span>
                        </div>
                        <svg className="gs-item-arrow" width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true"><path d="M5 3l4 4-4 4" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round"/></svg>
                      </div>
                    );
                  })}
                </div>
              )}
            </>
          )}

          {showEmpty && <EmptyState query={query} />}

          {!hasQuery && !loading && (
            <div className="gs-hint">
              <p className="gs-hint-text">Start typing to search across your accounts, recent transactions, and saved payees.</p>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="gs-footer">
          <span className="gs-footer-hint">
            <kbd className="gs-kbd-sm">↑</kbd>
            <kbd className="gs-kbd-sm">↓</kbd>
            <span>navigate</span>
          </span>
          <span className="gs-footer-hint">
            <kbd className="gs-kbd-sm">↵</kbd>
            <span>open</span>
          </span>
          <span className="gs-footer-hint">
            <kbd className="gs-kbd-sm">esc</kbd>
            <span>close</span>
          </span>
        </div>
      </div>
    </>
  );
};

export default GlobalSearch;
