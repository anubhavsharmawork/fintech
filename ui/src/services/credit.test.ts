
import * as credit from './credit';
import * as apiClient from '../api/apiClient';
import { API } from '../config/constants';

jest.mock('../api/apiClient', () => ({
  ...jest.requireActual('../api/apiClient'),
  apiGet: jest.fn(),
  apiPost: jest.fn(),
}));

const mockApiGet = apiClient.apiGet as jest.Mock;
const mockApiPost = apiClient.apiPost as jest.Mock;

describe('credit service', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getCreditFacility', () => {
    it('should fetch credit facility for wallet', async () => {
      const mockFacility = {
        id: 'cf1',
        userId: 'u1',
        walletAddress: '0x123',
        creditLimit: 10000,
        drawnAmount: 2000,
        outstandingBalance: 2000,
        availableCredit: 8000,
        currency: 'USD',
        status: 'active',
        createdAt: '2024-01-01',
        updatedAt: '2024-01-02'
      };
      mockApiGet.mockResolvedValue(mockFacility);

      const result = await credit.getCreditFacility('0x123');

      expect(apiClient.apiGet).toHaveBeenCalledWith(`${API.CREDIT_FACILITY}?walletAddress=${encodeURIComponent('0x123')}`);
      expect(result).toEqual(mockFacility);
    });

    it('should handle not found error', async () => {
      mockApiGet.mockRejectedValue(new apiClient.ApiError(404, 'Credit facility not found'));

      await expect(credit.getCreditFacility('0xinvalid')).rejects.toThrow('Credit facility not found');
    });
  });

  describe('requestDrawdown', () => {
    it('should request drawdown', async () => {
      const mockFacility = {
        id: 'cf1',
        userId: 'u1',
        walletAddress: '0x123',
        creditLimit: 10000,
        drawnAmount: 3000,
        outstandingBalance: 3000,
        availableCredit: 7000,
        currency: 'USD',
        status: 'active',
        createdAt: '2024-01-01',
        updatedAt: '2024-01-03'
      };
      mockApiPost.mockResolvedValue(mockFacility);

      const result = await credit.requestDrawdown('0x123', 1000);

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.CREDIT_DRAWDOWN, { walletAddress: '0x123', amount: 1000 });
      expect(result.drawnAmount).toBe(3000);
    });
  });

  describe('submitRepayment', () => {
    it('should submit repayment', async () => {
      const mockResponse = {
        repayment: {
          id: 'r1',
          facilityId: 'cf1',
          amount: 500,
          currency: 'USD',
          status: 'completed',
          createdAt: '2024-01-04'
        },
        facility: {
          id: 'cf1',
          userId: 'u1',
          walletAddress: '0x123',
          creditLimit: 10000,
          drawnAmount: 2500,
          outstandingBalance: 2500,
          availableCredit: 7500,
          currency: 'USD',
          status: 'active',
          createdAt: '2024-01-01',
          updatedAt: '2024-01-04'
        }
      };
      mockApiPost.mockResolvedValue(mockResponse);

      const result = await credit.submitRepayment('0x123', 500);

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.CREDIT_REPAYMENT, { walletAddress: '0x123', amount: 500 });
      expect(result.repayment.amount).toBe(500);
      expect(result.facility.outstandingBalance).toBe(2500);
    });
  });

  describe('getRepayments', () => {
    it('should fetch repayment history', async () => {
      const mockRepayments = [
        { id: 'r1', facilityId: 'cf1', amount: 500, currency: 'USD', status: 'completed', createdAt: '2024-01-04' },
        { id: 'r2', facilityId: 'cf1', amount: 300, currency: 'USD', status: 'completed', createdAt: '2024-01-05' }
      ];
      mockApiGet.mockResolvedValue(mockRepayments);

      const result = await credit.getRepayments('0x123');

      expect(apiClient.apiGet).toHaveBeenCalledWith(`${API.CREDIT_REPAYMENTS}?walletAddress=${encodeURIComponent('0x123')}`);
      expect(result).toHaveLength(2);
    });

    it('should handle empty repayment history', async () => {
      mockApiGet.mockResolvedValue([]);

      const result = await credit.getRepayments('0x123');

      expect(result).toEqual([]);
    });
  });
});

