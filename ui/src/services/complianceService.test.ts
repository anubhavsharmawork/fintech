
import * as complianceService from './complianceService';
import * as apiClient from '../api/apiClient';
import { API } from '../config/constants';

jest.mock('../api/apiClient', () => ({
  ...jest.requireActual('../api/apiClient'),
  apiGet: jest.fn(),
  apiPost: jest.fn(),
}));

describe('complianceService', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getKycStatus', () => {
    it('should fetch KYC status', async () => {
      const mockStatus = { userId: 'u1', status: 'verified' };
      (apiClient.apiGet as jest.Mock).mockResolvedValue(mockStatus);

      const result = await complianceService.getKycStatus();

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.KYC_STATUS);
      expect(result).toEqual(mockStatus);
    });

    it('should handle API error', async () => {
      (apiClient.apiGet as jest.Mock).mockRejectedValue(new apiClient.ApiError(403, 'Unauthorized'));

      await expect(complianceService.getKycStatus()).rejects.toThrow('Unauthorized');
    });
  });

  describe('getSarReports', () => {
    it('should fetch SAR reports', async () => {
      const mockReports = [
        {
          id: 's1',
          transactionId: 't1',
          userId: 'u1',
          amount: 10000,
          currency: 'USD',
          reason: 'Large transaction',
          riskLevel: 'high',
          flaggedAt: '2024-01-01',
          status: 'pending'
        }
      ];
      (apiClient.apiGet as jest.Mock).mockResolvedValue(mockReports);

      const result = await complianceService.getSarReports();

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SAR_REPORTS);
      expect(result).toEqual(mockReports);
    });

    it('should handle empty SAR reports', async () => {
      (apiClient.apiGet as jest.Mock).mockResolvedValue([]);

      const result = await complianceService.getSarReports();

      expect(result).toEqual([]);
    });
  });
});

