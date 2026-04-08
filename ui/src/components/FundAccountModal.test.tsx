import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import FundAccountModal from './FundAccountModal';
import * as bankingService from '../services/banking';

jest.mock('../services/banking');

describe('FundAccountModal Component', () => {
  const mockGetExternalAccounts = bankingService.getExternalAccounts as jest.Mock;
  const mockDepositFromExternal = bankingService.depositFromExternal as jest.Mock;
  const mockOnComplete = jest.fn();
  const mockOnCancel = jest.fn();

  const defaultProps = {
    accountId: 'acc-123',
    accountType: 'Checking',
    currency: 'NZD',
    onComplete: mockOnComplete,
    onCancel: mockOnCancel,
  };

  const mockExternalAccounts = [
    {
      id: 'ext-1',
      accountName: 'Everyday',
      accountType: 'checking',
      accountNumber: '12-3456-7890123-00',
      balance: 5000,
      currency: 'NZD',
      lastSyncedAt: '2024-01-15T12:00:00Z',
      bankName: 'ANZ',
      bankLogo: '🏦',
    },
    {
      id: 'ext-2',
      accountName: 'Savings',
      accountType: 'savings',
      accountNumber: '12-3456-7890123-01',
      balance: 15000,
      currency: 'NZD',
      lastSyncedAt: '2024-01-15T12:00:00Z',
      bankName: 'ASB',
      bankLogo: '🏛️',
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    mockGetExternalAccounts.mockResolvedValue(mockExternalAccounts);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('Loading State', () => {
    it('should show loading indicator while fetching external accounts', () => {
      mockGetExternalAccounts.mockImplementation(() => new Promise(() => {}));
      render(<FundAccountModal {...defaultProps} />);
      expect(screen.getByText(/Loading external accounts/)).toBeInTheDocument();
    });
  });

  describe('No External Accounts', () => {
    it('should display message when no external accounts are connected', async () => {
      mockGetExternalAccounts.mockResolvedValue([]);
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText(/No external bank accounts found/)).toBeInTheDocument();
      });
    });

    it('should show close button when no external accounts', async () => {
      mockGetExternalAccounts.mockResolvedValue([]);
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        const closeBtn = screen.getByRole('button', { name: /Close/i });
        expect(closeBtn).toBeInTheDocument();
        fireEvent.click(closeBtn);
        expect(mockOnCancel).toHaveBeenCalled();
      });
    });
  });

  describe('Form Display', () => {
    it('should render source account dropdown with external accounts', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Select source external account')).toBeInTheDocument();
      });

      const select = screen.getByLabelText('Select source external account');
      expect(select).toBeInTheDocument();
      expect(select.querySelectorAll('option')).toHaveLength(2);
    });

    it('should render amount input field', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Deposit amount')).toBeInTheDocument();
      });
    });

    it('should render cancel button that calls onCancel', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
      expect(mockOnCancel).toHaveBeenCalledTimes(1);
    });

    it('should render heading "Fund from External Bank"', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Fund from External Bank' })).toBeInTheDocument();
      });
    });
  });

  describe('Input Validation', () => {
    it('should disable submit button when amount is empty', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Deposit amount')).toBeInTheDocument();
      });

      const submitBtn = screen.getByRole('button', { name: /Deposit/i });
      expect(submitBtn).toBeDisabled();
    });

    it('should enable submit button when valid amount is entered', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Deposit amount')).toBeInTheDocument();
      });

      fireEvent.change(screen.getByLabelText('Deposit amount'), { target: { value: '100' } });

      const submitBtn = screen.getByRole('button', { name: /Deposit/i });
      expect(submitBtn).not.toBeDisabled();
    });

    it('should show insufficient balance warning when amount exceeds source balance', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Deposit amount')).toBeInTheDocument();
      });

      fireEvent.change(screen.getByLabelText('Deposit amount'), { target: { value: '99999' } });

      await waitFor(() => {
        expect(screen.getByText(/Insufficient balance in source account/)).toBeInTheDocument();
      });
    });

    it('should show max deposit warning for amounts over 1,000,000', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Deposit amount')).toBeInTheDocument();
      });

      fireEvent.change(screen.getByLabelText('Deposit amount'), { target: { value: '1000001' } });

      await waitFor(() => {
        expect(screen.getByText(/Maximum single deposit/)).toBeInTheDocument();
      });
    });
  });

  describe('Successful Deposit', () => {
    it('should call depositFromExternal with correct params on submit', async () => {
      mockDepositFromExternal.mockResolvedValue({
        depositId: 'dep-1',
        accountId: 'acc-123',
        amount: 250,
        currency: 'NZD',
        status: 'completed',
      });

      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Deposit amount')).toBeInTheDocument();
      });

      fireEvent.change(screen.getByLabelText('Deposit amount'), { target: { value: '250' } });

      const submitBtn = screen.getByRole('button', { name: /Deposit/i });
      fireEvent.click(submitBtn);

      await waitFor(() => {
        expect(mockDepositFromExternal).toHaveBeenCalledWith('acc-123', 'ext-1', 250);
      });
    });

    it('should show success message after deposit', async () => {
      mockDepositFromExternal.mockResolvedValue({
        depositId: 'dep-1',
        accountId: 'acc-123',
        amount: 250,
        currency: 'NZD',
        status: 'completed',
      });

      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Deposit amount')).toBeInTheDocument();
      });

      fireEvent.change(screen.getByLabelText('Deposit amount'), { target: { value: '250' } });
      fireEvent.click(screen.getByRole('button', { name: /Deposit/i }));

      await waitFor(() => {
        expect(screen.getByText(/deposited successfully/)).toBeInTheDocument();
      });
    });
  });

  describe('Error Handling', () => {
    it('should show error message when deposit fails', async () => {
      mockDepositFromExternal.mockRejectedValue(new Error('Bank connection is not active'));

      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Deposit amount')).toBeInTheDocument();
      });

      fireEvent.change(screen.getByLabelText('Deposit amount'), { target: { value: '100' } });
      fireEvent.click(screen.getByRole('button', { name: /Deposit/i }));

      await waitFor(() => {
        expect(screen.getByText('Bank connection is not active')).toBeInTheDocument();
      });
    });

    it('should show error when external accounts fail to load', async () => {
      mockGetExternalAccounts.mockRejectedValue(new Error('Network error'));

      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });
    });
  });

  describe('Source Account Selection', () => {
    it('should pre-select the first external account', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        const select = screen.getByLabelText('Select source external account') as HTMLSelectElement;
        expect(select.value).toBe('ext-1');
      });
    });

    it('should allow changing the source account', async () => {
      render(<FundAccountModal {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Select source external account')).toBeInTheDocument();
      });

      const select = screen.getByLabelText('Select source external account');
      fireEvent.change(select, { target: { value: 'ext-2' } });

      expect((select as HTMLSelectElement).value).toBe('ext-2');
    });
  });
});
