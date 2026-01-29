import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import LinkedBanks from './LinkedBanks';
import * as bankingService from '../services/banking';

jest.mock('../services/banking');

describe('LinkedBanks Component', () => {
  const mockGetConnectedBanks = bankingService.getConnectedBanks as jest.Mock;
  const mockGetExternalAccounts = bankingService.getExternalAccounts as jest.Mock;
  const mockDisconnectBank = bankingService.disconnectBank as jest.Mock;
  const mockSyncBankAccounts = bankingService.syncBankAccounts as jest.Mock;

  const mockConnections = [
    {
      id: 'conn1',
      bankId: 'anz',
      bankName: 'ANZ',
      bankLogo: '🏦',
      status: 'Active',
      connectedAt: '2024-01-15T10:00:00Z'
    },
    {
      id: 'conn2',
      bankId: 'asb',
      bankName: 'ASB',
      bankLogo: '🏛️',
      status: 'Active',
      connectedAt: '2024-01-14T10:00:00Z'
    }
  ];

  const mockAccounts = [
    {
      id: 'acc1',
      accountName: 'Everyday',
      accountType: 'checking',
      accountNumber: '12-3456-7890123-00',
      balance: 1500,
      currency: 'NZD',
      lastSyncedAt: '2024-01-15T12:00:00Z',
      bankName: 'ANZ',
      bankLogo: '🏦'
    },
    {
      id: 'acc2',
      accountName: 'Savings',
      accountType: 'savings',
      accountNumber: '12-3456-7890123-01',
      balance: 5000,
      currency: 'NZD',
      lastSyncedAt: '2024-01-15T12:00:00Z',
      bankName: 'ANZ',
      bankLogo: '🏦'
    },
    {
      id: 'acc3',
      accountName: 'FastSaver',
      accountType: 'savings',
      accountNumber: '12-9999-8888888-00',
      balance: 2500,
      currency: 'NZD',
      lastSyncedAt: '2024-01-14T12:00:00Z',
      bankName: 'ASB',
      bankLogo: '🏛️'
    }
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    mockGetConnectedBanks.mockResolvedValue(mockConnections);
    mockGetExternalAccounts.mockResolvedValue(mockAccounts);
    mockDisconnectBank.mockResolvedValue(undefined);
    mockSyncBankAccounts.mockResolvedValue({ syncedAt: '2024-01-15T14:00:00Z' });
    
    // Mock window.confirm
    window.confirm = jest.fn(() => true);
  });

  const renderLinkedBanks = (refreshTrigger?: number) => {
    return render(
      <BrowserRouter>
        <LinkedBanks refreshTrigger={refreshTrigger} />
      </BrowserRouter>
    );
  };

  describe('Loading State', () => {
    it('should show loading message while fetching data', () => {
      mockGetConnectedBanks.mockImplementation(() => new Promise(() => {}));
      renderLinkedBanks();

      expect(screen.getByText(/loading connected banks/i)).toBeInTheDocument();
    });
  });

  describe('No Connections', () => {
    it('should render nothing when no connections exist', async () => {
      mockGetConnectedBanks.mockResolvedValue([]);
      mockGetExternalAccounts.mockResolvedValue([]);

      const { container } = renderLinkedBanks();

      await waitFor(() => {
        expect(container.firstChild).toBeNull();
      });
    });
  });

  describe('Connected Banks Display', () => {
    it('should display total balance header', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /connected bank total/i })).toBeInTheDocument();
      });
    });

    it('should show total balance across all accounts', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        // Total: 1500 + 5000 + 2500 = 9000
        expect(screen.getByText(/\$9,000/)).toBeInTheDocument();
      });
    });

    it('should show count of banks and accounts', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByText(/2 bank\(s\).*3 account\(s\)/i)).toBeInTheDocument();
      });
    });

    it('should display linked banks heading', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /linked banks/i })).toBeInTheDocument();
      });
    });

    it('should display each connected bank', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
        expect(screen.getByText('ASB')).toBeInTheDocument();
      });
    });

    it('should display bank logos', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getAllByText('🏦').length).toBeGreaterThan(0);
        expect(screen.getAllByText('🏛️').length).toBeGreaterThan(0);
      });
    });

    it('should display connection status badge', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        const activeStatuses = screen.getAllByText('Active');
        expect(activeStatuses.length).toBe(2);
      });
    });
  });

  describe('Account Display', () => {
    it('should display accounts under each bank', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByText('Everyday')).toBeInTheDocument();
        expect(screen.getByText('Savings')).toBeInTheDocument();
        expect(screen.getByText('FastSaver')).toBeInTheDocument();
      });
    });

    it('should display account balances', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByText(/\$1,500/)).toBeInTheDocument();
        expect(screen.getByText(/\$5,000/)).toBeInTheDocument();
        expect(screen.getByText(/\$2,500/)).toBeInTheDocument();
      });
    });
  });

  describe('Sync Functionality', () => {
    it('should have sync button for each bank', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/sync anz accounts/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/sync asb accounts/i)).toBeInTheDocument();
      });
    });

    it('should call sync when button clicked', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/sync anz accounts/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/sync anz accounts/i));

      await waitFor(() => {
        expect(mockSyncBankAccounts).toHaveBeenCalledWith('conn1');
      });
    });

    it('should show syncing state during sync', async () => {
      mockSyncBankAccounts.mockImplementation(() => new Promise(() => {}));
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/sync anz accounts/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/sync anz accounts/i));

      await waitFor(() => {
        expect(screen.getByText(/syncing/i)).toBeInTheDocument();
      });
    });

    it('should disable all sync buttons while syncing', async () => {
      mockSyncBankAccounts.mockImplementation(() => new Promise(() => {}));
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/sync anz accounts/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/sync anz accounts/i));

      await waitFor(() => {
        expect(screen.getByLabelText(/sync asb accounts/i)).toBeDisabled();
      });
    });

    it('should reload data after sync completes', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/sync anz accounts/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/sync anz accounts/i));

      await waitFor(() => {
        // Should call getConnectedBanks twice: initial load + after sync
        expect(mockGetConnectedBanks).toHaveBeenCalledTimes(2);
      });
    });
  });

  describe('Disconnect Functionality', () => {
    it('should have disconnect button for each bank', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect anz/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/disconnect asb/i)).toBeInTheDocument();
      });
    });

    it('should show confirmation dialog before disconnect', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/disconnect anz/i));

      expect(window.confirm).toHaveBeenCalledWith('Are you sure you want to disconnect ANZ?');
    });

    it('should call disconnect when confirmed', async () => {
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/disconnect anz/i));

      await waitFor(() => {
        expect(mockDisconnectBank).toHaveBeenCalledWith('conn1');
      });
    });

    it('should not disconnect when cancelled', async () => {
      (window.confirm as jest.Mock).mockReturnValue(false);
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/disconnect anz/i));

      expect(mockDisconnectBank).not.toHaveBeenCalled();
    });

    it('should show disconnecting state', async () => {
      mockDisconnectBank.mockImplementation(() => new Promise(() => {}));
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/disconnect anz/i));

      await waitFor(() => {
        expect(screen.getByText(/disconnecting/i)).toBeInTheDocument();
      });
    });

    it('should disable all disconnect buttons while disconnecting', async () => {
      mockDisconnectBank.mockImplementation(() => new Promise(() => {}));
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/disconnect anz/i));

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect asb/i)).toBeDisabled();
      });
    });
  });

  describe('Error Handling', () => {
    it('should show error when loading fails', async () => {
      mockGetConnectedBanks.mockRejectedValue(new Error('Failed to load'));
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByText(/failed to load/i)).toBeInTheDocument();
      });
    });

    it('should show error when sync fails', async () => {
      mockSyncBankAccounts.mockRejectedValue(new Error('Sync failed'));
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/sync anz accounts/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/sync anz accounts/i));

      await waitFor(() => {
        expect(screen.getByText(/sync failed/i)).toBeInTheDocument();
      });
    });

    it('should show error when disconnect fails', async () => {
      mockDisconnectBank.mockRejectedValue(new Error('Disconnect failed'));
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/disconnect anz/i));

      await waitFor(() => {
        expect(screen.getByText(/disconnect failed/i)).toBeInTheDocument();
      });
    });

    it('should show fallback error message for sync', async () => {
      mockSyncBankAccounts.mockRejectedValue({});
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/sync anz accounts/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/sync anz accounts/i));

      await waitFor(() => {
        expect(screen.getByText(/failed to sync accounts/i)).toBeInTheDocument();
      });
    });

    it('should show fallback error message for disconnect', async () => {
      mockDisconnectBank.mockRejectedValue({});
      renderLinkedBanks();

      await waitFor(() => {
        expect(screen.getByLabelText(/disconnect anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/disconnect anz/i));

      await waitFor(() => {
        expect(screen.getByText(/failed to disconnect bank/i)).toBeInTheDocument();
      });
    });
  });

  describe('Refresh Trigger', () => {
    it('should reload data when refreshTrigger changes', async () => {
      const { rerender } = renderLinkedBanks(1);

      await waitFor(() => {
        expect(mockGetConnectedBanks).toHaveBeenCalledTimes(1);
      });

      rerender(
        <BrowserRouter>
          <LinkedBanks refreshTrigger={2} />
        </BrowserRouter>
      );

      await waitFor(() => {
        expect(mockGetConnectedBanks).toHaveBeenCalledTimes(2);
      });
    });
  });
});
