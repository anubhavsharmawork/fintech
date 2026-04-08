/**
 * Iso8601Interceptor — ensures every date/time string sent over HTTP uses
 * UTC with a trailing "Z" suffix (ISO 8601) and that incoming UTC strings
 * are parsed consistently.  Works as a pair with the backend
 * Iso8601DateTimeConverter so there is never timezone drift or
 * locale-specific formatting surprises.
 */

const ISO_RE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;

/** Normalise a date-like value to a UTC ISO 8601 string with trailing Z. */
export function toUtcIso(value: string | Date | number): string {
  const d = value instanceof Date ? value : new Date(value);
  if (isNaN(d.getTime())) return String(value);
  return d.toISOString(); // always ends with Z
}

/**
 * Deep-walk a plain object / array and convert every string that looks like
 * an ISO 8601 timestamp to a guaranteed UTC "…Z" form.  This is intended
 * to be called on request bodies before they leave the client.
 */
export function normaliseOutgoing<T>(body: T): T {
  if (body === null || body === undefined) return body;
  if (typeof body === 'string' && ISO_RE.test(body)) return toUtcIso(body) as unknown as T;
  if (Array.isArray(body)) return body.map(normaliseOutgoing) as unknown as T;
  if (typeof body === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(body as Record<string, unknown>)) {
      out[k] = normaliseOutgoing(v);
    }
    return out as T;
  }
  return body;
}

/**
 * Parse an incoming ISO 8601 string into a JS Date.  If the string already
 * ends with "Z" it is used as-is; otherwise we assume UTC and append "Z"
 * so that `new Date()` does not apply a local offset.
 */
export function parseUtcIso(value: string): Date {
  if (!ISO_RE.test(value)) return new Date(value);
  const normalized = value.endsWith('Z') || /[+-]\d{2}:?\d{2}$/.test(value)
    ? value
    : value + 'Z';
  return new Date(normalized);
}
