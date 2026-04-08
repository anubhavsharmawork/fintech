// @ts-nocheck
// Global search service — searches accounts, transactions and payees in parallel

import { apiGet } from '../api/apiClient';
import { API } from '../config/constants';

export interface SearchAccount {
  id: string;
  accountNumber: string;
  accountType: string;
  balance: number;
  currency: string;
}

export interface SearchTransaction {
  id: string;
  accountId: string;
  amount: number;
  currency: string;
  type: 'credit' | 'debit';
  description: string;
  createdAt: string;
}

export interface SearchPayee {
  id: string;
  name: string;
  accountNumber: string;
}

export interface SearchResults {
  accounts: SearchAccount[];
  transactions: SearchTransaction[];
  payees: SearchPayee[];
}

export interface LoadSearchDataResult extends SearchResults {
  message: string;
}

type PayeeSource = Partial<SearchPayee> & {
  displayName?: string;
  normalizedName?: string;
  frequency?: number;
  count?: number;
};

function unwrapCollection<T>(value: unknown): T[] {
  if (Array.isArray(value)) return value as T[];
  if (value && typeof value === 'object') {
    const record = value as Record<string, unknown>;
    for (const key of ['items', 'results', 'data', 'value', 'records']) {
      if (Array.isArray(record[key])) return record[key] as T[];
    }
  }
  return [];
}

function normaliseQuery(query: string): string {
  return query.trim().toLowerCase();
}

function hasMatch(value: string | number | null | undefined, query: string): boolean {
  if (value === null || value === undefined) return false;
  return String(value).toLowerCase().includes(query);
}

export function mapPayees(payees: PayeeSource[] = []): SearchPayee[] {
  const grouped = new Map<string, { payee: SearchPayee; frequency: number; order: number }>();

  payees.forEach((entry, index) => {
    const rawName = (entry.name ?? entry.displayName ?? '').trim();
    if (!rawName) return;
    const normalized = (entry.normalizedName ?? rawName).trim().toLowerCase().replace(/\s+/g, ' ');
    const accountNumber = String(entry.accountNumber ?? '').trim();
    const frequency = Number(entry.frequency ?? entry.count ?? 1) || 1;

    const existing = grouped.get(normalized);
    if (existing) {
      existing.frequency += frequency;
      return;
    }

    grouped.set(normalized, {
      payee: {
        id: String(entry.id ?? normalized),
        name: rawName,
        accountNumber,
      },
      frequency,
      order: index,
    });
  });

  return Array.from(grouped.values())
    .sort((a, b) => b.frequency - a.frequency || a.payee.name.localeCompare(b.payee.name))
    .slice(0, 5)
    .map(entry => entry.payee);
}

export async function loadSearchData(query: string, signal?: AbortSignal): Promise<LoadSearchDataResult> {
  const trimmed = query.trim();
  try {
    const [accounts, transactions, payees] = await Promise.all([
      searchAccounts(trimmed, signal),
      searchTransactions(trimmed, signal),
      searchPayees(trimmed, signal),
    ]);

    const hasResults = accounts.length > 0 || transactions.length > 0 || payees.length > 0;
    return {
      accounts,
      transactions,
      payees,
      message: hasResults ? '' : 'No results found',
    };
  } catch (error: any) {
    const message = error?.name === 'AbortError' || /timeout/i.test(error?.message || '')
      ? 'Request timed out'
      : 'Failed to load search data';
    return { accounts: [], transactions: [], payees: [], message };
  }
}

export async function searchAccounts(query: string, signal?: AbortSignal): Promise<SearchAccount[]> {
  const data = unwrapCollection<SearchAccount>(await apiGet<SearchAccount[] | { items?: SearchAccount[] }>(API.SEARCH_ACCOUNTS, { signal }));
  const q = normaliseQuery(query);
  return data
    .filter(a =>
      hasMatch(a.accountNumber, q) ||
      hasMatch(a.accountType, q) ||
      hasMatch(a.balance, q)
    )
    .slice(0, 5);
}

export async function searchTransactions(query: string, signal?: AbortSignal): Promise<SearchTransaction[]> {
  const data = unwrapCollection<SearchTransaction>(await apiGet<SearchTransaction[] | { items?: SearchTransaction[] }>(API.SEARCH_TRANSACTIONS, { signal }));
  const q = normaliseQuery(query);
  return data
    .filter(t =>
      hasMatch(t.description, q) ||
      hasMatch(t.amount, q) ||
      hasMatch(t.type, q)
    )
    .slice(0, 5);
}

export async function searchPayees(query: string, signal?: AbortSignal): Promise<SearchPayee[]> {
  const data = unwrapCollection<PayeeSource>(await apiGet<SearchPayee[] | { items?: SearchPayee[] }>(API.SEARCH_PAYEES, { signal }));
  const q = normaliseQuery(query);
  return mapPayees(data.filter(p =>
    hasMatch(p.name ?? p.displayName, q) ||
    hasMatch(p.accountNumber, q)
  ));
}
