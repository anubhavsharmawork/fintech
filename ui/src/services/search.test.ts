
import * as search from './search';
import * as apiClient from '../api/apiClient';
import { API } from '../config/constants';

jest.mock('../api/apiClient', () => ({
  ...jest.requireActual('../api/apiClient'),
  apiGet: jest.fn(),
}));

const mockApiGet = apiClient.apiGet as jest.Mock;

describe('search service', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('searchAccounts', () => {
    it('should filter accounts by query', async () => {
      const mockAccounts = [
        { id: 'a1', accountNumber: '1234567890', accountType: 'checking', balance: 5000, currency: 'USD' },
        { id: 'a2', accountNumber: '0987654321', accountType: 'savings', balance: 10000, currency: 'EUR' }
      ];
      mockApiGet.mockResolvedValue(mockAccounts);

      const result = await search.searchAccounts('1234');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_ACCOUNTS, { signal: undefined });
      expect(result).toHaveLength(1);
      expect(result[0].accountNumber).toBe('1234567890');
    });

    it('should filter by account type', async () => {
      const mockAccounts = [
        { id: 'a1', accountNumber: '1111', accountType: 'checking', balance: 5000, currency: 'USD' },
        { id: 'a2', accountNumber: '2222', accountType: 'savings', balance: 10000, currency: 'EUR' }
      ];
      mockApiGet.mockResolvedValue(mockAccounts);

      const result = await search.searchAccounts('savings');

      expect(result).toHaveLength(1);
      expect(result[0].accountType).toBe('savings');
    });

    it('should filter by balance string match', async () => {
      const mockAccounts = [
        { id: 'a1', accountNumber: '1111', accountType: 'checking', balance: 5000, currency: 'USD' },
        { id: 'a2', accountNumber: '2222', accountType: 'savings', balance: 9999, currency: 'EUR' }
      ];
      mockApiGet.mockResolvedValue(mockAccounts);

      const result = await search.searchAccounts('9999');

      expect(result).toHaveLength(1);
      expect(result[0].balance).toBe(9999);
    });

    it('should limit results to 5', async () => {
      const mockAccounts = Array.from({ length: 10 }, (_, i) => ({
        id: `a${i}`,
        accountNumber: `1234${i}`,
        accountType: 'checking',
        balance: 1000,
        currency: 'USD'
      }));
      mockApiGet.mockResolvedValue(mockAccounts);

      const result = await search.searchAccounts('1234');

      expect(result).toHaveLength(5);
    });

    it('should support abort signal', async () => {
      const controller = new AbortController();
      mockApiGet.mockResolvedValue([]);

      await search.searchAccounts('test', controller.signal);

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_ACCOUNTS, { signal: controller.signal });
    });

    it('should handle API error', async () => {
      mockApiGet.mockRejectedValue(new apiClient.ApiError(500, 'Server error'));

      await expect(search.searchAccounts('test')).rejects.toThrow('Server error');
    });
  });

  describe('searchTransactions', () => {
    it('should filter transactions by description', async () => {
      const mockTransactions = [
        { id: 't1', accountId: 'a1', amount: 100, currency: 'USD', type: 'credit' as const, description: 'Salary payment', createdAt: '2024-01-01' },
        { id: 't2', accountId: 'a1', amount: 50, currency: 'USD', type: 'debit' as const, description: 'Coffee shop', createdAt: '2024-01-02' }
      ];
      mockApiGet.mockResolvedValue(mockTransactions);

      const result = await search.searchTransactions('salary');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_TRANSACTIONS, { signal: undefined });
      expect(result).toHaveLength(1);
      expect(result[0].description).toBe('Salary payment');
    });

    it('should filter by amount', async () => {
      const mockTransactions = [
        { id: 't1', accountId: 'a1', amount: 100, currency: 'USD', type: 'credit' as const, description: 'Payment', createdAt: '2024-01-01' },
        { id: 't2', accountId: 'a1', amount: 50, currency: 'USD', type: 'debit' as const, description: 'Purchase', createdAt: '2024-01-02' }
      ];
      mockApiGet.mockResolvedValue(mockTransactions);

      const result = await search.searchTransactions('50');

      expect(result).toHaveLength(1);
      expect(result[0].amount).toBe(50);
    });

    it('should filter by type', async () => {
      const mockTransactions = [
        { id: 't1', accountId: 'a1', amount: 100, currency: 'USD', type: 'credit' as const, description: 'Payment', createdAt: '2024-01-01' },
        { id: 't2', accountId: 'a1', amount: 50, currency: 'USD', type: 'debit' as const, description: 'Purchase', createdAt: '2024-01-02' }
      ];
      mockApiGet.mockResolvedValue(mockTransactions);

      const result = await search.searchTransactions('debit');

      expect(result).toHaveLength(1);
      expect(result[0].type).toBe('debit');
    });

    it('should limit results to 5', async () => {
      const mockTransactions = Array.from({ length: 10 }, (_, i) => ({
        id: `t${i}`,
        accountId: 'a1',
        amount: 100,
        currency: 'USD',
        type: 'credit' as const,
        description: 'Payment',
        createdAt: '2024-01-01'
      }));
      mockApiGet.mockResolvedValue(mockTransactions);

      const result = await search.searchTransactions('payment');

      expect(result).toHaveLength(5);
    });
  });

  describe('searchPayees', () => {
    it('should filter payees by name', async () => {
      const mockPayees = [
        { id: 'p1', name: 'John Doe', accountNumber: '1111' },
        { id: 'p2', name: 'Jane Smith', accountNumber: '2222' }
      ];
      mockApiGet.mockResolvedValue(mockPayees);

      const result = await search.searchPayees('john');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_PAYEES, { signal: undefined });
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('John Doe');
    });

    it('should filter by account number', async () => {
      const mockPayees = [
        { id: 'p1', name: 'John Doe', accountNumber: '1111' },
        { id: 'p2', name: 'Jane Smith', accountNumber: '2222' }
      ];
      mockApiGet.mockResolvedValue(mockPayees);

      const result = await search.searchPayees('2222');

      expect(result).toHaveLength(1);
      expect(result[0].accountNumber).toBe('2222');
    });

    it('should limit results to 5', async () => {
      const mockPayees = Array.from({ length: 10 }, (_, i) => ({
        id: `p${i}`,
        name: `Payee ${i}`,
        accountNumber: `1111${i}`
      }));
      mockApiGet.mockResolvedValue(mockPayees);

      const result = await search.searchPayees('payee');

      expect(result).toHaveLength(5);
    });

    it('should handle empty results', async () => {
      mockApiGet.mockResolvedValue([]);

      const result = await search.searchPayees('nonexistent');

      expect(result).toEqual([]);
    });
  });
});

describe('search service', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('searchAccounts', () => {
    it('should filter accounts by query', async () => {
      const mockAccounts = [
        { id: 'a1', accountNumber: '1234567890', accountType: 'checking', balance: 5000, currency: 'USD' },
        { id: 'a2', accountNumber: '0987654321', accountType: 'savings', balance: 10000, currency: 'EUR' }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockAccounts);

      const result = await search.searchAccounts('1234');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_ACCOUNTS, { signal: undefined });
      expect(result).toHaveLength(1);
      expect(result[0].accountNumber).toBe('1234567890');
    });

    it('should filter by account type', async () => {
      const mockAccounts = [
        { id: 'a1', accountNumber: '1111', accountType: 'checking', balance: 5000, currency: 'USD' },
        { id: 'a2', accountNumber: '2222', accountType: 'savings', balance: 10000, currency: 'EUR' }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockAccounts);

      const result = await search.searchAccounts('savings');

      expect(result).toHaveLength(1);
      expect(result[0].accountType).toBe('savings');
    });

    it('should limit results to 5', async () => {
      const mockAccounts = Array.from({ length: 10 }, (_, i) => ({
        id: `a${i}`,
        accountNumber: `1234${i}`,
        accountType: 'checking',
        balance: 1000,
        currency: 'USD'
      }));
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockAccounts);

      const result = await search.searchAccounts('1234');

      expect(result).toHaveLength(5);
    });

    it('should support abort signal', async () => {
      const controller = new AbortController();
       (apiClient.apiGet as jest.Mock).mockResolvedValue([]);

      await search.searchAccounts('test', controller.signal);

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_ACCOUNTS, { signal: controller.signal });
    });

    it('should handle API error', async () => {
       (apiClient.apiGet as jest.Mock).mockRejectedValue(new apiClient.ApiError(500, 'Server error'));

      await expect(search.searchAccounts('test')).rejects.toThrow('Server error');
    });
  });

  describe('searchTransactions', () => {
    it('should filter transactions by description', async () => {
      const mockTransactions = [
        { id: 't1', accountId: 'a1', amount: 100, currency: 'USD', type: 'credit' as const, description: 'Salary payment', createdAt: '2024-01-01' },
        { id: 't2', accountId: 'a1', amount: 50, currency: 'USD', type: 'debit' as const, description: 'Coffee shop', createdAt: '2024-01-02' }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockTransactions);

      const result = await search.searchTransactions('salary');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_TRANSACTIONS, { signal: undefined });
      expect(result).toHaveLength(1);
      expect(result[0].description).toBe('Salary payment');
    });

    it('should filter by amount', async () => {
      const mockTransactions = [
        { id: 't1', accountId: 'a1', amount: 100, currency: 'USD', type: 'credit' as const, description: 'Payment', createdAt: '2024-01-01' },
        { id: 't2', accountId: 'a1', amount: 50, currency: 'USD', type: 'debit' as const, description: 'Purchase', createdAt: '2024-01-02' }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockTransactions);

      const result = await search.searchTransactions('50');

      expect(result).toHaveLength(1);
      expect(result[0].amount).toBe(50);
    });

    it('should filter by type', async () => {
      const mockTransactions = [
        { id: 't1', accountId: 'a1', amount: 100, currency: 'USD', type: 'credit' as const, description: 'Payment', createdAt: '2024-01-01' },
        { id: 't2', accountId: 'a1', amount: 50, currency: 'USD', type: 'debit' as const, description: 'Purchase', createdAt: '2024-01-02' }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockTransactions);

      const result = await search.searchTransactions('debit');

      expect(result).toHaveLength(1);
      expect(result[0].type).toBe('debit');
    });

    it('should limit results to 5', async () => {
      const mockTransactions = Array.from({ length: 10 }, (_, i) => ({
        id: `t${i}`,
        accountId: 'a1',
        amount: 100,
        currency: 'USD',
        type: 'credit' as const,
        description: 'Payment',
        createdAt: '2024-01-01'
      }));
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockTransactions);

      const result = await search.searchTransactions('payment');

      expect(result).toHaveLength(5);
    });
  });

  describe('searchPayees', () => {
    it('should filter payees by name', async () => {
      const mockPayees = [
        { id: 'p1', name: 'John Doe', accountNumber: '1111' },
        { id: 'p2', name: 'Jane Smith', accountNumber: '2222' }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockPayees);

      const result = await search.searchPayees('john');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_PAYEES, { signal: undefined });
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe('John Doe');
    });

    it('should filter by account number', async () => {
      const mockPayees = [
        { id: 'p1', name: 'John Doe', accountNumber: '1111' },
        { id: 'p2', name: 'Jane Smith', accountNumber: '2222' }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockPayees);

      const result = await search.searchPayees('2222');

      expect(result).toHaveLength(1);
      expect(result[0].accountNumber).toBe('2222');
    });

    it('should limit results to 5', async () => {
      const mockPayees = Array.from({ length: 10 }, (_, i) => ({
        id: `p${i}`,
        name: `Payee ${i}`,
        accountNumber: `1111${i}`
      }));
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockPayees);

      const result = await search.searchPayees('payee');

      expect(result).toHaveLength(5);
    });

    it('should handle empty results', async () => {
       (apiClient.apiGet as jest.Mock).mockResolvedValue([]);

      const result = await search.searchPayees('nonexistent');

      expect(result).toEqual([]);
    });
  });
});

describe('unwrapCollection object branch coverage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should unwrap accounts from items key in object response', async () => {
    const account = { id: 'a1', accountNumber: 'test001', accountType: 'checking', balance: 1000, currency: 'USD' };
    (apiClient.apiGet as jest.Mock).mockResolvedValue({ items: [account] });

    const result = await search.searchAccounts('test');

    expect(result).toHaveLength(1);
    expect(result[0].accountNumber).toBe('test001');
  });

  it('should unwrap accounts from results key in object response', async () => {
    const account = { id: 'a1', accountNumber: 'test001', accountType: 'checking', balance: 1000, currency: 'USD' };
    (apiClient.apiGet as jest.Mock).mockResolvedValue({ results: [account] });

    const result = await search.searchAccounts('test');

    expect(result).toHaveLength(1);
  });

  it('should unwrap accounts from data key in object response', async () => {
    const account = { id: 'a1', accountNumber: 'test001', accountType: 'checking', balance: 1000, currency: 'USD' };
    (apiClient.apiGet as jest.Mock).mockResolvedValue({ data: [account] });

    const result = await search.searchAccounts('test');

    expect(result).toHaveLength(1);
  });

  it('should unwrap accounts from value key in object response', async () => {
    const account = { id: 'a1', accountNumber: 'test001', accountType: 'checking', balance: 1000, currency: 'USD' };
    (apiClient.apiGet as jest.Mock).mockResolvedValue({ value: [account] });

    const result = await search.searchAccounts('test');

    expect(result).toHaveLength(1);
  });

  it('should unwrap accounts from records key in object response', async () => {
    const account = { id: 'a1', accountNumber: 'test001', accountType: 'checking', balance: 1000, currency: 'USD' };
    (apiClient.apiGet as jest.Mock).mockResolvedValue({ records: [account] });

    const result = await search.searchAccounts('test');

    expect(result).toHaveLength(1);
  });

  it('should return empty array when object has no recognized collection key', async () => {
    (apiClient.apiGet as jest.Mock).mockResolvedValue({ unknown: [] });

    const result = await search.searchAccounts('test');

    expect(result).toEqual([]);
  });
});

describe('loadSearchData', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should return results and empty message when matches are found', async () => {
    (apiClient.apiGet as jest.Mock).mockImplementation((url: string) => {
      if (url === API.SEARCH_ACCOUNTS) {
        return Promise.resolve([{ id: 'a1', accountNumber: 'test001', accountType: 'checking', balance: 1000, currency: 'USD' }]);
      }
      return Promise.resolve([]);
    });

    const result = await search.loadSearchData('test');

    expect(result.message).toBe('');
    expect(result.accounts).toHaveLength(1);
    expect(result.transactions).toEqual([]);
    expect(result.payees).toEqual([]);
  });

  it('should return "No results found" message when nothing matches', async () => {
    (apiClient.apiGet as jest.Mock).mockResolvedValue([]);

    const result = await search.loadSearchData('zzznomatch');

    expect(result.message).toBe('No results found');
    expect(result.accounts).toEqual([]);
    expect(result.transactions).toEqual([]);
    expect(result.payees).toEqual([]);
  });

  it('should return "Request timed out" message on AbortError', async () => {
    const abortError = new Error('AbortError');
    abortError.name = 'AbortError';
    (apiClient.apiGet as jest.Mock).mockRejectedValue(abortError);

    const result = await search.loadSearchData('test');

    expect(result.message).toBe('Request timed out');
    expect(result.accounts).toEqual([]);
    expect(result.transactions).toEqual([]);
    expect(result.payees).toEqual([]);
  });

  it('should return "Request timed out" message when error message contains timeout', async () => {
    (apiClient.apiGet as jest.Mock).mockRejectedValue(new Error('Request timeout exceeded'));

    const result = await search.loadSearchData('test');

    expect(result.message).toBe('Request timed out');
    expect(result.accounts).toEqual([]);
  });

  it('should return "Failed to load search data" message on generic error', async () => {
    (apiClient.apiGet as jest.Mock).mockRejectedValue(new Error('Network error'));

    const result = await search.loadSearchData('test');

    expect(result.message).toBe('Failed to load search data');
    expect(result.accounts).toEqual([]);
    expect(result.transactions).toEqual([]);
    expect(result.payees).toEqual([]);
  });

  it('should pass abort signal through to underlying search functions', async () => {
    (apiClient.apiGet as jest.Mock).mockResolvedValue([]);
    const controller = new AbortController();

    await search.loadSearchData('test', controller.signal);

    expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_ACCOUNTS, { signal: controller.signal });
    expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_TRANSACTIONS, { signal: controller.signal });
    expect(apiClient.apiGet).toHaveBeenCalledWith(API.SEARCH_PAYEES, { signal: controller.signal });
  });
});
