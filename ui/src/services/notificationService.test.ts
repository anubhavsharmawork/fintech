import {
  fetchNotifications,
  markAllRead,
  fetchPreferences,
  savePreferences,
  NotificationEvent,
  NotificationPreference,
} from './notificationService';
import * as apiClient from '../api/apiClient';
import { API, NOTIFICATION_EVENTS } from '../config/constants';

jest.mock('../api/apiClient');

const mockApiRequest = apiClient.apiRequest as jest.Mock;
const mockApiPost = apiClient.apiPost as jest.Mock;
const mockApiPut = apiClient.apiPut as jest.Mock;

describe('notificationService', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('fetchNotifications', () => {
    it('should return notifications when API returns valid JSON', async () => {
      const mockNotifications: NotificationEvent[] = [
        {
          id: 'n1',
          eventType: 'TransactionCreated',
          message: 'New transaction created',
          timestamp: '2024-01-15T10:00:00Z',
          read: false,
        },
        {
          id: 'n2',
          eventType: 'PaymentApproved',
          message: 'Payment was approved',
          timestamp: '2024-01-15T11:00:00Z',
          read: true,
        },
      ];

      const mockResponse = {
        ok: true,
        headers: {
          get: jest.fn().mockReturnValue('application/json'),
        },
        json: jest.fn().mockResolvedValue(mockNotifications),
      };
      mockApiRequest.mockResolvedValue(mockResponse);

      const result = await fetchNotifications();

      expect(apiClient.apiRequest).toHaveBeenCalledWith(API.NOTIFICATIONS);
      expect(result).toEqual(mockNotifications);
    });

    it('should return empty array when response is not JSON', async () => {
      const mockResponse = {
        ok: true,
        headers: {
          get: jest.fn().mockReturnValue('text/html'),
        },
      };
      mockApiRequest.mockResolvedValue(mockResponse);

      const result = await fetchNotifications();

      expect(result).toEqual([]);
    });

    it('should return empty array when response is not ok', async () => {
      const mockResponse = {
        ok: false,
        headers: {
          get: jest.fn().mockReturnValue('application/json'),
        },
      };
      mockApiRequest.mockResolvedValue(mockResponse);

      const result = await fetchNotifications();

      expect(result).toEqual([]);
    });

    it('should return empty array on API error', async () => {
      mockApiRequest.mockRejectedValue(new Error('Network error'));

      const result = await fetchNotifications();

      expect(result).toEqual([]);
    });

    it('should handle null content-type header', async () => {
      const mockResponse = {
        ok: true,
        headers: {
          get: jest.fn().mockReturnValue(null),
        },
      };
      mockApiRequest.mockResolvedValue(mockResponse);

      const result = await fetchNotifications();

      expect(result).toEqual([]);
    });
  });

  describe('markAllRead', () => {
    it('should call API to mark all notifications as read', async () => {
      mockApiPost.mockResolvedValue({});

      await markAllRead();

      expect(apiClient.apiPost).toHaveBeenCalledWith(API.NOTIFICATIONS_READ_ALL, {});
    });

    it('should not throw on API error', async () => {
      mockApiPost.mockRejectedValue(new Error('Server error'));

      await expect(markAllRead()).resolves.not.toThrow();
    });
  });

  describe('fetchPreferences', () => {
    it('should return preferences when API returns valid JSON', async () => {
      const mockPreferences: NotificationPreference[] = [
        { eventType: NOTIFICATION_EVENTS.TRANSACTION_CREATED, emailEnabled: true, smsEnabled: false },
        { eventType: NOTIFICATION_EVENTS.PAYMENT_APPROVED, emailEnabled: false, smsEnabled: true },
      ];

      const mockResponse = {
        ok: true,
        headers: {
          get: jest.fn().mockReturnValue('application/json'),
        },
        json: jest.fn().mockResolvedValue(mockPreferences),
      };
      mockApiRequest.mockResolvedValue(mockResponse);

      const result = await fetchPreferences();

      expect(apiClient.apiRequest).toHaveBeenCalledWith(API.NOTIFICATIONS_PREFERENCES);
      expect(result).toEqual(mockPreferences);
    });

    it('should return default preferences when response is not JSON', async () => {
      const mockResponse = {
        ok: true,
        headers: {
          get: jest.fn().mockReturnValue('text/plain'),
        },
      };
      mockApiRequest.mockResolvedValue(mockResponse);

      const result = await fetchPreferences();

      expect(result).toHaveLength(6); // 6 default preferences
      expect(result[0].eventType).toBe(NOTIFICATION_EVENTS.TRANSACTION_CREATED);
    });

    it('should return default preferences on API error', async () => {
      mockApiRequest.mockRejectedValue(new Error('Network error'));

      const result = await fetchPreferences();

      expect(result).toHaveLength(6);
    });

    it('should return default preferences when response is not ok', async () => {
      const mockResponse = {
        ok: false,
        headers: {
          get: jest.fn().mockReturnValue('application/json'),
        },
      };
      mockApiRequest.mockResolvedValue(mockResponse);

      const result = await fetchPreferences();

      expect(result).toHaveLength(6);
    });

    it('should have correct default preference values', async () => {
      mockApiRequest.mockRejectedValue(new Error('API unavailable'));

      const result = await fetchPreferences();

      // All defaults should have both email and SMS enabled
      result.forEach((pref) => {
        expect(pref.emailEnabled).toBe(true);
        expect(pref.smsEnabled).toBe(true);
      });
    });
  });

  describe('savePreferences', () => {
    it('should call API to save preferences', async () => {
      const preferences: NotificationPreference[] = [
        { eventType: NOTIFICATION_EVENTS.TRANSACTION_CREATED, emailEnabled: true, smsEnabled: false },
      ];
      mockApiPut.mockResolvedValue({});

      await savePreferences(preferences);

      expect(apiClient.apiPut).toHaveBeenCalledWith(API.NOTIFICATIONS_PREFERENCES, preferences);
    });

    it('should not throw on API error', async () => {
      mockApiPut.mockRejectedValue(new Error('Server error'));

      const preferences: NotificationPreference[] = [
        { eventType: NOTIFICATION_EVENTS.TRANSACTION_CREATED, emailEnabled: true, smsEnabled: false },
      ];

      await expect(savePreferences(preferences)).resolves.not.toThrow();
    });

    it('should handle empty preferences array', async () => {
      mockApiPut.mockResolvedValue({});

      await savePreferences([]);

      expect(apiClient.apiPut).toHaveBeenCalledWith(API.NOTIFICATIONS_PREFERENCES, []);
    });
  });
});
