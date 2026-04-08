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
  // Valid JWT with far-future expiry to avoid refresh attempts
  const mockToken = 'header.eyJleHAiOjk5OTk5OTk5OTl9.sig';

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

      expect(fetch).toHaveBeenCalledWith(
        '/bankconnections/available',
        expect.objectContaining({
          credentials: 'include'
        })
      );
      expect(result).toEqual(mockAvailableBanks);
    });

    it('should fetch available banks with country filter', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => mockAvailableBanks
      });

      const result = await getAvailableBanks('NZ');

      expect(fetch).toHaveBeenCalledWith(
        '/bankconnections/available?country=NZ',
        expect.any(Object)
      );
      expect(result).toEqual(mockAvailableBanks);
    });

    it('should throw error when fetch fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Headers({ 'content-type': 'text/plain' }),
        text: async () => 'Internal Server Error'
      });

      await expect(getAvailableBanks()).rejects.toThrow('Internal Server Error');
    });

    it('should work without auth token', async () => {
      localStorage.removeItem('token');
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => []
      });

      const result = await getAvailableBanks();

      expect(fetch).toHaveBeenCalled();
      expect(result).toEqual([]);
    });
  });

  describe('getConnectedBanks', () => {
    it('should fetch connected banks', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => mockConnections
      });

      const result = await getConnectedBanks();

      expect(fetch).toHaveBeenCalledWith(
        '/bankconnections',
        expect.objectContaining({
          credentials: 'include'
        })
      );
      expect(result).toEqual(mockConnections);
    });

    it('should throw error when fetch fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 401,
        statusText: 'Unauthorized',
        headers: new Headers({ 'content-type': 'text/plain' }),
        text: async () => 'Unauthorized'
      });

      await expect(getConnectedBanks()).rejects.toThrow('Unauthorized');
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

      expect(fetch).toHaveBeenCalledWith(
        '/bankconnections/connect',
        expect.objectContaining({
          method: 'POST',
          credentials: 'include'
        })
      );
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
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Headers({ 'content-type': 'text/plain' }),
        text: async () => 'Internal Server Error'
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

      expect(fetch).toHaveBeenCalledWith(
        '/bankconnections/conn123',
        expect.objectContaining({
          method: 'DELETE',
          credentials: 'include'
        })
      );
    });

    it('should throw error when disconnect fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 404,
        statusText: 'Not Found',
        headers: new Headers({ 'content-type': 'text/plain' }),
        text: async () => 'Not Found'
      });

      await expect(disconnectBank('conn123')).rejects.toThrow('Not Found');
    });
  });

  describe('getExternalAccounts', () => {
    it('should fetch external accounts', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: true,
        json: async () => mockExternalAccounts
      });

      const result = await getExternalAccounts();

      expect(fetch).toHaveBeenCalledWith(
        '/bankconnections/accounts',
        expect.objectContaining({
          credentials: 'include'
        })
      );
      expect(result).toEqual(mockExternalAccounts);
    });

    it('should throw error when fetch fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Headers({ 'content-type': 'text/plain' }),
        text: async () => 'Internal Server Error'
      });

      await expect(getExternalAccounts()).rejects.toThrow('Internal Server Error');
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

      expect(fetch).toHaveBeenCalledWith(
        '/bankconnections/conn123/sync',
        expect.objectContaining({
          method: 'POST',
          credentials: 'include'
        })
      );
      expect(result).toEqual(mockResponse);
    });

    it('should throw error when sync fails', async () => {
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Headers({ 'content-type': 'text/plain' }),
        text: async () => 'Internal Server Error'
      });

      await expect(syncBankAccounts('conn123')).rejects.toThrow('Internal Server Error');
    });
  });
});
