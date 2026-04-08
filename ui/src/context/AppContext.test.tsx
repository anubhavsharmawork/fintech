import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { AppProvider, useAppContext } from './AppContext';

const TestConsumer: React.FC = () => {
  const { user, unreadNotificationCount, setUnreadNotificationCount, refreshUser } = useAppContext();
  return (
    <div>
      <div data-testid="user-id">{user?.id || 'no-user'}</div>
      <div data-testid="user-email">{user?.email || 'no-email'}</div>
      <div data-testid="user-role">{user?.role || 'no-role'}</div>
      <div data-testid="user-name">{user?.name || 'no-name'}</div>
      <div data-testid="notification-count">{unreadNotificationCount}</div>
      <button onClick={() => setUnreadNotificationCount(5)}>Set Notifications</button>
      <button onClick={refreshUser}>Refresh User</button>
    </div>
  );
};

function createMockJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payloadBase64 = btoa(JSON.stringify(payload));
  return `${header}.${payloadBase64}.mock-sig`;
}

describe('AppContext', () => {
  const originalLocalStorage = global.localStorage;
  let mockStorage: { [key: string]: string };

  beforeEach(() => {
    mockStorage = {};
    Object.defineProperty(window, 'localStorage', {
      value: {
        getItem: jest.fn((key: string) => mockStorage[key] || null),
        setItem: jest.fn((key: string, value: string) => { mockStorage[key] = value; }),
        removeItem: jest.fn((key: string) => { delete mockStorage[key]; }),
        clear: jest.fn(() => { mockStorage = {}; }),
      },
      writable: true,
    });
  });

  afterEach(() => {
    Object.defineProperty(window, 'localStorage', { value: originalLocalStorage, writable: true });
    jest.restoreAllMocks();
  });

  describe('AppProvider', () => {
    it('should render children', () => {
      render(<AppProvider><div data-testid="child">Child</div></AppProvider>);
      expect(screen.getByTestId('child')).toBeInTheDocument();
    });

    it('should provide null user when no token', () => {
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-id')).toHaveTextContent('no-user');
    });

    it('should decode user from valid JWT', () => {
      mockStorage['token'] = createMockJwt({ sub: 'u1', email: 'a@b.com', role: 'admin', given_name: 'J', family_name: 'D' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-id')).toHaveTextContent('u1');
      expect(screen.getByTestId('user-email')).toHaveTextContent('a@b.com');
      expect(screen.getByTestId('user-role')).toHaveTextContent('admin');
      expect(screen.getByTestId('user-name')).toHaveTextContent('J D');
    });

    it('should use email as name when no names', () => {
      mockStorage['token'] = createMockJwt({ sub: 'u1', email: 'x@y.com', role: 'user' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-name')).toHaveTextContent('x@y.com');
    });

    it('should handle id claim instead of sub', () => {
      mockStorage['token'] = createMockJwt({ id: 'u2', email: 'x@y.com' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-id')).toHaveTextContent('u2');
    });

    it('should handle userId claim', () => {
      mockStorage['token'] = createMockJwt({ userId: 'u3', email: 'x@y.com' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-id')).toHaveTextContent('u3');
    });

    it('should handle unique_name claim', () => {
      mockStorage['token'] = createMockJwt({ sub: 'u1', unique_name: 'un@y.com' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-email')).toHaveTextContent('un@y.com');
    });

    it('should handle MS identity role claim', () => {
      mockStorage['token'] = createMockJwt({ sub: 'u1', email: 'x@y.com', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'admin' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-role')).toHaveTextContent('admin');
    });

    it('should default role to user', () => {
      mockStorage['token'] = createMockJwt({ sub: 'u1', email: 'x@y.com' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-role')).toHaveTextContent('user');
    });

    it('should return null for invalid JWT format', () => {
      mockStorage['token'] = 'invalid';
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-id')).toHaveTextContent('no-user');
    });

    it('should return null for JWT without id', () => {
      mockStorage['token'] = createMockJwt({ email: 'x@y.com' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-id')).toHaveTextContent('no-user');
    });
  });

  describe('setUnreadNotificationCount', () => {
    it('should initialize to 0', () => {
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('notification-count')).toHaveTextContent('0');
    });

    it('should update count', () => {
      render(<AppProvider><TestConsumer /></AppProvider>);
      fireEvent.click(screen.getByText('Set Notifications'));
      expect(screen.getByTestId('notification-count')).toHaveTextContent('5');
    });
  });

  describe('refreshUser', () => {
    it('should refresh user and update state', () => {
      mockStorage['token'] = createMockJwt({ sub: 'initial', email: 'n@y.com' });
      render(<AppProvider><TestConsumer /></AppProvider>);
      expect(screen.getByTestId('user-id')).toHaveTextContent('initial');

      mockStorage['token'] = createMockJwt({ sub: 'refreshed', email: 'r@y.com' });
      fireEvent.click(screen.getByText('Refresh User'));

      expect(screen.getByTestId('user-id')).toHaveTextContent('refreshed');
    });
  });

  describe('useAppContext', () => {
    it('should throw error outside AppProvider', () => {
      jest.spyOn(console, 'error').mockImplementation(() => {});
      expect(() => render(<TestConsumer />)).toThrow('useAppContext must be used within an AppProvider');
    });
  });
});
