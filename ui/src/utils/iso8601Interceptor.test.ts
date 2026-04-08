import { toUtcIso, normaliseOutgoing, parseUtcIso } from './iso8601Interceptor';

describe('iso8601Interceptor', () => {
  describe('toUtcIso', () => {
    it('should convert Date to ISO string', () => {
      const date = new Date('2024-01-15T10:30:00Z');
      const result = toUtcIso(date);

      expect(result).toBe('2024-01-15T10:30:00.000Z');
    });

    it('should convert timestamp number to ISO string', () => {
      const timestamp = Date.UTC(2024, 0, 15, 10, 30, 0);
      const result = toUtcIso(timestamp);

      expect(result).toMatch(/^2024-01-15T10:30:00\.000Z$/);
    });

    it('should convert ISO string to normalized UTC', () => {
      const result = toUtcIso('2024-01-15T10:30:00');

      expect(result).toMatch(/Z$/);
    });

    it('should return original string if invalid date', () => {
      const result = toUtcIso('not-a-date');

      expect(result).toBe('not-a-date');
    });
  });

  describe('normaliseOutgoing', () => {
    it('should normalize date strings in object', () => {
      const input = {
        createdAt: '2024-01-15T10:30:00',
        name: 'Test'
      };

      const result = normaliseOutgoing(input);

      expect(result.createdAt).toMatch(/Z$/);
      expect(result.name).toBe('Test');
    });

    it('should normalize nested objects', () => {
      const input = {
        user: {
          createdAt: '2024-01-15T10:30:00',
          email: 'test@test.com'
        }
      };

      const result = normaliseOutgoing(input);

      expect(result.user.createdAt).toMatch(/Z$/);
      expect(result.user.email).toBe('test@test.com');
    });

    it('should normalize arrays', () => {
      const input = [
        { createdAt: '2024-01-15T10:30:00' },
        { createdAt: '2024-01-16T11:00:00' }
      ];

      const result = normaliseOutgoing(input);

      expect(result[0].createdAt).toMatch(/Z$/);
      expect(result[1].createdAt).toMatch(/Z$/);
    });

    it('should handle null and undefined', () => {
      expect(normaliseOutgoing(null)).toBeNull();
      expect(normaliseOutgoing(undefined)).toBeUndefined();
    });

    it('should not modify non-date strings', () => {
      const input = { message: 'Hello', count: 42 };
      const result = normaliseOutgoing(input);

      expect(result).toEqual(input);
    });
  });

  describe('parseUtcIso', () => {
    it('should parse ISO string with Z', () => {
      const result = parseUtcIso('2024-01-15T10:30:00Z');

      expect(result.toISOString()).toBe('2024-01-15T10:30:00.000Z');
    });

    it('should append Z to ISO string without timezone', () => {
      const result = parseUtcIso('2024-01-15T10:30:00');

      expect(result.toISOString()).toBe('2024-01-15T10:30:00.000Z');
    });

    it('should preserve timezone offset', () => {
      const result = parseUtcIso('2024-01-15T10:30:00+05:00');

      expect(result instanceof Date).toBe(true);
    });

    it('should handle non-ISO strings', () => {
      const result = parseUtcIso('January 15, 2024');

      expect(result instanceof Date).toBe(true);
    });
  });
});

