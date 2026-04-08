import { apiGet, apiPost, apiPatch, apiDelete } from '../api/apiClient';
import { API } from '../config/constants';

export interface VirtualCard {
  id: string;
  userId: string;
  nickname: string;
  last4: string;
  expiryMonth: number;
  expiryYear: number;
  status: 'active' | 'frozen';
  createdAt: string;
}

export interface CardCreateResult {
  card: VirtualCard;
  cardNumber: string;
  cvv: string;
}

export async function listCards(): Promise<VirtualCard[]> {
  return apiGet<VirtualCard[]>(API.CARDS);
}

export async function createCard(nickname: string): Promise<CardCreateResult> {
  return apiPost<CardCreateResult>(API.CARDS, { nickname });
}

export async function freezeCard(id: string): Promise<VirtualCard> {
  return apiPatch<VirtualCard>(API.CARD_FREEZE(id));
}

export async function unfreezeCard(id: string): Promise<VirtualCard> {
  return apiPatch<VirtualCard>(API.CARD_UNFREEZE(id));
}

export async function deleteCard(id: string): Promise<void> {
  return apiDelete(API.CARD(id));
}
