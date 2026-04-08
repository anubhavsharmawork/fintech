import { apiRequest, apiPost, apiPut } from '../api/apiClient';
import { API, NOTIFICATION_EVENTS } from '../config/constants';

export interface NotificationEvent {
  id: string;
  eventType: string;
  message: string;
  timestamp: string;
  read: boolean;
}

export interface NotificationPreference {
  eventType: string;
  emailEnabled: boolean;
  smsEnabled: boolean;
}

const DEFAULT_PREFERENCES: NotificationPreference[] = [
  { eventType: NOTIFICATION_EVENTS.TRANSACTION_CREATED, emailEnabled: true, smsEnabled: true },
  { eventType: NOTIFICATION_EVENTS.PAYMENT_APPROVED, emailEnabled: true, smsEnabled: true },
  { eventType: NOTIFICATION_EVENTS.BATCH_SUBMITTED, emailEnabled: true, smsEnabled: true },
  { eventType: NOTIFICATION_EVENTS.REPAYMENT_COMPLETED, emailEnabled: true, smsEnabled: true },
  { eventType: NOTIFICATION_EVENTS.KYC_STATUS_CHANGED, emailEnabled: true, smsEnabled: true },
  { eventType: NOTIFICATION_EVENTS.SUSPICIOUS_ACTIVITY, emailEnabled: true, smsEnabled: true },
];

function isJsonResponse(res: Response): boolean {
  return res.ok && (res.headers.get('content-type') ?? '').includes('application/json');
}

export async function fetchNotifications(): Promise<NotificationEvent[]> {
  try {
    const res = await apiRequest(API.NOTIFICATIONS);
    if (!isJsonResponse(res)) return [];
    return (await res.json()) as NotificationEvent[];
  } catch {
    return [];
  }
}

export async function markAllRead(): Promise<void> {
  try {
    await apiPost(API.NOTIFICATIONS_READ_ALL, {});
  } catch {
    // silently resolve — stub may not be available yet
  }
}

export async function fetchPreferences(): Promise<NotificationPreference[]> {
  try {
    const res = await apiRequest(API.NOTIFICATIONS_PREFERENCES);
    if (!isJsonResponse(res)) return DEFAULT_PREFERENCES;
    return (await res.json()) as NotificationPreference[];
  } catch {
    return DEFAULT_PREFERENCES;
  }
}

export async function savePreferences(prefs: NotificationPreference[]): Promise<void> {
  try {
    await apiPut(API.NOTIFICATIONS_PREFERENCES, prefs);
  } catch {
    // silently resolve — stub may not be available yet
  }
}
