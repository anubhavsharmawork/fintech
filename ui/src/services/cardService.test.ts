import * as cardService from './cardService';
import * as apiClient from '../api/apiClient';
import { API } from '../config/constants';

jest.mock('../api/apiClient');

describe('cardService', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('listCards', () => {
    it('should fetch list of cards', async () => {
      const mockCards = [
        { id: '1', userId: 'u1', nickname: 'My Card', last4: '1234', expiryMonth: 12, expiryYear: 2025, status: 'active' as const, createdAt: '2024-01-01' }
      ];
      (apiClient.apiGet as jest.Mock).mockResolvedValue(mockCards);

      const result = await cardService.listCards();

      expect(apiClient.apiGet).toHaveBeenCalledWith(API.CARDS);
      expect(result).toEqual(mockCards);
    });
  });

  describe('createCard', () => {
    it('should create a new card', async () => {
      const mockResult = {
        card: { id: '1', userId: 'u1', nickname: 'New Card', last4: '5678', expiryMonth: 6, expiryYear: 2026, status: 'active' as const, createdAt: '2024-01-01' },
        cardNumber: '1234567812345678',
        cvv: '123'
      };
      (apiClient.apiPost as jest.Mock).mockResolvedValue(mockResult);

      const result = await cardService.createCard('New Card');

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.CARDS, { nickname: 'New Card' });
      expect(result).toEqual(mockResult);
    });
  });

  describe('freezeCard', () => {
    it('should freeze a card', async () => {
      const mockCard = { id: '1', userId: 'u1', nickname: 'My Card', last4: '1234', expiryMonth: 12, expiryYear: 2025, status: 'frozen' as const, createdAt: '2024-01-01' };
      (apiClient.apiPatch as jest.Mock).mockResolvedValue(mockCard);

      const result = await cardService.freezeCard('1');

      expect(apiClient.apiPatch).toHaveBeenCalledWith(API.CARD_FREEZE('1'));
      expect(result.status).toBe('frozen');
    });
  });

  describe('unfreezeCard', () => {
    it('should unfreeze a card', async () => {
      const mockCard = { id: '1', userId: 'u1', nickname: 'My Card', last4: '1234', expiryMonth: 12, expiryYear: 2025, status: 'active' as const, createdAt: '2024-01-01' };
      (apiClient.apiPatch as jest.Mock).mockResolvedValue(mockCard);

      const result = await cardService.unfreezeCard('1');

      expect(apiClient.apiPatch).toHaveBeenCalledWith(API.CARD_UNFREEZE('1'));
      expect(result.status).toBe('active');
    });
  });

  describe('deleteCard', () => {
    it('should delete a card', async () => {
      (apiClient.apiDelete as jest.Mock).mockResolvedValue(undefined);

      await cardService.deleteCard('1');

      expect(apiClient.apiDelete).toHaveBeenCalledWith(API.CARD('1'));
    });
  });
});
