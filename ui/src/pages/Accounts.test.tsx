import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Accounts from './Accounts';
import * as fModeHook from '../hooks/useFMode';

jest.mock('../hooks/useFMode');
jest.mock('../services/crypto', () => ({
  getETHBalance: jest.fn().mockResolvedValue('1.0'),
  getFTKBalance: jest.fn().mockResolvedValue('100.0'),
  connectWallet: jest.fn(),
  sendFTKTransfer: jest.fn(),
}));

describe('Accounts Page', () => {
  const mockToggle = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('mock-token');
    (global.fetch as jest.Mock).mockClear();
    (fModeHook.useFMode as jest.Mock).mockReturnValue({
      enabled: false,
      toggle: mockToggle,
    });
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  const renderAccounts = () => {
    return render(
      <BrowserRouter>
        <Accounts />
      </BrowserRouter>
    );
  };

  describe('Page Header', () => {
    it('should render "My Accounts" title when fMode disabled', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'My Accounts' })).toBeInTheDocument();
      });
    });

    it('should render "Crypto Wallet" title when fMode enabled', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Crypto Wallet' })).toBeInTheDocument();
      });
    });
  });

  describe('Loading State', () => {
    it('should show loading text while fetching accounts', async () => {
      (global.fetch as jest.Mock).mockImplementation(() => new Promise(() => {}));

      renderAccounts();

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('should not show loading when fMode enabled', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      renderAccounts();

      expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
    });
  });

  describe('Account List', () => {
    it('should display accounts after successful fetch', async () => {
      const mockAccounts = [
        { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: 1000, currency: 'NZD' },
        { id: '2', accountNumber: 'ACC002', accountType: 'Savings', balance: 5000, currency: 'NZD' },
      ];

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => mockAccounts,
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByText('Checking Account')).toBeInTheDocument();
        expect(screen.getByText('Savings Account')).toBeInTheDocument();
      });
    });

    it('should display account balances formatted as NZD', async () => {
      const mockAccounts = [
        { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: 1234.56, currency: 'NZD' },
      ];

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => mockAccounts,
      });

      renderAccounts();

      await waitFor(() => {
        const balanceElements = screen.getAllByText('$1,234.56');
        expect(balanceElements.length).toBeGreaterThan(0);
      });
    });

    it('should display "No accounts yet" when no accounts exist', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByText('No accounts yet.')).toBeInTheDocument();
      });
    });

    it('should display account numbers', async () => {
      const mockAccounts = [
        { id: '1', accountNumber: 'ACC12345', accountType: 'Checking', balance: 1000, currency: 'NZD' },
      ];

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => mockAccounts,
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByText('ACC12345')).toBeInTheDocument();
      });
    });

    it('should display N/A for null account numbers', async () => {
      const mockAccounts = [
        { id: '1', accountNumber: null, accountType: 'Checking', balance: 1000, currency: 'NZD' },
      ];

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => mockAccounts,
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByText('N/A')).toBeInTheDocument();
      });
    });
  });

  describe('Error Handling', () => {
    it('should display error message on fetch failure', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByText(/Failed to load accounts/)).toBeInTheDocument();
      });
    });

    it('should display error when network fails', async () => {
      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });
    });
  });

  describe('Create Account Form', () => {
    beforeEach(() => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => [],
      });
    });

    it('should render create account form when fMode disabled', async () => {
      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Create Account' })).toBeInTheDocument();
      });
    });

    it('should have account type dropdown with Checking and Savings options', async () => {
      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('combobox')).toBeInTheDocument();
        expect(screen.getByRole('option', { name: 'Checking' })).toBeInTheDocument();
        expect(screen.getByRole('option', { name: 'Savings' })).toBeInTheDocument();
      });
    });

    it('should have initial deposit input field', async () => {
      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('spinbutton')).toBeInTheDocument();
      });
    });

    it('should submit form and reload accounts on success', async () => {
      const mockAccounts = [
        { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: 100, currency: 'NZD' },
      ];

      (global.fetch as jest.Mock)
        .mockResolvedValueOnce({ ok: true, json: async () => [] }) // Initial load
        .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 'new' }) }) // Create
        .mockResolvedValueOnce({ ok: true, json: async () => mockAccounts }); // Reload

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add New Account' })).toBeInTheDocument();
      });

      const depositInput = screen.getByRole('spinbutton');
      fireEvent.change(depositInput, { target: { value: '100' } });

      const submitButton = screen.getByRole('button', { name: 'Add New Account' });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          '/accounts',
          expect.objectContaining({
            method: 'POST',
            body: expect.stringContaining('Checking'),
          })
        );
      });
    });

    it('should change account type when dropdown value changes', async () => {
      (global.fetch as jest.Mock)
        .mockResolvedValueOnce({ ok: true, json: async () => [] })
        .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 'new' }) })
        .mockResolvedValueOnce({ ok: true, json: async () => [] });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('combobox')).toBeInTheDocument();
      });

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: 'Savings' } });

      const submitButton = screen.getByRole('button', { name: 'Add New Account' });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          '/accounts',
          expect.objectContaining({
            body: expect.stringContaining('Savings'),
          })
        );
      });
    });

    it('should show "Creating..." text when form is submitting', async () => {
      (global.fetch as jest.Mock)
        .mockResolvedValueOnce({ ok: true, json: async () => [] })
        .mockImplementation(() => new Promise(() => {})); // Never resolves

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add New Account' })).toBeInTheDocument();
      });

      const submitButton = screen.getByRole('button', { name: 'Add New Account' });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Creating...' })).toBeInTheDocument();
      });
    });

    it('should show error when account creation fails', async () => {
      (global.fetch as jest.Mock)
        .mockResolvedValueOnce({ ok: true, json: async () => [] })
        .mockResolvedValueOnce({ ok: false, status: 400 });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add New Account' })).toBeInTheDocument();
      });

      const submitButton = screen.getByRole('button', { name: 'Add New Account' });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/Failed to create account/)).toBeInTheDocument();
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

    it('should not show create account form when fMode enabled', () => {
      renderAccounts();

      expect(screen.queryByRole('heading', { name: 'Create Account' })).not.toBeInTheDocument();
    });

    it('should not show account list when fMode enabled', () => {
      renderAccounts();

      expect(screen.queryByText('Your Accounts')).not.toBeInTheDocument();
    });
  });

  describe('BalanceCard Integration', () => {
    it('should show BalanceCard with total balance when accounts loaded', async () => {
      const mockAccounts = [
        { id: '1', accountNumber: 'ACC001', accountType: 'Checking', balance: 3000, currency: 'NZD' },
        { id: '2', accountNumber: 'ACC002', accountType: 'Savings', balance: 5000, currency: 'NZD' },
      ];

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => mockAccounts,
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByRole('region', { name: 'Total Balance Overview' })).toBeInTheDocument();
      });
    });

    it('should not show BalanceCard when no accounts', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      renderAccounts();

      await waitFor(() => {
        expect(screen.getByText('No accounts yet.')).toBeInTheDocument();
      });

      expect(screen.queryByRole('region', { name: 'Total Balance Overview' })).not.toBeInTheDocument();
    });
  });

  describe('Authorization Header', () => {
    it('should include token in authorization header', async () => {
      jest.spyOn(Storage.prototype, 'getItem').mockReturnValue('test-token-123');
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      renderAccounts();

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          '/accounts',
          expect.objectContaining({
            headers: expect.objectContaining({
              Authorization: 'Bearer test-token-123',
            }),
          })
        );
      });
    });

    it('should include empty bearer when no token', async () => {
      jest.spyOn(Storage.prototype, 'getItem').mockReturnValue(null);
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => [],
      });

      renderAccounts();

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          '/accounts',
          expect.objectContaining({
            headers: expect.objectContaining({
              Authorization: '',
            }),
          })
        );
      });
    });
  });
});
