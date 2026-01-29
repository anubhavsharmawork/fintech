// Bank connection service for Open Banking integration

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

const getAuthHeader = (): HeadersInit => {
  const token = localStorage.getItem('token');
  return {
    'Content-Type': 'application/json',
    'Authorization': token ? `Bearer ${token}` : ''
  };
};

/**
 * Get list of available banks for connection.
 */
export async function getAvailableBanks(country?: string): Promise<AvailableBank[]> {
  const url = country 
    ? `/bankconnections/available?country=${encodeURIComponent(country)}`
    : '/bankconnections/available';

  const res = await fetch(url, { headers: getAuthHeader() });
  if (!res.ok) throw new Error(`Failed to fetch available banks (${res.status})`);
  return res.json();
}

/**
 * Get user's connected banks.
 */
export async function getConnectedBanks(): Promise<BankConnection[]> {
  const res = await fetch('/bankconnections', { headers: getAuthHeader() });
  if (!res.ok) throw new Error(`Failed to fetch connected banks (${res.status})`);
  return res.json();
}

/**
 * Connect to a bank (initiates mock OAuth flow).
 */
export async function connectBank(bankId: string): Promise<{ connectionId: string; accountsImported: number }> {
  const res = await fetch('/bankconnections/connect', {
    method: 'POST',
    headers: getAuthHeader(),
    body: JSON.stringify({ bankId })
  });

  if (res.status === 409) {
    throw new Error('Bank already connected');
  }
  if (!res.ok) throw new Error(`Failed to connect bank (${res.status})`);
  return res.json();
}

/**
 * Disconnect a bank.
 */
export async function disconnectBank(connectionId: string): Promise<void> {
  const res = await fetch(`/bankconnections/${connectionId}`, {
    method: 'DELETE',
    headers: getAuthHeader()
  });
  if (!res.ok) throw new Error(`Failed to disconnect bank (${res.status})`);
}

/**
 * Get all external bank accounts.
 */
export async function getExternalAccounts(): Promise<ExternalBankAccount[]> {
  const res = await fetch('/bankconnections/accounts', { headers: getAuthHeader() });
  if (!res.ok) throw new Error(`Failed to fetch external accounts (${res.status})`);
  return res.json();
}

/**
 * Sync accounts for a bank connection (refresh balances).
 */
export async function syncBankAccounts(connectionId: string): Promise<{ syncedAt: string }> {
  const res = await fetch(`/bankconnections/${connectionId}/sync`, {
    method: 'POST',
    headers: getAuthHeader()
  });
  if (!res.ok) throw new Error(`Failed to sync accounts (${res.status})`);
  return res.json();
}
