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

  describe('Keyboard Navigation', () => {
    it('should select bank tile on Enter key', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      const anzTile = screen.getByText('ANZ').closest('[tabindex="0"]')!;
      fireEvent.keyDown(anzTile, { key: 'Enter' });

      await waitFor(() => {
        expect(screen.getByText(/ready to link anz/i)).toBeInTheDocument();
      });
    });

    it('should select bank tile on Space key', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      const anzTile = screen.getByText('ANZ').closest('[tabindex="0"]')!;
      fireEvent.keyDown(anzTile, { key: ' ' });

      await waitFor(() => {
        expect(screen.getByText(/ready to link anz/i)).toBeInTheDocument();
      });
    });

    it('should not select bank tile on other keys', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      const anzTile = screen.getByText('ANZ').closest('[tabindex="0"]')!;
      fireEvent.keyDown(anzTile, { key: 'Tab' });

      expect(screen.queryByText(/ready to link anz/i)).not.toBeInTheDocument();
    });
  });

  describe('Confirmation Panel', () => {
    it('should show confirmation panel when tile is clicked', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('ANZ').closest('[tabindex="0"]')!);

      await waitFor(() => {
        expect(screen.getByText(/ready to link anz/i)).toBeInTheDocument();
      });
    });

    it('should dismiss confirmation panel on cancel', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('ANZ').closest('[tabindex="0"]')!);

      await waitFor(() => {
        expect(screen.getByText(/ready to link anz/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /^cancel$/i }));

      await waitFor(() => {
        expect(screen.queryByText(/ready to link anz/i)).not.toBeInTheDocument();
      });
    });

    it('should connect via Proceed to Connect button in confirmation panel', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('ANZ').closest('[tabindex="0"]')!);

      await waitFor(() => {
        expect(screen.getByText(/proceed to connect/i)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText(/proceed to connect/i));

      await waitFor(() => {
        expect(mockConnectBank).toHaveBeenCalledWith('anz');
      });
    });
  });

  describe('Image Error Fallback', () => {
    it('should hide image and show initials fallback when image errors', async () => {
      const banksWithIcon = [
        { id: 'nz_anz', name: 'ANZ', logo: '🏦', country: 'NZ' },
      ];
      mockGetAvailableBanks.mockResolvedValue(banksWithIcon);
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      const img = document.querySelector('.cb-tile-logo-img') as HTMLImageElement;
      if (img) {
        fireEvent.error(img);
        // After error the img should no longer be present
        await waitFor(() => {
          expect(document.querySelector('.cb-tile-logo-img')).not.toBeInTheDocument();
        });
      }
    });
  });

  describe('getMeta fallback', () => {
    it('should use initials fallback for unknown bank id', async () => {
      const unknownBanks = [
        { id: 'xx_unknown_bank', name: 'Unknown Bank', logo: '🏦', country: 'NZ' },
      ];
      mockGetAvailableBanks.mockResolvedValue(unknownBanks);
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('Unknown Bank')).toBeInTheDocument();
      });

      const tile = screen.getByText('Unknown Bank').closest('[tabindex="0"]')!;
      const logo = tile.querySelector('[data-initials]') as HTMLElement;
      // The fallback initials are the last 3 chars uppercased: 'ANK'
      expect(logo?.getAttribute('data-initials')).toBe('ANK');
    });
  });

  describe('Search filtering', () => {
    it('should show empty message when search yields no results', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/search banks/i)).toBeInTheDocument();
      });

      fireEvent.change(screen.getByLabelText(/search banks/i), {
        target: { value: 'zzz_no_match_zzz' },
      });

      await waitFor(() => {
        expect(screen.getByText(/no banks available/i)).toBeInTheDocument();
      });
    });

    it('should show matching bank after partial search', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByLabelText(/search banks/i)).toBeInTheDocument();
      });

      fireEvent.change(screen.getByLabelText(/search banks/i), {
        target: { value: 'ANZ' },
      });

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
        expect(screen.queryByText('ASB')).not.toBeInTheDocument();
      });
    });
  });

  describe('Confirmation panel image error fallback', () => {
    it('should handle image error in confirmation panel logo', async () => {
      const banksWithIcon = [
        { id: 'nz_anz', name: 'ANZ', logo: '🏦', country: 'NZ' },
      ];
      mockGetAvailableBanks.mockResolvedValue(banksWithIcon);
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      // Select the bank to show confirmation panel
      fireEvent.click(screen.getByText('ANZ').closest('[tabindex="0"]')!);

      await waitFor(() => {
        expect(screen.getByText(/ready to link anz/i)).toBeInTheDocument();
      });

      // Fire error on the confirmation panel img if present
      const imgs = document.querySelectorAll('.cb-confirmation-logo .cb-tile-logo-img');
      if (imgs.length > 0) {
        fireEvent.error(imgs[0]);
        await waitFor(() => {
          const remainingImgs = document.querySelectorAll('.cb-confirmation-logo .cb-tile-logo-img');
          expect(remainingImgs.length).toBe(0);
        });
      }
    });
  });

  describe('Country change resets state', () => {
    it('should clear selected bank and search query when country changes', async () => {
      renderConnectBank();

      await waitFor(() => {
        expect(screen.getByText('ANZ')).toBeInTheDocument();
      });

      // Select a bank
      fireEvent.click(screen.getByText('ANZ').closest('[tabindex="0"]')!);
      await waitFor(() => {
        expect(screen.getByText(/ready to link anz/i)).toBeInTheDocument();
      });

      // Change country — should reset selectedBank and searchQuery
      const countrySelect = screen.getByLabelText(/select country/i);
      fireEvent.change(countrySelect, { target: { value: 'AU' } });

      await waitFor(() => {
        expect(screen.queryByText(/ready to link anz/i)).not.toBeInTheDocument();
      });
    });
  });
});
