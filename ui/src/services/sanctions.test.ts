
import * as sanctions from './sanctions';
import * as apiClient from '../api/apiClient';
import { API } from '../config/constants';

jest.mock('../api/apiClient', () => ({
  ...jest.requireActual('../api/apiClient'),
  apiGet: jest.fn(),
  apiPost: jest.fn(),
}));

const mockApiGet = apiClient.apiGet as jest.Mock;
const mockApiPost = apiClient.apiPost as jest.Mock;

const baseSanction = {
  id: 's1',
  externalProjectId: 'p1',
  externalTenantId: 't1',
  userId: 'u1',
  accountId: 'a1',
  requestedAmount: 5000,
  currency: 'USD',
  purpose: 'Equipment purchase',
  riskScore: 25,
  kycStatus: 'verified',
  amlStatus: 'clear',
  approvedAmount: null,
  decisionReason: null,
  ftkTransactionRef: null,
  idempotencyKey: 'idem1',
  createdAt: '2024-01-01',
  updatedAt: '2024-01-01',
  createdBy: 'user1',
};

describe('sanctions service', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getSanctions', () => {
    it('should fetch all sanctions', async () => {
      const mockSanctions = [{ ...baseSanction, status: 'pending' }];
      mockApiGet.mockResolvedValue(mockSanctions);

      const result = await sanctions.getSanctions();

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SANCTIONS);
      expect(result).toEqual(mockSanctions);
    });
  });

  describe('getSanctionById', () => {
    it('should fetch sanction by id', async () => {
      const mockSanction = { ...baseSanction, status: 'approved', approvedAmount: 5000, decisionReason: 'Low risk', ftkTransactionRef: 'tx123' };
      mockApiGet.mockResolvedValue(mockSanction);

      const result = await sanctions.getSanctionById('s1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SANCTION('s1'));
      expect(result).toEqual(mockSanction);
    });

    it('should handle not found error', async () => {
      mockApiGet.mockRejectedValue(new apiClient.ApiError(404, 'Sanction not found'));

      await expect(sanctions.getSanctionById('invalid')).rejects.toThrow('Sanction not found');
    });
  });

  describe('createSanction', () => {
    it('should create a new sanction request', async () => {
      const createDto = {
        externalProjectId: 'p1',
        externalTenantId: 't1',
        userId: 'u1',
        accountId: 'a1',
        requestedAmount: 3000,
        currency: 'USD',
        purpose: 'Medical supplies',
        idempotencyKey: 'idem2'
      };
      const mockResponse = { ...baseSanction, id: 's2', ...createDto, riskScore: 15, status: 'pending', createdAt: '2024-01-03', updatedAt: '2024-01-03' };
      mockApiPost.mockResolvedValue(mockResponse);

      const result = await sanctions.createSanction(createDto);

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.SANCTIONS, createDto);
      expect(result.id).toBe('s2');
    });
  });

  describe('disburseSanction', () => {
    it('should disburse an approved sanction with empty body', async () => {
      const mockSanction = { ...baseSanction, status: 'disbursed', approvedAmount: 5000, ftkTransactionRef: 'tx123' };
      mockApiPost.mockResolvedValue(mockSanction);

      const result = await sanctions.disburseSanction('s1');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.SANCTION_DISBURSE('s1'), {});
      expect(result.status).toBe('disbursed');
    });
  });

  describe('rejectSanction', () => {
    it('should reject a sanction with reason', async () => {
      const mockSanction = { ...baseSanction, status: 'rejected', riskScore: 85, kycStatus: 'pending', amlStatus: 'flagged', decisionReason: 'High risk score' };
      mockApiPost.mockResolvedValue(mockSanction);

      const result = await sanctions.rejectSanction('s1', 'High risk score');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.SANCTION_REJECT('s1'), { reason: 'High risk score' });
      expect(result.status).toBe('rejected');
      expect(result.decisionReason).toBe('High risk score');
    });
  });

  describe('cancelSanction', () => {
    it('should cancel a sanction', async () => {
      const mockSanction = { ...baseSanction, status: 'cancelled', decisionReason: 'User requested cancellation' };
      mockApiPost.mockResolvedValue(mockSanction);

      const result = await sanctions.cancelSanction('s1', 'User requested cancellation');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.SANCTION_CANCEL('s1'), { reason: 'User requested cancellation' });
      expect(result.status).toBe('cancelled');
    });
  });

  describe('getSanctionAudit', () => {
    it('should fetch audit log for sanction', async () => {
      const mockAudit = [
        {
          id: 'a1',
          sanctionRequestId: 's1',
          fromStatus: 'pending',
          toStatus: 'approved',
          changedBy: 'admin',
          reason: 'Low risk verified',
          timestamp: '2024-01-02',
          correlationId: 'cor1'
        }
      ];
      mockApiGet.mockResolvedValue(mockAudit);

      const result = await sanctions.getSanctionAudit('s1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SANCTION_AUDIT('s1'));
      expect(result).toHaveLength(1);
      expect(result[0].toStatus).toBe('approved');
    });

    it('should handle empty audit log', async () => {
      mockApiGet.mockResolvedValue([]);

      const result = await sanctions.getSanctionAudit('s1');

      expect(result).toEqual([]);
    });
  });
});

describe('sanctions service', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getSanctions', () => {
    it('should fetch all sanctions', async () => {
      const mockSanctions = [
        {
          id: 's1',
          externalProjectId: 'p1',
          externalTenantId: 't1',
          userId: 'u1',
          accountId: 'a1',
          requestedAmount: 5000,
          currency: 'USD',
          purpose: 'Equipment purchase',
          riskScore: 25,
          kycStatus: 'verified',
          amlStatus: 'clear',
          status: 'pending',
          approvedAmount: null,
          decisionReason: null,
          ftkTransactionRef: null,
          idempotencyKey: 'idem1',
          createdAt: '2024-01-01',
          updatedAt: '2024-01-01',
          createdBy: 'user1'
        }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockSanctions);

      const result = await sanctions.getSanctions();

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SANCTIONS);
      expect(result).toEqual(mockSanctions);
    });
  });

  describe('getSanctionById', () => {
    it('should fetch sanction by id', async () => {
      const mockSanction = {
        id: 's1',
        externalProjectId: 'p1',
        externalTenantId: 't1',
        userId: 'u1',
        accountId: 'a1',
        requestedAmount: 5000,
        currency: 'USD',
        purpose: 'Equipment',
        riskScore: 25,
        kycStatus: 'verified',
        amlStatus: 'clear',
        status: 'approved',
        approvedAmount: 5000,
        decisionReason: 'Low risk',
        ftkTransactionRef: 'tx123',
        idempotencyKey: 'idem1',
        createdAt: '2024-01-01',
        updatedAt: '2024-01-02',
        createdBy: 'user1'
      };
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockSanction);

      const result = await sanctions.getSanctionById('s1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SANCTION('s1'));
      expect(result).toEqual(mockSanction);
    });

    it('should handle not found error', async () => {
       (apiClient.apiGet as jest.Mock).mockRejectedValue(new apiClient.ApiError(404, 'Sanction not found'));

      await expect(sanctions.getSanctionById('invalid')).rejects.toThrow('Sanction not found');
    });
  });

  describe('createSanction', () => {
    it('should create a new sanction request', async () => {
      const createDto = {
        externalProjectId: 'p1',
        externalTenantId: 't1',
        userId: 'u1',
        accountId: 'a1',
        requestedAmount: 3000,
        currency: 'USD',
        purpose: 'Medical supplies',
        idempotencyKey: 'idem2'
      };
      const mockResponse = {
        id: 's2',
        ...createDto,
        riskScore: 15,
        kycStatus: 'verified',
        amlStatus: 'clear',
        status: 'pending',
        approvedAmount: null,
        decisionReason: null,
        ftkTransactionRef: null,
        createdAt: '2024-01-03',
        updatedAt: '2024-01-03',
        createdBy: 'user1'
      };
       (apiClient.apiPost as jest.Mock).mockResolvedValue(mockResponse);

      const result = await sanctions.createSanction(createDto);

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.SANCTIONS, createDto);
      expect(result.id).toBe('s2');
    });
  });

  describe('disburseSanction', () => {
    it('should disburse an approved sanction', async () => {
      const mockSanction = {
        id: 's1',
        externalProjectId: 'p1',
        externalTenantId: 't1',
        userId: 'u1',
        accountId: 'a1',
        requestedAmount: 5000,
        currency: 'USD',
        purpose: 'Equipment',
        riskScore: 25,
        kycStatus: 'verified',
        amlStatus: 'clear',
        status: 'disbursed',
        approvedAmount: 5000,
        decisionReason: null,
        ftkTransactionRef: 'tx123',
        idempotencyKey: 'idem1',
        createdAt: '2024-01-01',
        updatedAt: '2024-01-03',
        createdBy: 'user1'
      };
       (apiClient.apiPost as jest.Mock).mockResolvedValue(mockSanction);

      const result = await sanctions.disburseSanction('s1');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.SANCTION_DISBURSE('s1'), {});
      expect(result.status).toBe('disbursed');
    });
  });

  describe('rejectSanction', () => {
    it('should reject a sanction with reason', async () => {
      const mockSanction = {
        id: 's1',
        externalProjectId: 'p1',
        externalTenantId: 't1',
        userId: 'u1',
        accountId: 'a1',
        requestedAmount: 5000,
        currency: 'USD',
        purpose: 'Equipment',
        riskScore: 85,
        kycStatus: 'pending',
        amlStatus: 'flagged',
        status: 'rejected',
        approvedAmount: null,
        decisionReason: 'High risk score',
        ftkTransactionRef: null,
        idempotencyKey: 'idem1',
        createdAt: '2024-01-01',
        updatedAt: '2024-01-02',
        createdBy: 'user1'
      };
       (apiClient.apiPost as jest.Mock).mockResolvedValue(mockSanction);

      const result = await sanctions.rejectSanction('s1', 'High risk score');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.SANCTION_REJECT('s1'), { reason: 'High risk score' });
      expect(result.status).toBe('rejected');
      expect(result.decisionReason).toBe('High risk score');
    });
  });

  describe('cancelSanction', () => {
    it('should cancel a sanction', async () => {
      const mockSanction = {
        id: 's1',
        externalProjectId: 'p1',
        externalTenantId: 't1',
        userId: 'u1',
        accountId: 'a1',
        requestedAmount: 5000,
        currency: 'USD',
        purpose: 'Equipment',
        riskScore: 25,
        kycStatus: 'verified',
        amlStatus: 'clear',
        status: 'cancelled',
        approvedAmount: null,
        decisionReason: 'User requested cancellation',
        ftkTransactionRef: null,
        idempotencyKey: 'idem1',
        createdAt: '2024-01-01',
        updatedAt: '2024-01-03',
        createdBy: 'user1'
      };
       (apiClient.apiPost as jest.Mock).mockResolvedValue(mockSanction);

      const result = await sanctions.cancelSanction('s1', 'User requested cancellation');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.SANCTION_CANCEL('s1'), { reason: 'User requested cancellation' });
      expect(result.status).toBe('cancelled');
    });
  });

  describe('getSanctionAudit', () => {
    it('should fetch audit log for sanction', async () => {
      const mockAudit = [
        {
          id: 'a1',
          sanctionRequestId: 's1',
          fromStatus: 'pending',
          toStatus: 'approved',
          changedBy: 'admin',
          reason: 'Low risk verified',
          timestamp: '2024-01-02',
          correlationId: 'cor1'
        }
      ];
       (apiClient.apiGet as jest.Mock).mockResolvedValue(mockAudit);

      const result = await sanctions.getSanctionAudit('s1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SANCTION_AUDIT('s1'));
      expect(result).toHaveLength(1);
      expect(result[0].toStatus).toBe('approved');
    });

    it('should handle empty audit log', async () => {
       (apiClient.apiGet as jest.Mock).mockResolvedValue([]);

      const result = await sanctions.getSanctionAudit('s1');

      expect(result).toEqual([]);
    });
  });
});

describe('getSanctionsList and unwrapItems coverage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getSanctionsList', () => {
    it('should return paginated response when payload is a plain array', async () => {
      const items = [{ ...baseSanction, status: 'pending' }];
      mockApiGet.mockResolvedValue(items);

      const result = await sanctions.getSanctionsList();

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SANCTIONS);
      expect(result.items).toEqual(items);
      expect(result.totalCount).toBe(1);
      expect(result.page).toBe(1);
      expect(result.pageSize).toBe(1);
    });

    it('should use explicit pagination fields when present in payload', async () => {
      const items = [{ ...baseSanction, status: 'pending' }];
      mockApiGet.mockResolvedValue({ items, totalCount: 20, page: 3, pageSize: 5 });

      const result = await sanctions.getSanctionsList();

      expect(result.items).toEqual(items);
      expect(result.totalCount).toBe(20);
      expect(result.page).toBe(3);
      expect(result.pageSize).toBe(5);
    });

    it('should unwrap items from results key', async () => {
      const items = [{ ...baseSanction, status: 'approved' }];
      mockApiGet.mockResolvedValue({ results: items });

      const result = await sanctions.getSanctionsList();

      expect(result.items).toEqual(items);
      expect(result.totalCount).toBe(1);
    });

    it('should unwrap items from data key', async () => {
      const items = [{ ...baseSanction, status: 'pending' }];
      mockApiGet.mockResolvedValue({ data: items });

      const result = await sanctions.getSanctionsList();

      expect(result.items).toEqual(items);
    });

    it('should unwrap items from value key', async () => {
      const items = [{ ...baseSanction, status: 'pending' }];
      mockApiGet.mockResolvedValue({ value: items });

      const result = await sanctions.getSanctionsList();

      expect(result.items).toEqual(items);
    });

    it('should unwrap items from records key', async () => {
      const items = [{ ...baseSanction, status: 'pending' }];
      mockApiGet.mockResolvedValue({ records: items });

      const result = await sanctions.getSanctionsList();

      expect(result.items).toEqual(items);
    });

    it('should return empty items with fallback pagination when payload has no recognized keys', async () => {
      mockApiGet.mockResolvedValue({ unknown: [] });

      const result = await sanctions.getSanctionsList();

      expect(result.items).toEqual([]);
      expect(result.totalCount).toBe(0);
      expect(result.page).toBe(1);
      expect(result.pageSize).toBe(0);
    });

    it('should return empty items with fallback pagination when payload is null', async () => {
      mockApiGet.mockResolvedValue(null);

      const result = await sanctions.getSanctionsList();

      expect(result.items).toEqual([]);
      expect(result.totalCount).toBe(0);
      expect(result.page).toBe(1);
      expect(result.pageSize).toBe(0);
    });
  });

  describe('getSanctionById with item wrapper', () => {
    it('should unwrap sanction from { item } response wrapper', async () => {
      const mockSanction = { ...baseSanction, status: 'approved' };
      mockApiGet.mockResolvedValue({ item: mockSanction });

      const result = await sanctions.getSanctionById('s1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.SANCTION('s1'));
      expect(result).toEqual(mockSanction);
    });
  });
});

