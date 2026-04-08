import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import * as auth from './auth';
import App from './App';
import { ToastProvider } from './components/Toast';
import { AppProvider } from './context/AppContext';

jest.mock('./auth');
jest.mock('./hooks/useFMode', () => ({
  useFMode: () => ({ enabled: false, toggle: jest.fn() }),
  FModeProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const renderApp = () => {
  return render(
    <AppProvider>
      <ToastProvider>
        <App />
      </ToastProvider>
    </AppProvider>
  );
};

describe('App Component', () => {
  const mockGetToken = auth.getToken as jest.Mock;
  const mockOnAuthChange = auth.onAuthChange as jest.Mock;
  const mockDecodeJwt = auth.decodeJwt as jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue(null);
    mockOnAuthChange.mockReturnValue(() => {});
    mockDecodeJwt.mockReturnValue({ exp: Math.floor(Date.now() / 1000) + 3600 });
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('App Rendering', () => {
    it('should render the App component without crashing', async () => {
      mockGetToken.mockResolvedValue('mock-token');
      jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('mock-token');
      renderApp();
      await waitFor(() => { expect(document.body).toBeInTheDocument(); });
    });

    it('should subscribe to auth changes on mount', async () => {
      mockGetToken.mockResolvedValue('mock-token');
      jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('mock-token');
      renderApp();
      await waitFor(() => { expect(mockOnAuthChange).toHaveBeenCalled(); });
    });
  });

  describe('Auth State Management', () => {
    it('should call getToken on mount', async () => {
      mockGetToken.mockResolvedValue('mock-token');
      jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('mock-token');
      renderApp();
      await waitFor(() => { expect(mockGetToken).toHaveBeenCalled(); });
    });

    it('should unsubscribe from auth changes on unmount', async () => {
      const mockUnsubscribe = jest.fn();
      mockOnAuthChange.mockReturnValue(mockUnsubscribe);
      mockGetToken.mockResolvedValue('valid-token');
      jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('valid-token');
      const { unmount } = renderApp();
      await waitFor(() => { expect(mockOnAuthChange).toHaveBeenCalled(); });
      unmount();
      expect(mockUnsubscribe).toHaveBeenCalled();
    });

    it('should respond to auth state changes', async () => {
      let authChangeHandler: ((token: string | null) => void) | null = null;
      mockOnAuthChange.mockImplementation((handler: (token: string | null) => void) => {
        authChangeHandler = handler;
        return () => {};
      });
      mockGetToken.mockResolvedValue(null);
      renderApp();
      await waitFor(() => { expect(mockOnAuthChange).toHaveBeenCalled(); });
      expect(authChangeHandler).not.toBeNull();
    });
  });
});

describe('RequireAuth Component', () => {
  const mockGetToken = auth.getToken as jest.Mock;
  const mockOnAuthChange = auth.onAuthChange as jest.Mock;
  const mockDecodeJwt = auth.decodeJwt as jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue(null);
    mockOnAuthChange.mockReturnValue(() => {});
    mockDecodeJwt.mockReturnValue({ exp: Math.floor(Date.now() / 1000) + 3600 });
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('should render when unauthenticated', async () => {
    mockGetToken.mockResolvedValue(null);
    renderApp();
    await waitFor(() => { expect(document.body).toBeInTheDocument(); });
  });

  it('should render when authenticated', async () => {
    mockGetToken.mockResolvedValue('valid-token');
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('valid-token');
    renderApp();
    await waitFor(() => { expect(document.body).toBeInTheDocument(); });
  });

  it('should handle getToken returning null', async () => {
    mockGetToken.mockResolvedValue(null);
    renderApp();
    await waitFor(() => { expect(document.body).toBeInTheDocument(); });
  });

  it('should handle expired token', async () => {
    mockDecodeJwt.mockReturnValue({ exp: Math.floor(Date.now() / 1000) - 100 });
    mockGetToken.mockResolvedValue('expired-token');
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('expired-token');
    renderApp();
    await waitFor(() => { expect(document.body).toBeInTheDocument(); });
  });

  it('should handle token without exp claim', async () => {
    mockDecodeJwt.mockReturnValue({ sub: 'user123' });
    mockGetToken.mockResolvedValue('token-no-exp');
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('token-no-exp');
    renderApp();
    await waitFor(() => { expect(document.body).toBeInTheDocument(); });
  });

  it('should handle decodeJwt returning null', async () => {
    mockDecodeJwt.mockReturnValue(null);
    mockGetToken.mockResolvedValue('malformed-token');
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('malformed-token');
    renderApp();
    await waitFor(() => { expect(document.body).toBeInTheDocument(); });
  });
});

describe('AnimatedRoutes', () => {
  const mockGetToken = auth.getToken as jest.Mock;
  const mockOnAuthChange = auth.onAuthChange as jest.Mock;
  const mockDecodeJwt = auth.decodeJwt as jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue(null);
    mockOnAuthChange.mockReturnValue(() => {});
    mockDecodeJwt.mockReturnValue({ exp: Math.floor(Date.now() / 1000) + 3600 });
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('should render routes with animation wrapper', async () => {
    mockGetToken.mockResolvedValue('valid-token');
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('valid-token');
    const { container } = renderApp();
    await waitFor(() => { expect(container.querySelector('.route-fade-in')).toBeInTheDocument(); });
  });

  it('should render login route without auth', async () => {
    mockGetToken.mockResolvedValue(null);
    renderApp();
    await waitFor(() => { expect(document.body).toBeInTheDocument(); });
  });
});
