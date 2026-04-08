import { apiGet } from '../api/apiClient';
import { API } from '../config/constants';

export async function getKycStatus(): Promise<{ userId: string; status: string }> {
  return apiGet<{ userId: string; status: string }>(API.KYC_STATUS);
}

export interface SarReport {
  id: string;
  transactionId: string;
  userId: string;
  amount: number;
  currency: string;
  reason: string;
  riskLevel: string;
  flaggedAt: string;
  status: string;
}

export async function getSarReports(): Promise<SarReport[]> {
  return apiGet<SarReport[]>(API.SAR_REPORTS);
}
