import { apiPut } from '../api/apiClient';
import { API } from '../config/constants';

export interface TimezonePreference {
  timeZoneId: string | null;
  utcOffsetMinutes: number | null;
}

/**
 * Detect the browser's current IANA timezone and UTC offset in minutes.
 */
export function detectBrowserTimezone(): TimezonePreference {
  try {
    const timeZoneId = Intl.DateTimeFormat().resolvedOptions().timeZone; // e.g. "Pacific/Auckland"
    const utcOffsetMinutes = -(new Date().getTimezoneOffset()); // getTimezoneOffset returns inverted sign
    return { timeZoneId, utcOffsetMinutes };
  } catch {
    return { timeZoneId: null, utcOffsetMinutes: null };
  }
}

/**
 * Send the user's timezone preference to the backend.
 */
export async function updateTimezone(pref: TimezonePreference): Promise<TimezonePreference> {
  return apiPut<TimezonePreference>(API.TIMEZONE, pref);
}

/**
 * Fire-and-forget: detect the browser timezone and push it to the server.
 * Intended to be called after successful login.
 */
export async function syncBrowserTimezone(): Promise<void> {
  try {
    const pref = detectBrowserTimezone();
    if (pref.timeZoneId) {
      await updateTimezone(pref);
    }
  } catch {
    // Best-effort — don't block login flow
  }
}
