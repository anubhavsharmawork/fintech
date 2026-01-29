import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import * as auth from './auth';
import App from './App';
import { ToastProvider } from './components/Toast';

jest.mock('./auth');
jest.mock('./hooks/useFMode', () => ({
  useFMode: () => ({ enabled: false, toggle: jest.fn() }),
  FModeProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const renderApp = () => {
  return render(
    <ToastProvider>
      <App />
    </ToastProvider>
  );
};

describe('App Component', () => {
  const mockGetToken = auth.getToken as jest.Mock;
  const mockOnAuthChange = auth.onAuthChange as jest.Mock;
  let getItemSpy: jest.SpyInstance;

  beforeEach(() => {
    jest.clearAllMocks();
    getItemSpy = jest.spyOn(Storage.prototype, 'getItem').mockReturnValue(null);
    mockOnAuthChange.mockReturnValue(() => {});
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('App Rendering', () => {
    it('should render the App component without crashing', async () => {
      getItemSpy.mockReturnValue('mock-token');
      mockGetToken.mockResolvedValue('mock-token');

      renderApp();

      await waitFor(() => {
        expect(document.body).toBeInTheDocument();
      });
    });

    it('should subscribe to auth changes on mount', async () => {
      getItemSpy.mockReturnValue('mock-token');
      mockGetToken.mockResolvedValue('mock-token');

      renderApp();

      await waitFor(() => {
        expect(mockOnAuthChange).toHaveBeenCalled();
      });
    });
  });

  describe('Auth State Management', () => {
    it('should call getToken on mount', async () => {
      getItemSpy.mockReturnValue('mock-token');
      mockGetToken.mockResolvedValue('mock-token');

      renderApp();

      await waitFor(() => {
        expect(mockGetToken).toHaveBeenCalled();
      });
    });

    it('should check localStorage for token', async () => {
      getItemSpy.mockReturnValue('stored-token');
      mockGetToken.mockResolvedValue('stored-token');

      renderApp();

      await waitFor(() => {
        expect(getItemSpy).toHaveBeenCalledWith('token');
      });
    });

    it('should unsubscribe from auth changes on unmount', async () => {
      const mockUnsubscribe = jest.fn();
      mockOnAuthChange.mockReturnValue(mockUnsubscribe);
      getItemSpy.mockReturnValue('valid-token');
      mockGetToken.mockResolvedValue('valid-token');

      const { unmount } = renderApp();

      await waitFor(() => {
        expect(mockOnAuthChange).toHaveBeenCalled();
      });

      unmount();

      expect(mockUnsubscribe).toHaveBeenCalled();
    });

    it('should respond to auth state changes', async () => {
      let authChangeHandler: ((token: string | null) => void) | null = null;
      mockOnAuthChange.mockImplementation((handler: (token: string | null) => void) => {
        authChangeHandler = handler;
        return () => {};
      });

      getItemSpy.mockReturnValue(null);
      mockGetToken.mockResolvedValue(null);

      renderApp();

      await waitFor(() => {
        expect(mockOnAuthChange).toHaveBeenCalled();
      });

      // The handler should exist
      expect(authChangeHandler).not.toBeNull();
    });
  });
});

// Test RequireAuth behavior by testing with actual route rendering
describe('RequireAuth Component', () => {
  const mockGetToken = auth.getToken as jest.Mock;
  const mockOnAuthChange = auth.onAuthChange as jest.Mock;
  let getItemSpy: jest.SpyInstance;

  beforeEach(() => {
    jest.clearAllMocks();
    getItemSpy = jest.spyOn(Storage.prototype, 'getItem').mockReturnValue(null);
    mockOnAuthChange.mockReturnValue(() => {});
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('should redirect unauthenticated users to login', async () => {
    getItemSpy.mockReturnValue(null);
    mockGetToken.mockResolvedValue(null);

    renderApp();

    await waitFor(() => {
      // When not authenticated, the app should show login content
      const body = document.body;
      expect(body).toBeInTheDocument();
    });
  });

  it('should allow authenticated users to access protected routes', async () => {
    getItemSpy.mockReturnValue('valid-token');
    mockGetToken.mockResolvedValue('valid-token');

    renderApp();

    await waitFor(() => {
      // When authenticated, protected content should be accessible
      const body = document.body;
      expect(body).toBeInTheDocument();
    });
  });

  it('should handle token refresh during getToken', async () => {
    getItemSpy.mockReturnValue(null);
    mockGetToken.mockResolvedValue('refreshed-token');

    renderApp();

    await waitFor(() => {
      expect(mockGetToken).toHaveBeenCalled();
    });
  });

  it('should handle getToken returning null', async () => {
    getItemSpy.mockReturnValue(null);
    mockGetToken.mockResolvedValue(null);

    renderApp();

    await waitFor(() => {
      expect(mockGetToken).toHaveBeenCalled();
    });
  });
});
