import {
  getAvailableBanks,
  getConnectedBanks,
  connectBank,
  disconnectBank,
  getExternalAccounts,
  syncBankAccounts,
  AvailableBank,
  BankConnection,
  ExternalBankAccount
} from './banking';

describe('Banking Service', () => {
  const mockToken = 'test-jwt-token';

  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    localStorage.setItem('token', mockToken);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  const mockAvailableBanks: AvailableBank[] = [
    { id: 'bank1', name: 'ANZ', logo: 'anz.png', country: 'NZ' },
    { id: 'bank2', name: 'ASB', logo: 'asb.png', country: 'NZ' },
  ];

  const mockConnections: BankConnection[] = [
    {
      id: 'conn1',
      bankId: 'bank1',
      bankName: 'ANZ',
      bankLogo: 'anz.png',
      status: 'active',
      connectedAt: '2024-01-15T10:00:00Z'
    }
  ];

  const mockExternalAccounts: ExternalBankAccount[] = [
    {
      id: 'ext1',
      accountName: 'Savings',
      accountType: 'savings',
      accountNumber: '12-3456-7890123-00',
      balance: 5000,
      currency: 'NZD',
      lastSyncedAt: '2024-01-15T10:00:00Z',
      bankName: 'ANZ',
      bankLogo: 'anz.png'
    }
  ];

  describe('getAvailableBanks', () => {
    it('should fetch available banks without country filter', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => mockAvailableBanks
      });

      const result = await getAvailableBanks();

      expect(fetch).toHaveBeenCalledWith('/bankconnections/available', {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${mockToken}`
        }
      });
      expect(result).toEqual(mockAvailableBanks);
    });

    it('should fetch available banks with country filter', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => mockAvailableBanks
      });

      const result = await getAvailableBanks('NZ');

      expect(fetch).toHaveBeenCalledWith('/bankconnections/available?country=NZ', expect.any(Object));
      expect(result).toEqual(mockAvailableBanks);
    });

    it('should throw error when fetch fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 500
      });

      await expect(getAvailableBanks()).rejects.toThrow('Failed to fetch available banks (500)');
    });

    it('should include empty auth header when no token', async () => {
      localStorage.removeItem('token');
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => []
      });

      await getAvailableBanks();

      expect(fetch).toHaveBeenCalledWith('/bankconnections/available', {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': ''
        }
      });
    });
  });

  describe('getConnectedBanks', () => {
    it('should fetch connected banks', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => mockConnections
      });

      const result = await getConnectedBanks();

      expect(fetch).toHaveBeenCalledWith('/bankconnections', {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${mockToken}`
        }
      });
      expect(result).toEqual(mockConnections);
    });

    it('should throw error when fetch fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 401
      });

      await expect(getConnectedBanks()).rejects.toThrow('Failed to fetch connected banks (401)');
    });
  });

  describe('connectBank', () => {
    it('should connect to a bank successfully', async () => {
      const mockResponse = { connectionId: 'conn123', accountsImported: 3 };
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => mockResponse
      });

      const result = await connectBank('bank1');

      expect(fetch).toHaveBeenCalledWith('/bankconnections/connect', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${mockToken}`
        },
        body: JSON.stringify({ bankId: 'bank1' })
      });
      expect(result).toEqual(mockResponse);
    });

    it('should throw specific error when bank already connected (409)', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 409
      });

      await expect(connectBank('bank1')).rejects.toThrow('Bank already connected');
    });

    it('should throw generic error for other failures', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 500
      });

      await expect(connectBank('bank1')).rejects.toThrow('Failed to connect bank (500)');
    });
  });

  describe('disconnectBank', () => {
    it('should disconnect a bank successfully', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true
      });

      await disconnectBank('conn123');

      expect(fetch).toHaveBeenCalledWith('/bankconnections/conn123', {
        method: 'DELETE',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${mockToken}`
        }
      });
    });

    it('should throw error when disconnect fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 404
      });

      await expect(disconnectBank('conn123')).rejects.toThrow('Failed to disconnect bank (404)');
    });
  });

  describe('getExternalAccounts', () => {
    it('should fetch external accounts', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => mockExternalAccounts
      });

      const result = await getExternalAccounts();

      expect(fetch).toHaveBeenCalledWith('/bankconnections/accounts', {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${mockToken}`
        }
      });
      expect(result).toEqual(mockExternalAccounts);
    });

    it('should throw error when fetch fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 500
      });

      await expect(getExternalAccounts()).rejects.toThrow('Failed to fetch external accounts (500)');
    });
  });

  describe('syncBankAccounts', () => {
    it('should sync accounts successfully', async () => {
      const mockResponse = { syncedAt: '2024-01-15T12:00:00Z' };
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => mockResponse
      });

      const result = await syncBankAccounts('conn123');

      expect(fetch).toHaveBeenCalledWith('/bankconnections/conn123/sync', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${mockToken}`
        }
      });
      expect(result).toEqual(mockResponse);
    });

    it('should throw error when sync fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 500
      });

      await expect(syncBankAccounts('conn123')).rejects.toThrow('Failed to sync accounts (500)');
    });
  });
});
