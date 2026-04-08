// @ts-nocheck
import * as React from 'react';
import {
  searchAccounts,
  searchTransactions,
  searchPayees,
  SearchAccount,
  SearchTransaction,
  SearchPayee,
} from '../services/search';

export type SearchResultItem =
  | { kind: 'account'; data: SearchAccount }
  | { kind: 'transaction'; data: SearchTransaction }
  | { kind: 'payee'; data: SearchPayee };

export interface GlobalSearchState {
  isOpen: boolean;
  query: string;
  loading: boolean;
  accounts: SearchAccount[];
  transactions: SearchTransaction[];
  payees: SearchPayee[];
  activeIndex: number;
  flatItems: SearchResultItem[];
  open: () => void;
  close: () => void;
  setQuery: (q: string) => void;
  setActiveIndex: (i: number) => void;
  onKeyDown: (e: React.KeyboardEvent) => void;
  getNavigationPath: (item: SearchResultItem) => string;
}

export function useGlobalSearch(navigate: (path: string) => void): GlobalSearchState {
  const [isOpen, setIsOpen] = React.useState(false);
  const [query, setQuery] = React.useState('');
  const [loading, setLoading] = React.useState(false);
  const [accounts, setAccounts] = React.useState<SearchAccount[]>([]);
  const [transactions, setTransactions] = React.useState<SearchTransaction[]>([]);
  const [payees, setPayees] = React.useState<SearchPayee[]>([]);
  const [activeIndex, setActiveIndex] = React.useState(-1);
  const abortRef = React.useRef<AbortController | null>(null);
  const debounceRef = React.useRef<ReturnType<typeof setTimeout> | null>(null);

  const open = React.useCallback(() => {
    setIsOpen(true);
    setQuery('');
    setAccounts([]);
    setTransactions([]);
    setPayees([]);
    setActiveIndex(-1);
    setLoading(false);
  }, []);

  const close = React.useCallback(() => {
    setIsOpen(false);
    if (abortRef.current) abortRef.current.abort();
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, []);

  // Global keyboard shortcut
  React.useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        setIsOpen(prev => {
          if (prev) {
            if (abortRef.current) abortRef.current.abort();
            if (debounceRef.current) clearTimeout(debounceRef.current);
            return false;
          }
          setQuery('');
          setAccounts([]);
          setTransactions([]);
          setPayees([]);
          setActiveIndex(-1);
          setLoading(false);
          return true;
        });
      }
      if (e.key === 'Escape' && isOpen) {
        close();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [isOpen, close]);

  // Debounced search
  React.useEffect(() => {
    if (!isOpen) return;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (abortRef.current) abortRef.current.abort();

    const trimmed = query.trim();
    if (!trimmed) {
      setAccounts([]);
      setTransactions([]);
      setPayees([]);
      setLoading(false);
      setActiveIndex(-1);
      return;
    }

    setLoading(true);
    debounceRef.current = setTimeout(() => {
      const controller = new AbortController();
      abortRef.current = controller;

      Promise.allSettled([
        searchAccounts(trimmed, controller.signal),
        searchTransactions(trimmed, controller.signal),
        searchPayees(trimmed, controller.signal),
      ]).then(([aResult, tResult, pResult]) => {
        if (controller.signal.aborted) return;
        setAccounts(aResult.status === 'fulfilled' ? aResult.value : []);
        setTransactions(tResult.status === 'fulfilled' ? tResult.value : []);
        setPayees(pResult.status === 'fulfilled' ? pResult.value : []);
        setActiveIndex(-1);
        setLoading(false);
      });
    }, 300);

    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [query, isOpen]);

  const flatItems = React.useMemo<SearchResultItem[]>(() => {
    const items: SearchResultItem[] = [];
    accounts.forEach(a => items.push({ kind: 'account', data: a }));
    transactions.forEach(t => items.push({ kind: 'transaction', data: t }));
    payees.forEach(p => items.push({ kind: 'payee', data: p }));
    return items;
  }, [accounts, transactions, payees]);

  const getNavigationPath = React.useCallback((item: SearchResultItem): string => {
    switch (item.kind) {
      case 'account': return '/accounts';
      case 'transaction': return '/transactions';
      case 'payee': return '/transactions';
    }
  }, []);

  const onKeyDown = React.useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActiveIndex(prev => (prev < flatItems.length - 1 ? prev + 1 : 0));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIndex(prev => (prev > 0 ? prev - 1 : flatItems.length - 1));
    } else if (e.key === 'Enter' && activeIndex >= 0 && activeIndex < flatItems.length) {
      e.preventDefault();
      const item = flatItems[activeIndex];
      close();
      navigate(getNavigationPath(item));
    }
  }, [flatItems, activeIndex, close, navigate, getNavigationPath]);

  return {
    isOpen,
    query,
    loading,
    accounts,
    transactions,
    payees,
    activeIndex,
    flatItems,
    open,
    close,
    setQuery,
    setActiveIndex,
    onKeyDown,
    getNavigationPath,
  };
}
