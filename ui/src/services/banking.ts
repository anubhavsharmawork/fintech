// Bank connection service for Open Banking integration

import { apiGet, apiPost, apiDelete, apiRequest, ApiError } from '../api/apiClient';
import { API } from '../config/constants';

export interface AvailableBank {
  id: string;
  name: string;
  logo: string;
  country: string;
}

export interface BankConnection {
  id: string;
  bankId: string;
  bankName: string;
  bankLogo: string;
  status: string;
  connectedAt: string;
}

export interface ExternalBankAccount {
  id: string;
  accountName: string;
  accountType: string;
  accountNumber: string;
  balance: number;
  currency: string;
  lastSyncedAt: string;
  bankName: string;
  bankLogo: string;
}

/**
 * Get list of available banks for connection.
 */
export async function getAvailableBanks(country?: string): Promise<AvailableBank[]> {
  const url = country
    ? `${API.BANK_CONNECTIONS_AVAILABLE}?country=${encodeURIComponent(country)}`
    : API.BANK_CONNECTIONS_AVAILABLE;
  return apiGet<AvailableBank[]>(url);
}

/**
 * Get user's connected banks.
 */
export async function getConnectedBanks(): Promise<BankConnection[]> {
  return apiGet<BankConnection[]>(API.BANK_CONNECTIONS);
}

/**
 * Connect to a bank (initiates mock OAuth flow).
 */
export async function connectBank(bankId: string): Promise<{ connectionId: string; accountsImported: number }> {
  const res = await apiRequest(API.BANK_CONNECTIONS_CONNECT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ bankId }),
  });
  if (res.status === 409) throw new Error('Bank already connected');
  if (!res.ok) throw new ApiError(res.status, `Failed to connect bank (${res.status})`);
  return res.json();
}

/**
 * Disconnect a bank.
 */
export async function disconnectBank(connectionId: string): Promise<void> {
  return apiDelete(API.BANK_CONNECTION(connectionId));
}

/**
 * Get all external bank accounts.
 */
export async function getExternalAccounts(): Promise<ExternalBankAccount[]> {
  return apiGet<ExternalBankAccount[]>(API.BANK_CONNECTIONS_ACCOUNTS);
}

/**
 * Sync accounts for a bank connection (refresh balances).
 */
export async function syncBankAccounts(connectionId: string): Promise<{ syncedAt: string }> {
  return apiPost<{ syncedAt: string }>(API.BANK_CONNECTION_SYNC(connectionId), {});
}

export interface DepositFromExternalResponse {
  depositId: string;
  accountId: string;
  amount: number;
  currency: string;
  status: string;
}

/**
 * Deposit funds from an external bank account into an internal account.
 */
export async function depositFromExternal(
  accountId: string,
  externalBankAccountId: string,
  amount: number
): Promise<DepositFromExternalResponse> {
  return apiPost<DepositFromExternalResponse>(
    API.ACCOUNT_DEPOSIT_EXTERNAL(accountId),
    { externalBankAccountId, amount },
  );
}
