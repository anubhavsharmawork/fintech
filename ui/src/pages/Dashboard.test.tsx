import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Dashboard from './Dashboard';
import * as auth from '../auth';
import * as fModeHook from '../hooks/useFMode';

jest.mock('../auth');
jest.mock('../hooks/useFMode');

describe('Dashboard Page', () => {
  const mockToggle = jest.fn();
  const mockAuthFetch = auth.authFetch as jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    (fModeHook.useFMode as jest.Mock).mockReturnValue({
      enabled: false,
      toggle: mockToggle,
    });
    mockAuthFetch.mockImplementation((url: string) => {
      if (url === '/accounts') {
        return Promise.resolve({
          ok: true,
          json: async () => [
            { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: 1000, currency: 'NZD' },
            { id: '2', accountNumber: 'ACC002', accountType: 'Savings', balance: 5000, currency: 'NZD' },
          ],
        });
      }
      if (url === '/transactions') {
        return Promise.resolve({
          ok: true,
          json: async () => [
            { id: 't1', amount: 100, type: 'credit' },
            { id: 't2', amount: 50, type: 'debit' },
          ],
        });
      }
      return Promise.resolve({ ok: true, json: async () => ({}) });
    });
  });

  const renderDashboard = () => {
    return render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );
  };

  describe('Hero Section', () => {
    it('should render welcome message', async () => {
      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText('Welcome back')).toBeInTheDocument();
      });
    });

    it('should show Overview badge when fMode disabled', async () => {
      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText('Overview')).toBeInTheDocument();
      });
    });

    it('should show F-Mode badge when fMode enabled', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText('F-Mode (DeFi)')).toBeInTheDocument();
      });
    });

    it('should have aria-label for Overview section', async () => {
      renderDashboard();

      await waitFor(() => {
        expect(screen.getByRole('region', { name: 'Overview' })).toBeInTheDocument();
      });
    });
  });

  describe('Loading State', () => {
    it('should show loading indicator while fetching data', async () => {
      mockAuthFetch.mockImplementation(() => new Promise(() => {}));

      renderDashboard();

      expect(screen.getByText(/loading/i)).toBeInTheDocument();
    });
  });

  describe('Account Data', () => {
    it('should fetch accounts on mount', async () => {
      renderDashboard();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/accounts');
      });
    });

    it('should display total balance from all accounts', async () => {
      renderDashboard();

      await waitFor(() => {
        // Total is $6,000 (1000 + 5000)
        expect(screen.getByText(/\$6,000/)).toBeInTheDocument();
      });
    });

    it('should format balance as NZD currency', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [
              { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: 1234.56, currency: 'NZD' },
            ],
          });
        }
        if (url === '/transactions') {
          return Promise.resolve({ ok: true, json: async () => [] });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText(/\$1,234\.56/)).toBeInTheDocument();
      });
    });

    it('should handle zero balance', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [
              { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: 0, currency: 'NZD' },
            ],
          });
        }
        if (url === '/transactions') {
          return Promise.resolve({ ok: true, json: async () => [] });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText(/\$0/)).toBeInTheDocument();
      });
    });
  });

  describe('Transaction Data', () => {
    it('should fetch transactions on mount', async () => {
      renderDashboard();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/transactions');
      });
    });

    it('should display transaction count', async () => {
      renderDashboard();

      await waitFor(() => {
        // 2 transactions
        expect(screen.getByText(/2/)).toBeInTheDocument();
      });
    });

    it('should handle empty transactions array', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [
              { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: 1000, currency: 'NZD' },
            ],
          });
        }
        if (url === '/transactions') {
          return Promise.resolve({ ok: true, json: async () => [] });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderDashboard();

      await waitFor(() => {
        expect(screen.queryByText(/loading/i)).not.toBeInTheDocument();
      });
    });
  });

  describe('Error Handling', () => {
    it('should show error when accounts fetch fails', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({ ok: false, status: 500 });
        }
        if (url === '/transactions') {
          return Promise.resolve({ ok: true, json: async () => [] });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText(/failed to load accounts/i)).toBeInTheDocument();
      });
    });

    it('should show error when transactions fetch fails', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [{ id: '1', balance: 1000 }],
          });
        }
        if (url === '/transactions') {
          return Promise.resolve({ ok: false, status: 500 });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText(/failed to load transactions/i)).toBeInTheDocument();
      });
    });

    it('should handle network errors gracefully', async () => {
      mockAuthFetch.mockRejectedValue(new Error('Network error'));

      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText(/failed to load dashboard/i)).toBeInTheDocument();
      });
    });
  });

  describe('F-Mode (Crypto)', () => {
    beforeEach(() => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });
    });

    it('should show crypto-specific content when fMode enabled', async () => {
      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText('F-Mode (DeFi)')).toBeInTheDocument();
      });
    });

    it('should still fetch account data in fMode', async () => {
      renderDashboard();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/accounts');
      });
    });
  });

  describe('BalanceCard Integration', () => {
    it('should render BalanceCard with total balance', async () => {
      renderDashboard();

      await waitFor(() => {
        expect(screen.getByRole('region', { name: 'Total Balance Overview' })).toBeInTheDocument();
      });
    });
  });

  describe('Concurrent API Calls', () => {
    it('should fetch accounts and transactions concurrently', async () => {
      const accountsPromise = new Promise((resolve) => {
        setTimeout(() => resolve({
          ok: true,
          json: async () => [{ id: '1', balance: 1000 }],
        }), 50);
      });
      
      const transactionsPromise = new Promise((resolve) => {
        setTimeout(() => resolve({
          ok: true,
          json: async () => [{ id: 't1' }],
        }), 50);
      });

      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return accountsPromise;
        if (url === '/transactions') return transactionsPromise;
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderDashboard();

      // Both should be called almost simultaneously
      expect(mockAuthFetch).toHaveBeenCalledWith('/accounts');
      expect(mockAuthFetch).toHaveBeenCalledWith('/transactions');
    });
  });

  describe('Null/Undefined Balance Handling', () => {
    it('should treat undefined balance as zero', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [
              { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: undefined, currency: 'NZD' },
            ],
          });
        }
        if (url === '/transactions') {
          return Promise.resolve({ ok: true, json: async () => [] });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderDashboard();

      await waitFor(() => {
        expect(screen.getByText(/\$0/)).toBeInTheDocument();
      });
    });

    it('should handle non-array transaction response', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [{ id: '1', balance: 1000 }],
          });
        }
        if (url === '/transactions') {
          return Promise.resolve({
            ok: true,
            json: async () => ({ data: [] }), // Object instead of array
          });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderDashboard();

      await waitFor(() => {
        // Should handle gracefully, count as 0
        expect(screen.queryByText(/loading/i)).not.toBeInTheDocument();
      });
    });
  });
});
