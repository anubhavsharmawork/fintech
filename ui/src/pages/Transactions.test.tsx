import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Transactions from './Transactions';
import * as auth from '../auth';
import * as fModeHook from '../hooks/useFMode';
import * as cryptoService from '../services/crypto';
import * as exportService from '../services/exportTransactions';
import { ToastProvider } from '../components/Toast';

jest.mock('../auth');
jest.mock('../hooks/useFMode');
jest.mock('../services/crypto');
jest.mock('../services/exportTransactions');

// Mock Recharts to avoid rendering issues in tests
jest.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  AreaChart: ({ children }: { children: React.ReactNode }) => <div data-testid="area-chart">{children}</div>,
  Area: () => <div data-testid="area" />,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

describe('Transactions Page', () => {
  const mockToggle = jest.fn();
  const mockAuthFetch = auth.authFetch as jest.Mock;
  const mockSendFTKTransfer = cryptoService.sendFTKTransfer as jest.Mock;

  const mockAccounts = [
    { id: 'acc1', accountNumber: 'ACC001', accountType: 'Checking', balance: 1000 },
    { id: 'acc2', accountNumber: 'ACC002', accountType: 'Savings', balance: 5000 },
  ];

  const mockPayees = [
    { id: 'payee1', name: 'Demo User', accountNumber: 'PAY001' },
    { id: 'payee2', name: 'John Doe', accountNumber: 'PAY002' },
  ];

  const mockTransactions = [
    { id: 't1', accountId: 'acc1', amount: 100, currency: 'NZD', type: 'credit', description: 'Deposit', createdAt: '2024-01-15T10:00:00Z', spendingType: 'Future' },
    { id: 't2', accountId: 'acc1', amount: 50, currency: 'NZD', type: 'debit', description: 'Coffee', createdAt: '2024-01-14T10:00:00Z', spendingType: 'Fun' },
  ];

  const mockUsers = [
    { id: 'user1', email: 'user@example.com', firstName: 'Test', lastName: 'User' },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    (fModeHook.useFMode as jest.Mock).mockReturnValue({
      enabled: false,
      toggle: mockToggle,
    });
    mockAuthFetch.mockImplementation((url: string) => {
      if (url === '/accounts') {
        return Promise.resolve({ ok: true, json: async () => mockAccounts });
      }
      if (url === '/payees') {
        return Promise.resolve({ ok: true, json: async () => mockPayees });
      }
      if (url === '/transactions') {
        return Promise.resolve({ ok: true, json: async () => mockTransactions });
      }
      if (url === '/users/all') {
        return Promise.resolve({ ok: true, json: async () => mockUsers });
      }
      if (url === '/payments') {
        return Promise.resolve({ ok: true, json: async () => ({ id: 'new-payment' }) });
      }
      return Promise.resolve({ ok: true, json: async () => ({}) });
    });
  });

  const renderTransactions = () => {
    return render(
      <BrowserRouter>
        <ToastProvider>
          <Transactions />
        </ToastProvider>
      </BrowserRouter>
    );
  };

  describe('Page Header', () => {
    it('should render Transactions heading', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Transactions' })).toBeInTheDocument();
      });
    });
  });

  describe('Tab Navigation', () => {
    it('should render Send Money tab when fMode disabled', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Send Money' })).toBeInTheDocument();
      });
    });

    it('should render Add Payee tab when fMode disabled', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add Payee' })).toBeInTheDocument();
      });
    });

    it('should render History tab', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });
    });

    it('should render Crypto Transfer tab when fMode enabled', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Crypto Transfer' })).toBeInTheDocument();
      });
    });

    it('should not render Add Payee tab when fMode enabled', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.queryByRole('button', { name: 'Add Payee' })).not.toBeInTheDocument();
      });
    });

    it('should switch to History tab when clicked', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        expect(screen.getByText(/15\/01\/2024|Jan 15/)).toBeInTheDocument();
      });
    });

    it('should switch to Add Payee tab when clicked', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add Payee' })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: 'Add Payee' }));

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Add Payee' })).toBeInTheDocument();
      });
    });
  });

  describe('Send Money Form', () => {
    it('should render from account dropdown', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/from account/i)).toBeInTheDocument();
      });
    });

    it('should render to payee dropdown', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/to payee/i)).toBeInTheDocument();
      });
    });

    it('should render description input', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/description/i)).toBeInTheDocument();
      });
    });

    it('should render spending type dropdown', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/spending type/i)).toBeInTheDocument();
      });
    });

    it('should have spending type options', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('option', { name: /fun/i })).toBeInTheDocument();
        expect(screen.getByRole('option', { name: /fixed/i })).toBeInTheDocument();
        expect(screen.getByRole('option', { name: /future/i })).toBeInTheDocument();
      });
    });

    it('should populate accounts in dropdown', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByText(/Checking - ACC001/)).toBeInTheDocument();
        expect(screen.getByText(/Savings - ACC002/)).toBeInTheDocument();
      });
    });

    it('should populate payees in dropdown', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByText(/Demo User - PAY001/)).toBeInTheDocument();
      });
    });

    it('should show error when spending type not selected', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^send payment/i })).toBeInTheDocument();
      });

      // Try to submit without selecting spending type
      fireEvent.click(screen.getByRole('button', { name: /^send payment/i }));

      await waitFor(() => {
        expect(screen.getByText(/select a conscious spending type/i)).toBeInTheDocument();
      });
    });

    it('should send payment successfully', async () => {
      localStorage.setItem('lastSpendingType', 'Fun');

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/from account/i)).toBeInTheDocument();
      });

      // Fill in amount (if there's an amount field)
      const amountInput = screen.queryByLabelText(/amount/i);
      if (amountInput) {
        fireEvent.change(amountInput, { target: { value: '50' } });
      }

      const sendButton = screen.getByRole('button', { name: /^send payment/i });
      fireEvent.click(sendButton);

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/payments', expect.objectContaining({
          method: 'POST',
        }));
      });
    });

    it('should show error when amount is invalid', async () => {
      localStorage.setItem('lastSpendingType', 'Fun');

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^send payment/i })).toBeInTheDocument();
      });

      const amountInput = screen.queryByLabelText(/amount/i);
      if (amountInput) {
        fireEvent.change(amountInput, { target: { value: '0' } });
      }

      fireEvent.click(screen.getByRole('button', { name: /^send payment/i }));

      await waitFor(() => {
        expect(screen.getByText(/valid amount/i)).toBeInTheDocument();
      });
    });

    it('should show Sending... while processing', async () => {
      localStorage.setItem('lastSpendingType', 'Fun');
      
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({ ok: true, json: async () => mockAccounts });
        }
        if (url === '/payees') {
          return Promise.resolve({ ok: true, json: async () => mockPayees });
        }
        if (url === '/transactions') {
          return Promise.resolve({ ok: true, json: async () => mockTransactions });
        }
        if (url === '/users/all') {
          return Promise.resolve({ ok: true, json: async () => mockUsers });
        }
        if (url === '/payments') {
          return new Promise(() => {}); // Never resolves
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^send payment/i })).toBeInTheDocument();
      });

      const amountInput = screen.queryByLabelText(/amount/i);
      if (amountInput) {
        fireEvent.change(amountInput, { target: { value: '50' } });
      }

      fireEvent.click(screen.getByRole('button', { name: /^send payment/i }));

      await waitFor(() => {
        expect(screen.getByText(/sending/i)).toBeInTheDocument();
      });
    });
  });

  describe('Add Payee Form', () => {
    beforeEach(async () => {
      renderTransactions();
      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add Payee' })).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: 'Add Payee' }));
    });

    it('should render Add Payee form', async () => {
      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Add Payee' })).toBeInTheDocument();
      });
    });

    it('should render user selection dropdown', async () => {
      await waitFor(() => {
        expect(screen.getByLabelText(/select existing user/i)).toBeInTheDocument();
      });
    });

    it('should populate users in dropdown', async () => {
      await waitFor(() => {
        expect(screen.getByText(/Test User/)).toBeInTheDocument();
      });
    });

    it('should auto-fill name when user selected', async () => {
      await waitFor(() => {
        expect(screen.getByLabelText(/select existing user/i)).toBeInTheDocument();
      });

      const userSelect = screen.getByLabelText(/select existing user/i);
      fireEvent.change(userSelect, { target: { value: 'user1' } });

      await waitFor(() => {
        const nameInput = screen.getByLabelText(/^Payee Name/i) as HTMLInputElement;
        expect(nameInput.value).toContain('Test');
      });
    });

    it('should submit payee successfully', async () => {
      mockAuthFetch.mockImplementation((url: string, options?: any) => {
        if (url === '/payees' && options?.method === 'POST') {
          return Promise.resolve({ ok: true, json: async () => ({ id: 'new-payee' }) });
        }
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mockTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      await waitFor(() => {
        expect(screen.getByLabelText(/^Payee Name/i)).toBeInTheDocument();
      });

      const nameInput = screen.getByLabelText(/^Payee Name/i);
      const accountInput = screen.getByLabelText(/account number/i);

      fireEvent.change(nameInput, { target: { value: 'New Payee' } });
      fireEvent.change(accountInput, { target: { value: 'NEW123' } });

      // Get the submit button (second Add Payee button - the form submit, not the tab)
      const addButtons = screen.getAllByRole('button', { name: /Add Payee/i });
      const submitButton = addButtons.find(btn => btn.getAttribute('type') === 'submit') || addButtons[addButtons.length - 1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/payees', expect.objectContaining({
          method: 'POST',
        }));
      });
    });

    it('should show error when payee creation fails', async () => {
      mockAuthFetch.mockImplementation((url: string, options?: any) => {
        if (url === '/payees' && options?.method === 'POST') {
          return Promise.resolve({ ok: false });
        }
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mockTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      await waitFor(() => {
        expect(screen.getByLabelText(/^Payee Name/i)).toBeInTheDocument();
      });

      const nameInput = screen.getByLabelText(/^Payee Name/i);
      const accountInput = screen.getByLabelText(/account number/i);

      fireEvent.change(nameInput, { target: { value: 'New Payee' } });
      fireEvent.change(accountInput, { target: { value: 'NEW123' } });

      // Get the submit button (second Add Payee button - the form submit, not the tab)
      const addButtons = screen.getAllByRole('button', { name: /Add Payee/i });
      const submitButton = addButtons.find(btn => btn.getAttribute('type') === 'submit') || addButtons[addButtons.length - 1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/failed to add payee/i)).toBeInTheDocument();
      });
    });
  });

  describe('Transaction History', () => {
    beforeEach(async () => {
      renderTransactions();
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: /^History/i }));
    });

    it('should display transaction list', async () => {
      await waitFor(() => {
        expect(screen.getByText('Deposit')).toBeInTheDocument();
        expect(screen.getByText('Coffee')).toBeInTheDocument();
      });
    });

    it('should display transaction amounts formatted as NZD', async () => {
      await waitFor(() => {
        expect(screen.getByText(/\$100/)).toBeInTheDocument();
        expect(screen.getByText(/\$50/)).toBeInTheDocument();
      });
    });

    it('should display transaction dates', async () => {
      await waitFor(() => {
        expect(screen.getByText(/15\/01\/2024|Jan 15|1\/15/)).toBeInTheDocument();
      });
    });

    it('should display spending type badges', async () => {
      await waitFor(() => {
        expect(screen.getByText(/Future/)).toBeInTheDocument();
        expect(screen.getByText(/Fun/)).toBeInTheDocument();
      });
    });
  });

  describe('Error Handling', () => {
    it('should display error when transactions fail to load', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/transactions') {
          return Promise.resolve({ ok: false, status: 500 });
        }
        return Promise.resolve({ ok: true, json: async () => [] });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByText(/failed to load transactions/i)).toBeInTheDocument();
      });
    });

    it('should display error when payment fails', async () => {
      localStorage.setItem('lastSpendingType', 'Fun');
      
      mockAuthFetch.mockImplementation((url: string, options?: any) => {
        if (url === '/payments' && options?.method === 'POST') {
          return Promise.resolve({ ok: false });
        }
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mockTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^send payment/i })).toBeInTheDocument();
      });

      const amountInput = screen.queryByLabelText(/amount/i);
      if (amountInput) {
        fireEvent.change(amountInput, { target: { value: '50' } });
      }

      fireEvent.click(screen.getByRole('button', { name: /^send payment/i }));

      await waitFor(() => {
        expect(screen.getByText(/failed to send payment/i)).toBeInTheDocument();
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

    it('should show FTK Token Transfer heading', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /ftk token transfer/i })).toBeInTheDocument();
      });
    });

    it('should show recipient wallet address input', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/recipient wallet address/i)).toBeInTheDocument();
      });
    });

    it('should show amount input for FTK', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/amount \(ftk\)/i)).toBeInTheDocument();
      });
    });

    it('should require wallet connection for transfer', async () => {
      renderTransactions();

      await waitFor(() => {
        const transferButton = screen.getByRole('button', { name: /transfer ftk/i });
        expect(transferButton).toBeDisabled();
      });
    });

    it('should switch to pay tab if on payee tab when fMode enabled', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: false,
        toggle: mockToggle,
      });

      const { rerender } = renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add Payee' })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: 'Add Payee' }));

      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      rerender(
        <BrowserRouter>
          <ToastProvider>
            <Transactions />
          </ToastProvider>
        </BrowserRouter>
      );

      await waitFor(() => {
        expect(screen.queryByRole('heading', { name: 'Add Payee' })).not.toBeInTheDocument();
      });
    });
  });

  describe('LocalStorage Persistence', () => {
    it('should persist spending type to localStorage', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/spending type/i)).toBeInTheDocument();
      });

      const spendingTypeSelect = screen.getByLabelText(/spending type/i);
      fireEvent.change(spendingTypeSelect, { target: { value: 'Fixed' } });

      expect(localStorage.getItem('lastSpendingType')).toBe('Fixed');
    });

    it('should restore spending type from localStorage', async () => {
      localStorage.setItem('lastSpendingType', 'Future');

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/spending type/i)).toBeInTheDocument();
      });

      const spendingTypeSelect = screen.getByLabelText(/spending type/i) as HTMLSelectElement;
      expect(spendingTypeSelect.value).toBe('Future');
    });
  });

  describe('API Integration', () => {
    it('should fetch accounts on mount', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/accounts');
      });
    });

    it('should fetch payees on mount', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/payees');
      });
    });

    it('should fetch transactions on mount', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/transactions');
      });
    });

    it('should fetch users on mount', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/users/all');
      });
    });
  });

  describe('Component Initialization', () => {
    it('should initialize with default tab', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Transactions' })).toBeInTheDocument();
      });
    });
  });

  describe('Crypto Transfer Extended', () => {
    beforeEach(() => {
      localStorage.setItem('lastSpendingType', 'Fun');
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });
    });

    it('should show error when wallet not connected', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/recipient wallet address/i)).toBeInTheDocument();
      });

      const recipientInput = screen.getByLabelText(/recipient wallet address/i);
      fireEvent.change(recipientInput, { target: { value: '0x1234567890abcdef' } });

      const amountInput = screen.getByLabelText(/amount \(ftk\)/i);
      fireEvent.change(amountInput, { target: { value: '10' } });

      // Button should be disabled when wallet not connected
      const transferButton = screen.getByRole('button', { name: /transfer ftk/i });
      expect(transferButton).toBeDisabled();
    });

    it('should show crypto mode banner', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByText(/f-mode|crypto mode/i)).toBeInTheDocument();
      });
    });

    it('should show connect wallet component', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByText(/connect.*wallet/i)).toBeInTheDocument();
      });
    });

    it('should show spending type selector in crypto mode', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/spending type/i)).toBeInTheDocument();
      });
    });
  });

  describe('History Filtering', () => {
    it('should show no transactions message when empty', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => [] });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        expect(screen.getByText(/no transactions/i)).toBeInTheDocument();
      });
    });

    it('should filter transactions based on fMode', async () => {
      const mixedTransactions = [
        { id: 't1', accountId: 'acc1', amount: 100, currency: 'NZD', type: 'credit', description: 'Fiat Tx', createdAt: '2024-01-15T10:00:00Z' },
        { id: 't2', accountId: 'acc1', amount: 50, currency: 'FTK', type: 'debit', description: 'Crypto Tx', createdAt: '2024-01-14T10:00:00Z' },
      ];

      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mixedTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        // In non-fMode, should show fiat transactions only
        expect(screen.getByText('Fiat Tx')).toBeInTheDocument();
        expect(screen.queryByText('Crypto Tx')).not.toBeInTheDocument();
      });
    });

    it('should show FTK transactions in fMode', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      const mixedTransactions = [
        { id: 't1', accountId: 'acc1', amount: 100, currency: 'NZD', type: 'credit', description: 'Fiat Tx', createdAt: '2024-01-15T10:00:00Z' },
        { id: 't2', accountId: 'acc1', amount: 50, currency: 'FTK', type: 'debit', description: 'Crypto Tx', createdAt: '2024-01-14T10:00:00Z' },
      ];

      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mixedTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        // In fMode, should show FTK transactions only
        expect(screen.getByText('Crypto Tx')).toBeInTheDocument();
        expect(screen.queryByText('Fiat Tx')).not.toBeInTheDocument();
      });
    });

    it('should show message when no matching mode transactions', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      const fiatOnlyTransactions = [
        { id: 't1', accountId: 'acc1', amount: 100, currency: 'NZD', type: 'credit', description: 'Fiat Tx', createdAt: '2024-01-15T10:00:00Z' },
      ];

      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => fiatOnlyTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        expect(screen.getByText(/switch modes/i)).toBeInTheDocument();
      });
    });

    it('should display transaction type badge', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        expect(screen.getByText(/CREDIT/)).toBeInTheDocument();
        expect(screen.getByText(/DEBIT/)).toBeInTheDocument();
      });
    });

    it('should show loading state', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/transactions') return new Promise(() => {}); // Never resolves
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        expect(screen.getByText(/loading/i)).toBeInTheDocument();
      });
    });
  });

  describe('Add Payee Extended', () => {
    it('should clear form after successful submission', async () => {
      mockAuthFetch.mockImplementation((url: string, options?: any) => {
        if (url === '/payees' && options?.method === 'POST') {
          return Promise.resolve({ ok: true, json: async () => ({ id: 'new-payee' }) });
        }
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mockTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add Payee' })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: 'Add Payee' }));

      await waitFor(() => {
        expect(screen.getByLabelText(/^Payee Name/i)).toBeInTheDocument();
      });

      const nameInput = screen.getByLabelText(/^Payee Name/i) as HTMLInputElement;
      const accountInput = screen.getByLabelText(/account number/i) as HTMLInputElement;

      fireEvent.change(nameInput, { target: { value: 'Test Payee' } });
      fireEvent.change(accountInput, { target: { value: 'ACC123456789' } });

      // Get the submit button (second Add Payee button - the form submit, not the tab)
      const addButtons = screen.getAllByRole('button', { name: /Add Payee/i });
      const submitButton = addButtons.find(btn => btn.getAttribute('type') === 'submit') || addButtons[addButtons.length - 1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(nameInput.value).toBe('');
      });
    });

    it('should auto-fill user email as fallback name', async () => {
      const usersWithoutNames = [
        { id: 'user1', email: 'fallback@example.com', firstName: '', lastName: '' },
      ];

      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mockTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => usersWithoutNames });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Add Payee' })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: 'Add Payee' }));

      await waitFor(() => {
        expect(screen.getByLabelText(/select existing user/i)).toBeInTheDocument();
      });

      const userSelect = screen.getByLabelText(/select existing user/i);
      fireEvent.change(userSelect, { target: { value: 'user1' } });

      await waitFor(() => {
        const nameInput = screen.getByLabelText(/^Payee Name/i) as HTMLInputElement;
        expect(nameInput.value).toBe('fallback@example.com');
      });
    });
  });

  describe('Send Payment Extended', () => {
    it('should show error when no payee selected in non-fMode', async () => {
      localStorage.setItem('lastSpendingType', 'Fun');

      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => [] }); // No payees
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mockTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^send payment/i })).toBeInTheDocument();
      });

      // Button should be disabled when no payees
      expect(screen.getByRole('button', { name: /^send payment/i })).toBeDisabled();
    });

    it('should disable send button when no accounts', async () => {
      localStorage.setItem('lastSpendingType', 'Fun');

      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => [] }); // No accounts
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mockTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /^send payment/i })).toBeInTheDocument();
      });

      // Button should be disabled when no accounts
      expect(screen.getByRole('button', { name: /^send payment/i })).toBeDisabled();
    });

    it('should update form description field', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/description/i)).toBeInTheDocument();
      });

      const descInput = screen.getByLabelText(/description/i) as HTMLInputElement;
      fireEvent.change(descInput, { target: { value: 'Test payment description' } });

      expect(descInput.value).toBe('Test payment description');
    });

    it('should change selected account', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/from account/i)).toBeInTheDocument();
      });

      const accountSelect = screen.getByLabelText(/from account/i) as HTMLSelectElement;
      fireEvent.change(accountSelect, { target: { value: 'acc2' } });

      expect(accountSelect.value).toBe('acc2');
    });

    it('should change selected payee', async () => {
      renderTransactions();

      await waitFor(() => {
        expect(screen.getByLabelText(/to payee/i)).toBeInTheDocument();
      });

      const payeeSelect = screen.getByLabelText(/to payee/i) as HTMLSelectElement;
      fireEvent.change(payeeSelect, { target: { value: 'payee2' } });

      expect(payeeSelect.value).toBe('payee2');
    });
  });

  describe('History filter and export branch coverage', () => {
    const mockExportCSV = exportService.exportCSV as jest.Mock;
    const mockExportPDF = exportService.exportPDF as jest.Mock;

    const mixedTransactions = [
      { id: 't1', accountId: 'acc1', amount: 100, currency: 'NZD', type: 'credit', description: 'Salary payment', createdAt: '2024-01-15T10:00:00Z', spendingType: 'Future' },
      { id: 't2', accountId: 'acc1', amount: 50, currency: 'NZD', type: 'debit', description: 'Coffee shop', createdAt: '2024-01-16T10:00:00Z', spendingType: 'Fun' },
    ];

    beforeEach(() => {
      mockExportCSV.mockResolvedValue(undefined);
      mockExportPDF.mockResolvedValue(undefined);
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mixedTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });
    });

    const openHistoryTab = async () => {
      renderTransactions();
      await waitFor(() => expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: /^History/i }));
      await waitFor(() => expect(screen.getByPlaceholderText('Search description...')).toBeInTheDocument());
    };

    it('filters by credit type', async () => {
      await openHistoryTab();
      const selects = screen.getAllByRole('combobox');
      const typeSelect = selects.find(s => (s as HTMLSelectElement).options[0]?.text === 'All Types');
      expect(typeSelect).toBeDefined();
      fireEvent.change(typeSelect!, { target: { value: 'credit' } });
      await waitFor(() => {
        expect(screen.getByText('Salary payment')).toBeInTheDocument();
        expect(screen.queryByText('Coffee shop')).not.toBeInTheDocument();
      });
    });

    it('filters by debit type', async () => {
      await openHistoryTab();
      const selects = screen.getAllByRole('combobox');
      const typeSelect = selects.find(s => (s as HTMLSelectElement).options[0]?.text === 'All Types');
      fireEvent.change(typeSelect!, { target: { value: 'debit' } });
      await waitFor(() => {
        expect(screen.getByText('Coffee shop')).toBeInTheDocument();
        expect(screen.queryByText('Salary payment')).not.toBeInTheDocument();
      });
    });

    it('filters by description search text', async () => {
      await openHistoryTab();
      const searchInput = screen.getByPlaceholderText('Search description...');
      fireEvent.change(searchInput, { target: { value: 'Coffee' } });
      await waitFor(() => {
        expect(screen.getByText('Coffee shop')).toBeInTheDocument();
        expect(screen.queryByText('Salary payment')).not.toBeInTheDocument();
      });
    });

    it('filters by from date', async () => {
      await openHistoryTab();
      const fromInput = screen.getByLabelText('From date');
      fireEvent.change(fromInput, { target: { value: '2024-01-16' } });
      await waitFor(() => {
        expect(screen.getByText('Coffee shop')).toBeInTheDocument();
        expect(screen.queryByText('Salary payment')).not.toBeInTheDocument();
      });
    });

    it('filters by to date', async () => {
      await openHistoryTab();
      const toInput = screen.getByLabelText('To date');
      fireEvent.change(toInput, { target: { value: '2024-01-15' } });
      await waitFor(() => {
        expect(screen.getByText('Salary payment')).toBeInTheDocument();
        expect(screen.queryByText('Coffee shop')).not.toBeInTheDocument();
      });
    });

    it('changes sort order to amount ascending', async () => {
      await openHistoryTab();
      const selects = screen.getAllByRole('combobox');
      const sortSelect = selects.find(s => (s as HTMLSelectElement).value === 'createdAt_desc');
      expect(sortSelect).toBeDefined();
      fireEvent.change(sortSelect!, { target: { value: 'amount_asc' } });
      await waitFor(() => {
        expect((sortSelect as HTMLSelectElement).value).toBe('amount_asc');
      });
    });

    it('opens and closes export dropdown', async () => {
      await openHistoryTab();
      const exportBtn = screen.getByRole('button', { name: /export/i });
      expect(exportBtn).not.toBeDisabled();
      fireEvent.click(exportBtn);
      await waitFor(() => expect(screen.getByText('Download CSV')).toBeInTheDocument());
      fireEvent.click(exportBtn);
      await waitFor(() => expect(screen.queryByText('Download CSV')).not.toBeInTheDocument());
    });

    it('calls exportCSV when Download CSV is clicked', async () => {
      await openHistoryTab();
      fireEvent.click(screen.getByRole('button', { name: /export/i }));
      await waitFor(() => expect(screen.getByText('Download CSV')).toBeInTheDocument());
      fireEvent.click(screen.getByText('Download CSV'));
      await waitFor(() => expect(mockExportCSV).toHaveBeenCalled());
    });

    it('calls exportPDF when Download PDF is clicked', async () => {
      await openHistoryTab();
      fireEvent.click(screen.getByRole('button', { name: /export/i }));
      await waitFor(() => expect(screen.getByText('Download PDF')).toBeInTheDocument());
      fireEvent.click(screen.getByText('Download PDF'));
      await waitFor(() => expect(mockExportPDF).toHaveBeenCalled());
    });

    it('export button is disabled when no transactions match filter', async () => {
      await openHistoryTab();
      const searchInput = screen.getByPlaceholderText('Search description...');
      fireEvent.change(searchInput, { target: { value: 'zzz_no_match_zzz' } });
      await waitFor(() => {
        const exportBtn = screen.getByRole('button', { name: /export/i });
        expect(exportBtn).toBeDisabled();
      });
    });

    it('filters by spending type', async () => {
      await openHistoryTab();
      const selects = screen.getAllByRole('combobox');
      const spendingSelect = selects.find(s => (s as HTMLSelectElement).options[0]?.text === 'All Spending Types');
      expect(spendingSelect).toBeDefined();
      fireEvent.change(spendingSelect!, { target: { value: 'Fun' } });
      await waitFor(() => {
        expect(screen.getByText('Coffee shop')).toBeInTheDocument();
        expect(screen.queryByText('Salary payment')).not.toBeInTheDocument();
      });
    });

    it('renders "—" when transaction has no spendingType', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => [
          { id: 't_no_cat', accountId: 'acc1', amount: 200, currency: 'NZD', type: 'credit', description: 'No Category', createdAt: '2024-01-17T10:00:00Z', spendingType: null },
        ]});
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();
      await waitFor(() => expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        expect(screen.getByText('No Category')).toBeInTheDocument();
        // The spending type cell renders '—' when spendingType is falsy
        expect(screen.getByText('—')).toBeInTheDocument();
      });
    });

    it('renders "Completed" status when transaction status is null', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => [
          { id: 't_no_status', accountId: 'acc1', amount: 75, currency: 'NZD', type: 'credit', description: 'No Status Tx', createdAt: '2024-01-18T10:00:00Z', spendingType: 'Fun', status: null },
        ]});
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();
      await waitFor(() => expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: /^History/i }));

      await waitFor(() => {
        expect(screen.getByText('No Status Tx')).toBeInTheDocument();
        expect(screen.getByText('Completed')).toBeInTheDocument();
      });
    });

    it('shows "No transactions match" message when filter reduces fiat results to zero but transactions exist', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') return Promise.resolve({ ok: true, json: async () => mockAccounts });
        if (url === '/payees') return Promise.resolve({ ok: true, json: async () => mockPayees });
        if (url === '/transactions') return Promise.resolve({ ok: true, json: async () => mixedTransactions });
        if (url === '/users/all') return Promise.resolve({ ok: true, json: async () => mockUsers });
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderTransactions();
      await waitFor(() => expect(screen.getByRole('button', { name: /^History/i })).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: /^History/i }));
      await waitFor(() => expect(screen.getByPlaceholderText('Search description...')).toBeInTheDocument());

      // Filter to a description that matches nothing
      const searchInput = screen.getByPlaceholderText('Search description...');
      fireEvent.change(searchInput, { target: { value: 'zzz_no_match_zzz' } });

      await waitFor(() => {
        expect(screen.getByText(/no transactions match the current filters/i)).toBeInTheDocument();
      });
    });

    it('changes sort order to amount descending', async () => {
      await openHistoryTab();
      const selects = screen.getAllByRole('combobox');
      const sortSelect = selects.find(s => (s as HTMLSelectElement).value === 'createdAt_desc');
      expect(sortSelect).toBeDefined();
      fireEvent.change(sortSelect!, { target: { value: 'amount_desc' } });
      await waitFor(() => {
        expect((sortSelect as HTMLSelectElement).value).toBe('amount_desc');
      });
    });

    it('changes sort order to date ascending', async () => {
      await openHistoryTab();
      const selects = screen.getAllByRole('combobox');
      const sortSelect = selects.find(s => (s as HTMLSelectElement).value === 'createdAt_desc');
      expect(sortSelect).toBeDefined();
      fireEvent.change(sortSelect!, { target: { value: 'createdAt_asc' } });
      await waitFor(() => {
        expect((sortSelect as HTMLSelectElement).value).toBe('createdAt_asc');
      });
    });

    it('closes export dropdown when clicking outside', async () => {
      await openHistoryTab();
      const exportBtn = screen.getByRole('button', { name: /export/i });
      fireEvent.click(exportBtn);
      await waitFor(() => expect(screen.getByText('Download CSV')).toBeInTheDocument());

      // Click outside the export menu
      fireEvent.mouseDown(document.body);

      await waitFor(() => {
        expect(screen.queryByText('Download CSV')).not.toBeInTheDocument();
      });
    });
  });
});




