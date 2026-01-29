import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import ConnectBank from './ConnectBank';
import * as bankingService from '../services/banking';

jest.mock('../services/banking');

describe('ConnectBank Component', () => {
  const mockOnConnected = jest.fn();
  const mockGetAvailableBanks = bankingService.getAvailableBanks as jest.Mock;
  const mockConnectBank = bankingService.connectBank as jest.Mock;

  const mockBanks = [
    { id: 'anz', name: 'ANZ', logo: '🏦', country: 'NZ' },
    { id: 'asb', name: 'ASB', logo: '🏛️', country: 'NZ' },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    mockGetAvailableBanks.mockResolvedValue(mockBanks);
    mockConnectBank.mockResolvedValue({ connectionId: 'conn123', accountsImported: 2 });
  });

  const renderConnectBank = () => {
    return render(
      <BrowserRouter>
        <ConnectBank onConnected={mockOnConnected} />
      </BrowserRouter>
    );
  };

  describe('Initial Render', () => {
    it('should render the component heading', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /connect your bank/i })).toBeInTheDocument();
      });
    });

    it('should render country selector', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/select country/i)).toBeInTheDocument();
      });
    });

    it('should show loading state initially', () => {
      mockGetAvailableBanks.mockImplementation(() => new Promise(() => {}));
      renderConnectBank();

      expect(screen.getByText(/loading available banks/i)).toBeInTheDocument();
    });
  });

  describe('Bank List', () => {
    it('should display available banks after loading', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
        expect(screen.getByText('ASB')).toBeInTheDocument();
      });
    });

    it('should display bank logos', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('🏦')).toBeInTheDocument();
        expect(screen.getByText('🏛️')).toBeInTheDocument();
      });
    });

    it('should display connect buttons for each bank', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/connect to asb/i)).toBeInTheDocument();
      });
    });

    it('should show no banks message when empty', async () => {
      mockGetAvailableBanks.mockResolvedValue([]);
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText(/no banks available/i)).toBeInTheDocument();
      });
    });
  });

  describe('Country Selection', () => {
    it('should default to NZ', async () => {
      renderConnectBank();

      await waitFor(() => {
        const countrySelect = screen.getByLabelText(/select country/i) as HTMLSelectElement;
        expect(countrySelect.value).toBe('NZ');
      });
    });

    it('should fetch banks when country changes', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/select country/i)).toBeInTheDocument();
      });

      const countrySelect = screen.getByLabelText(/select country/i);
      fireEvent.change(countrySelect, { target: { value: 'AU' } });

      await waitFor(() => {
        expect(mockGetAvailableBanks).toHaveBeenCalledWith('AU');
      });
    });

    it('should have Australia option', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByRole('option', { name: /australia/i })).toBeInTheDocument();
      });
    });

    it('should have UK option', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByRole('option', { name: /united kingdom/i })).toBeInTheDocument();
      });
    });
  });

  describe('Bank Connection', () => {
    it('should connect to bank when button clicked', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/connect to anz/i));

      await waitFor(() => {
        expect(mockConnectBank).toHaveBeenCalledWith('anz');
      });
    });

    it('should show connecting state during connection', async () => {
      mockConnectBank.mockImplementation(() => new Promise(() => {}));
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/connect to anz/i));

      await waitFor(() => {
        expect(screen.getByText(/connecting/i)).toBeInTheDocument();
      });
    });

    it('should disable buttons while connecting', async () => {
      mockConnectBank.mockImplementation(() => new Promise(() => {}));
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/connect to anz/i));

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to asb/i)).toBeDisabled();
      });
    });

    it('should show success message after connection', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/connect to anz/i));

      await waitFor(() => {
        expect(screen.getByText(/connected to anz.*imported 2 account/i)).toBeInTheDocument();
      });
    });

    it('should call onConnected callback after successful connection', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/connect to anz/i));

      await waitFor(() => {
        expect(mockOnConnected).toHaveBeenCalled();
      });
    });
  });

  describe('Error Handling', () => {
    it('should show error when loading banks fails', async () => {
      mockGetAvailableBanks.mockRejectedValue(new Error('Network error'));
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText(/network error/i)).toBeInTheDocument();
      });
    });

    it('should show error when connection fails', async () => {
      mockConnectBank.mockRejectedValue(new Error('Connection refused'));
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/connect to anz/i));

      await waitFor(() => {
        expect(screen.getByText(/connection refused/i)).toBeInTheDocument();
      });
    });

    it('should show bank already connected error', async () => {
      mockConnectBank.mockRejectedValue(new Error('Bank already connected'));
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/connect to anz/i));

      await waitFor(() => {
        expect(screen.getByText(/bank already connected/i)).toBeInTheDocument();
      });
    });

    it('should show fallback error message', async () => {
      mockConnectBank.mockRejectedValue({});
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/connect to anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByLabelText(/connect to anz/i));

      await waitFor(() => {
        expect(screen.getByText(/failed to connect bank/i)).toBeInTheDocument();
      });
    });
  });
});
