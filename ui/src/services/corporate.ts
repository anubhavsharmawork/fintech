import { apiGet, apiPost } from '../api/apiClient';
import { API } from '../config/constants';

export interface Organisation {
  id: string;
  name: string;
  registrationNumber: string;
  createdAt: string;
}

export interface OrgMember {
  id: string;
  userId: string;
  email: string;
  role: string;
  status: string;
  invitedAt: string;
  acceptedAt: string | null;
}

export interface ApprovalPolicy {
  id: string;
  requiredApprovals: number;
  monetaryThreshold: number | null;
}

export interface PaymentBatch {
  id: string;
  organisationId: string;
  submittedByUserId: string;
  status: string;
  currency: string;
  totalAmount: number;
  itemCount: number;
  createdAt: string;
  submittedAt: string | null;
  executedAt: string | null;
}

export interface PaymentBatchItem {
  sourceAccountId: string;
  payeeName: string;
  payeeAccountNumber: string | null;
  amount: number;
  description: string | null;
}

export interface ApprovalRecord {
  id: string;
  approvedByUserId: string;
  decision: string;
  comments: string | null;
  decidedAt: string;
}

export interface PaymentBatchDetail extends PaymentBatch {
  items: PaymentBatchItem[];
  approvals: ApprovalRecord[];
}

export async function getOrganisation(orgId: string): Promise<Organisation> {
  return apiGet<Organisation>(API.ORGANISATION(orgId));
}

export async function getMembers(orgId: string): Promise<OrgMember[]> {
  return apiGet<OrgMember[]>(API.ORGANISATION_MEMBERS(orgId));
}

export async function inviteMember(orgId: string, email: string, role: string): Promise<OrgMember> {
  return apiPost<OrgMember>(API.ORGANISATION_MEMBERS_INVITE(orgId), { email, role });
}

export async function getPaymentBatches(): Promise<PaymentBatch[]> {
  return apiGet<PaymentBatch[]>(API.PAYMENT_BATCHES);
}

export async function getPaymentBatch(batchId: string): Promise<PaymentBatchDetail> {
  return apiGet<PaymentBatchDetail>(API.PAYMENT_BATCH(batchId));
}

export async function createPaymentBatch(currency: string, items: PaymentBatchItem[]): Promise<PaymentBatch> {
  return apiPost<PaymentBatch>(API.PAYMENT_BATCHES, { currency, items });
}

export async function submitBatchForApproval(batchId: string): Promise<PaymentBatch> {
  return apiPost<PaymentBatch>(API.PAYMENT_BATCH_SUBMIT(batchId), {});
}

export async function executeBatch(batchId: string): Promise<PaymentBatch> {
  return apiPost<PaymentBatch>(API.PAYMENT_BATCH_EXECUTE(batchId), {});
}

export async function getPendingApprovals(): Promise<PaymentBatch[]> {
  return apiGet<PaymentBatch[]>(API.APPROVALS_PENDING);
}

export async function getApprovalBatchDetail(batchId: string): Promise<PaymentBatchDetail> {
  return apiGet<PaymentBatchDetail>(API.APPROVAL_DETAIL(batchId));
}

export async function decideBatch(batchId: string, decision: string, comments?: string): Promise<ApprovalRecord> {
  return apiPost<ApprovalRecord>(API.APPROVAL_DECIDE(batchId), { decision, comments });
}

export async function getOrganisationAccounts(orgId: string): Promise<any[]> {
  return apiGet<any[]>(API.ORG_ACCOUNTS(orgId));
}
