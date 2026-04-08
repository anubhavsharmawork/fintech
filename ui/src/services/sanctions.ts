import { apiGet, apiPost } from '../api/apiClient';
import { API } from '../config/constants';

export interface SanctionRequestDto {
  id: string;
  externalProjectId: string;
  externalTenantId: string;
  userId: string;
  accountId: string;
  requestedAmount: number;
  currency: string;
  purpose: string;
  riskScore: number;
  kycStatus: string;
  amlStatus: string;
  status: string;
  approvedAmount: number | null;
  decisionReason: string | null;
  ftkTransactionRef: string | null;
  idempotencyKey: string;
  createdAt: string;
  updatedAt: string;
  createdBy: string;
}

export interface SanctionAuditLogDto {
  id: string;
  sanctionRequestId: string;
  fromStatus: string;
  toStatus: string;
  changedBy: string;
  reason: string;
  timestamp: string;
  correlationId: string;
}

export interface CreateSanctionRequest {
  externalProjectId: string;
  externalTenantId: string;
  userId: string;
  accountId: string;
  requestedAmount: number;
  currency?: string;
  purpose: string;
  idempotencyKey: string;
}

export interface PaginatedSanctionsResponse {
  items: SanctionRequestDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

function unwrapItems<T>(payload: unknown): T[] {
  if (Array.isArray(payload)) return payload as T[];
  if (payload && typeof payload === 'object') {
    const record = payload as Record<string, unknown>;
    for (const key of ['items', 'results', 'data', 'value', 'records']) {
      if (Array.isArray(record[key])) return record[key] as T[];
    }
  }
  return [];
}

function toPaginatedResponse(payload: unknown): PaginatedSanctionsResponse {
  const items = unwrapItems<SanctionRequestDto>(payload);
  const record = payload && typeof payload === 'object' ? (payload as Record<string, unknown>) : {};
  return {
    items,
    totalCount: typeof record.totalCount === 'number' ? record.totalCount : items.length,
    page: typeof record.page === 'number' ? record.page : 1,
    pageSize: typeof record.pageSize === 'number' ? record.pageSize : items.length,
  };
}

export async function getSanctions(): Promise<SanctionRequestDto[]> {
  const response = await apiGet<SanctionRequestDto[] | PaginatedSanctionsResponse>(API.SANCTIONS);
  return unwrapItems<SanctionRequestDto>(response);
}

export async function getSanctionsList(): Promise<PaginatedSanctionsResponse> {
  const response = await apiGet<SanctionRequestDto[] | PaginatedSanctionsResponse>(API.SANCTIONS);
  return toPaginatedResponse(response);
}

export async function getSanctionById(id: string): Promise<SanctionRequestDto> {
  const response = await apiGet<SanctionRequestDto | { item?: SanctionRequestDto }>(API.SANCTION(id));
  if (response && typeof response === 'object' && !Array.isArray(response) && 'item' in response && response.item) {
    return response.item;
  }
  return response as SanctionRequestDto;
}

export async function createSanction(dto: CreateSanctionRequest): Promise<SanctionRequestDto> {
  return apiPost<SanctionRequestDto>(API.SANCTIONS, dto);
}

export async function disburseSanction(id: string): Promise<SanctionRequestDto> {
  return apiPost<SanctionRequestDto>(API.SANCTION_DISBURSE(id), {});
}

export async function rejectSanction(id: string, reason: string): Promise<SanctionRequestDto> {
  return apiPost<SanctionRequestDto>(API.SANCTION_REJECT(id), { reason });
}

export async function cancelSanction(id: string, reason: string): Promise<SanctionRequestDto> {
  return apiPost<SanctionRequestDto>(API.SANCTION_CANCEL(id), { reason });
}

export async function getSanctionAudit(id: string): Promise<SanctionAuditLogDto[]> {
  const response = await apiGet<SanctionAuditLogDto[] | { items?: SanctionAuditLogDto[] }>(API.SANCTION_AUDIT(id));
  return unwrapItems<SanctionAuditLogDto>(response);
}
