import { apiGet, apiPost } from '../api/apiClient';
import { API } from '../config/constants';

export interface CreditFacilityDto {
  id: string;
  userId: string;
  walletAddress: string;
  creditLimit: number;
  drawnAmount: number;
  outstandingBalance: number;
  availableCredit: number;
  currency: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreditRepaymentDto {
  id: string;
  facilityId: string;
  amount: number;
  currency: string;
  status: string;
  createdAt: string;
}

export async function getCreditFacility(walletAddress: string): Promise<CreditFacilityDto> {
  return apiGet<CreditFacilityDto>(`${API.CREDIT_FACILITY}?walletAddress=${encodeURIComponent(walletAddress)}`);
}

export async function requestDrawdown(walletAddress: string, amount: number): Promise<CreditFacilityDto> {
  return apiPost<CreditFacilityDto>(API.CREDIT_DRAWDOWN, { walletAddress, amount });
}

export async function submitRepayment(walletAddress: string, amount: number): Promise<{ repayment: CreditRepaymentDto; facility: CreditFacilityDto }> {
  return apiPost<{ repayment: CreditRepaymentDto; facility: CreditFacilityDto }>(API.CREDIT_REPAYMENT, { walletAddress, amount });
}

export async function getRepayments(walletAddress: string): Promise<CreditRepaymentDto[]> {
  return apiGet<CreditRepaymentDto[]>(`${API.CREDIT_REPAYMENTS}?walletAddress=${encodeURIComponent(walletAddress)}`);
}
