
import * as corporate from './corporate';
import * as apiClient from '../api/apiClient';
import { ApiError } from '../api/apiClient';
import { API } from '../config/constants';

jest.mock('../api/apiClient', () => ({
  ...jest.requireActual('../api/apiClient'),
  apiGet: jest.fn(),
  apiPost: jest.fn(),
}));

const mockApiGet = apiClient.apiGet as jest.Mock;
const mockApiPost = apiClient.apiPost as jest.Mock;

describe('corporate service', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('getOrganisation', () => {
    it('should fetch organisation details', async () => {
      const mockOrg = { id: 'org1', name: 'Acme Corp', registrationNumber: 'REG123', createdAt: '2024-01-01' };
      mockApiGet.mockResolvedValue(mockOrg);

      const result = await corporate.getOrganisation('org1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.ORGANISATION('org1'));
      expect(result).toEqual(mockOrg);
    });

    it('should handle not found error', async () => {
      mockApiGet.mockRejectedValue(new ApiError(404, 'Organisation not found'));

      await expect(corporate.getOrganisation('invalid')).rejects.toThrow('Organisation not found');
    });
  });

  describe('getMembers', () => {
    it('should fetch organisation members', async () => {
      const mockMembers = [
        { id: 'm1', userId: 'u1', email: 'user@test.com', role: 'admin', status: 'active', invitedAt: '2024-01-01', acceptedAt: '2024-01-02' }
      ];
      mockApiGet.mockResolvedValue(mockMembers);

      const result = await corporate.getMembers('org1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.ORGANISATION_MEMBERS('org1'));
      expect(result).toEqual(mockMembers);
    });
  });

  describe('inviteMember', () => {
    it('should invite a new member', async () => {
      const mockMember = { id: 'm2', userId: 'u2', email: 'new@test.com', role: 'viewer', status: 'pending', invitedAt: '2024-01-03', acceptedAt: null };
      mockApiPost.mockResolvedValue(mockMember);

      const result = await corporate.inviteMember('org1', 'new@test.com', 'viewer');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.ORGANISATION_MEMBERS_INVITE('org1'), { email: 'new@test.com', role: 'viewer' });
      expect(result).toEqual(mockMember);
    });
  });

  describe('getPaymentBatches', () => {
    it('should fetch payment batches', async () => {
      const mockBatches = [
        { id: 'b1', organisationId: 'org1', submittedByUserId: 'u1', status: 'draft', currency: 'USD', totalAmount: 1000, itemCount: 5, createdAt: '2024-01-01', submittedAt: null, executedAt: null }
      ];
      mockApiGet.mockResolvedValue(mockBatches);

      const result = await corporate.getPaymentBatches();

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.PAYMENT_BATCHES);
      expect(result).toEqual(mockBatches);
    });
  });

  describe('getPaymentBatch', () => {
    it('should fetch batch detail with items and approvals', async () => {
      const mockDetail = {
        id: 'b1',
        organisationId: 'org1',
        submittedByUserId: 'u1',
        status: 'pending_approval',
        currency: 'USD',
        totalAmount: 1000,
        itemCount: 1,
        createdAt: '2024-01-01',
        submittedAt: '2024-01-02',
        executedAt: null,
        items: [{ sourceAccountId: 'acc1', payeeName: 'John', payeeAccountNumber: '12345', amount: 1000, description: 'Payment' }],
        approvals: []
      };
      mockApiGet.mockResolvedValue(mockDetail);

      const result = await corporate.getPaymentBatch('b1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.PAYMENT_BATCH('b1'));
      expect(result.items).toHaveLength(1);
    });
  });

  describe('createPaymentBatch', () => {
    it('should create a payment batch', async () => {
      const items = [{ sourceAccountId: 'acc1', payeeName: 'Test', payeeAccountNumber: '123', amount: 500, description: 'Test payment' }];
      const mockBatch = { id: 'b2', organisationId: 'org1', submittedByUserId: 'u1', status: 'draft', currency: 'USD', totalAmount: 500, itemCount: 1, createdAt: '2024-01-03', submittedAt: null, executedAt: null };
      mockApiPost.mockResolvedValue(mockBatch);

      const result = await corporate.createPaymentBatch('USD', items);

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.PAYMENT_BATCHES, { currency: 'USD', items });
      expect(result).toEqual(mockBatch);
    });
  });

  describe('submitBatchForApproval', () => {
    it('should submit batch for approval', async () => {
      const mockBatch = { id: 'b1', organisationId: 'org1', submittedByUserId: 'u1', status: 'pending_approval', currency: 'USD', totalAmount: 1000, itemCount: 1, createdAt: '2024-01-01', submittedAt: '2024-01-02', executedAt: null };
      mockApiPost.mockResolvedValue(mockBatch);

      const result = await corporate.submitBatchForApproval('b1');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.PAYMENT_BATCH_SUBMIT('b1'), {});
      expect(result.status).toBe('pending_approval');
    });
  });

  describe('executeBatch', () => {
    it('should execute batch', async () => {
      const mockBatch = { id: 'b1', organisationId: 'org1', submittedByUserId: 'u1', status: 'executed', currency: 'USD', totalAmount: 1000, itemCount: 1, createdAt: '2024-01-01', submittedAt: '2024-01-02', executedAt: '2024-01-03' };
      mockApiPost.mockResolvedValue(mockBatch);

      const result = await corporate.executeBatch('b1');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.PAYMENT_BATCH_EXECUTE('b1'), {});
      expect(result.status).toBe('executed');
    });
  });

  describe('getPendingApprovals', () => {
    it('should fetch pending approvals', async () => {
      const mockBatches = [
        { id: 'b1', organisationId: 'org1', submittedByUserId: 'u1', status: 'pending_approval', currency: 'USD', totalAmount: 1000, itemCount: 1, createdAt: '2024-01-01', submittedAt: '2024-01-02', executedAt: null }
      ];
      mockApiGet.mockResolvedValue(mockBatches);

      const result = await corporate.getPendingApprovals();

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.APPROVALS_PENDING);
      expect(result).toEqual(mockBatches);
    });

    it('should handle empty pending approvals', async () => {
      mockApiGet.mockResolvedValue([]);

      const result = await corporate.getPendingApprovals();

      expect(result).toEqual([]);
    });
  });

  describe('getApprovalBatchDetail', () => {
    it('should fetch approval batch detail', async () => {
      const mockDetail = {
        id: 'b1',
        organisationId: 'org1',
        submittedByUserId: 'u1',
        status: 'pending_approval',
        currency: 'USD',
        totalAmount: 1000,
        itemCount: 1,
        createdAt: '2024-01-01',
        submittedAt: '2024-01-02',
        executedAt: null,
        items: [],
        approvals: []
      };
      mockApiGet.mockResolvedValue(mockDetail);

      const result = await corporate.getApprovalBatchDetail('b1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.APPROVAL_DETAIL('b1'));
      expect(result).toEqual(mockDetail);
    });
  });

  describe('decideBatch', () => {
    it('should approve a batch with comments', async () => {
      const mockRecord = { id: 'ar1', approvedByUserId: 'u1', decision: 'Approved', comments: 'LGTM', decidedAt: '2024-01-03' };
      mockApiPost.mockResolvedValue(mockRecord);

      const result = await corporate.decideBatch('b1', 'Approved', 'LGTM');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.APPROVAL_DECIDE('b1'), { decision: 'Approved', comments: 'LGTM' });
      expect(result.decision).toBe('Approved');
    });

    it('should reject a batch without comments', async () => {
      const mockRecord = { id: 'ar2', approvedByUserId: 'u1', decision: 'Rejected', comments: undefined, decidedAt: '2024-01-03' };
      mockApiPost.mockResolvedValue(mockRecord);

      await corporate.decideBatch('b1', 'Rejected');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.APPROVAL_DECIDE('b1'), { decision: 'Rejected', comments: undefined });
    });
  });

  describe('getOrganisationAccounts', () => {
    it('should fetch organisation accounts', async () => {
      const mockAccounts = [{ id: 'acc1', accountNumber: '12345', balance: 5000 }];
      mockApiGet.mockResolvedValue(mockAccounts);

      const result = await corporate.getOrganisationAccounts('org1');

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.ORG_ACCOUNTS('org1'));
      expect(result).toEqual(mockAccounts);
    });
  });
});

