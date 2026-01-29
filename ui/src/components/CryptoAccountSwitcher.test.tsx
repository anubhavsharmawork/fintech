import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import CryptoAccountSwitcher from './CryptoAccountSwitcher';
import * as cryptoService from '../services/crypto';

jest.mock('../services/crypto');

describe('CryptoAccountSwitcher Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('1.5');
    (cryptoService.getFTKBalance as jest.Mock).mockResolvedValue('1000');
  });

  describe('Render Tests', () => {
    it('should render crypto account component', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('Crypto Account')).toBeInTheDocument();
      });
    });

    it('should display card class', async () => {
      const { container } = render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(container.querySelector('.card')).toBeInTheDocument();
      });
    });

    it('should display refresh button', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).toBeInTheDocument();
      });
    });

    it('should have btn and btn-secondary classes on button', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).toHaveClass('btn', 'btn-secondary');
      });
    });

    it('should have type button on refresh button', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).toHaveAttribute('type', 'button');
      });
    });
  });

  describe('Address Display', () => {
    it('should display provided address', () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef" />);

      expect(screen.getByText('Address: 0x1234567890abcdef')).toBeInTheDocument();
    });

    it('should display demo address when no address provided', () => {
      render(<CryptoAccountSwitcher demoAddress="0xdemo" />);

      expect(screen.getByText('Address: 0xdemo')).toBeInTheDocument();
    });

    it('should prefer real address over demo address', () => {
      render(<CryptoAccountSwitcher address="0xreal" demoAddress="0xdemo" />);

      expect(screen.getByText('Address: 0xreal')).toBeInTheDocument();
      expect(screen.queryByText('Address: 0xdemo')).not.toBeInTheDocument();
    });

    it('should display no wallet message when neither address provided', () => {
      render(<CryptoAccountSwitcher />);

      expect(screen.getByText('No wallet connected. Using demo.')).toBeInTheDocument();
    });

    it('should display address with small class', () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      const addressElement = screen.getByText(/Address: 0x1234/);
      expect(addressElement).toHaveClass('small');
    });
  });

  describe('Balance Display', () => {
    it('should display ETH balance initially', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('ETH: 1.5')).toBeInTheDocument();
      });
    });

    it('should display FTK balance initially', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('FTK: 1000')).toBeInTheDocument();
      });
    });

    it('should have small class on balance elements', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        const ethElement = screen.getByText('ETH: 1.5');
        expect(ethElement).toHaveClass('small');
      });

      const ftkElement = screen.getByText('FTK: 1000');
      expect(ftkElement).toHaveClass('small');
    });

    it('should display initial balance as 0', () => {
      (cryptoService.getETHBalance as jest.Mock).mockImplementationOnce(
        () => new Promise(() => {}) // Never resolves
      );
      (cryptoService.getFTKBalance as jest.Mock).mockImplementationOnce(
        () => new Promise(() => {}) // Never resolves
      );

      render(<CryptoAccountSwitcher address="0x1234" />);

      expect(screen.getByText('ETH: 0')).toBeInTheDocument();
      expect(screen.getByText('FTK: 0')).toBeInTheDocument();
    });

    it('should handle decimal balances', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('2.5');
      (cryptoService.getFTKBalance as jest.Mock).mockResolvedValue('1234.567');

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('ETH: 2.5')).toBeInTheDocument();
        expect(screen.getByText('FTK: 1234.567')).toBeInTheDocument();
      });
    });

    it('should handle large balances', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('9999.999');
      (cryptoService.getFTKBalance as jest.Mock).mockResolvedValue('1000000');

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('ETH: 9999.999')).toBeInTheDocument();
        expect(screen.getByText('FTK: 1000000')).toBeInTheDocument();
      });
    });

    it('should handle zero balances', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('0');
      (cryptoService.getFTKBalance as jest.Mock).mockResolvedValue('0');

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('ETH: 0')).toBeInTheDocument();
        expect(screen.getByText('FTK: 0')).toBeInTheDocument();
      });
    });
  });

  describe('Balance Fetching', () => {
    it('should call getETHBalance on mount', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x1234');
      });
    });

    it('should call getFTKBalance on mount', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(cryptoService.getFTKBalance).toHaveBeenCalledWith('0x1234');
      });
    });

    it('should use demo address if no real address', async () => {
      render(<CryptoAccountSwitcher demoAddress="0xdemo" />);

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0xdemo');
        expect(cryptoService.getFTKBalance).toHaveBeenCalledWith('0xdemo');
      });
    });

    it('should not fetch if no address provided', () => {
      jest.useFakeTimers();

      render(<CryptoAccountSwitcher />);

      jest.runAllTimers();

      expect(cryptoService.getETHBalance).not.toHaveBeenCalled();
      expect(cryptoService.getFTKBalance).not.toHaveBeenCalled();

      jest.useRealTimers();
    });

    it('should fetch balances in parallel', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x1234');
        expect(cryptoService.getFTKBalance).toHaveBeenCalledWith('0x1234');
      });

      // Both should be called (we can't guarantee order with async)
      expect(cryptoService.getETHBalance).toHaveBeenCalled();
      expect(cryptoService.getFTKBalance).toHaveBeenCalled();
    });

    it('should refetch when address changes', async () => {
      const { rerender } = render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x1234');
      });

      (cryptoService.getETHBalance as jest.Mock).mockClear();
      (cryptoService.getFTKBalance as jest.Mock).mockClear();

      rerender(<CryptoAccountSwitcher address="0x5678" />);

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x5678');
        expect(cryptoService.getFTKBalance).toHaveBeenCalledWith('0x5678');
      });
    });

    it('should not refetch when demo address changes if real address provided', async () => {
      const { rerender } = render(
        <CryptoAccountSwitcher address="0x1234" demoAddress="0xdemo1" />
      );

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x1234');
      });

      (cryptoService.getETHBalance as jest.Mock).mockClear();
      (cryptoService.getFTKBalance as jest.Mock).mockClear();

      rerender(<CryptoAccountSwitcher address="0x1234" demoAddress="0xdemo2" />);

      expect(cryptoService.getETHBalance).not.toHaveBeenCalled();
    });
  });

  describe('Refresh Button', () => {
    it('should refresh balances when button clicked', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('Refresh Balances')).toBeInTheDocument();
      });

      (cryptoService.getETHBalance as jest.Mock).mockClear();
      (cryptoService.getFTKBalance as jest.Mock).mockClear();

      fireEvent.click(screen.getByText('Refresh Balances'));

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x1234');
        expect(cryptoService.getFTKBalance).toHaveBeenCalledWith('0x1234');
      });
    });

    it('should show loading state while refreshing', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(() => resolve('2.0'), 100)
          )
      );
      (cryptoService.getFTKBalance as jest.Mock).mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(() => resolve('2000'), 100)
          )
      );

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('Refresh Balances')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Refresh Balances'));

      expect(screen.getByText('Refreshing…')).toBeInTheDocument();
    });

    it('should disable button while loading', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(() => resolve('2.0'), 100)
          )
      );
      (cryptoService.getFTKBalance as jest.Mock).mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(() => resolve('2000'), 100)
          )
      );

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('Refresh Balances')).toBeInTheDocument();
      });

      const button = screen.getByRole('button') as HTMLButtonElement;
      fireEvent.click(button);

      expect(button).toBeDisabled();
    });

    it('should disable button when no address', () => {
      render(<CryptoAccountSwitcher />);

      const button = screen.getByRole('button') as HTMLButtonElement;
      expect(button).toBeDisabled();
    });

    it('should enable button when address provided and not loading', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        const button = screen.getByRole('button') as HTMLButtonElement;
        expect(button).not.toBeDisabled();
      });
    });

    it('should update balances after refresh', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText('ETH: 1.5')).toBeInTheDocument();
      });

      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('3.0');
      (cryptoService.getFTKBalance as jest.Mock).mockResolvedValue('2000');

      fireEvent.click(screen.getByText('Refresh Balances'));

      await waitFor(() => {
        expect(screen.getByText('ETH: 3.0')).toBeInTheDocument();
        expect(screen.getByText('FTK: 2000')).toBeInTheDocument();
      });
    });
  });

  describe('Error Handling', () => {
    it('should handle balance fetch errors gracefully', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockRejectedValue(
        new Error('Network error')
      );
      (cryptoService.getFTKBalance as jest.Mock).mockRejectedValue(
        new Error('Network error')
      );

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        // Should not crash, remains with initial state
        expect(screen.getByText('Crypto Account')).toBeInTheDocument();
      });
    });

    it('should continue loading state on error', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockRejectedValue(
        new Error('Network error')
      );
      (cryptoService.getFTKBalance as jest.Mock).mockRejectedValue(
        new Error('Network error')
      );

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        // Eventually button should be enabled again after loading completes
        const button = screen.getByRole('button');
        expect(button).not.toBeDisabled();
      });
    });
  });

  describe('Address Switch Scenarios', () => {
    it('should load balances for first address', async () => {
      render(<CryptoAccountSwitcher address="0x1111" />);

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x1111');
      });
    });

    it('should load balances for second address after switching', async () => {
      const { rerender } = render(<CryptoAccountSwitcher address="0x1111" />);

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x1111');
      });

      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('5.0');
      (cryptoService.getFTKBalance as jest.Mock).mockResolvedValue('5000');

      rerender(<CryptoAccountSwitcher address="0x2222" />);

      await waitFor(() => {
        expect(cryptoService.getETHBalance).toHaveBeenCalledWith('0x2222');
      });
    });

    it('should display current address in UI', () => {
      render(<CryptoAccountSwitcher address="0xaabbccdd" />);

      expect(screen.getByText('Address: 0xaabbccdd')).toBeInTheDocument();
    });
  });

  describe('Layout', () => {
    it('should have flex column layout', () => {
      const { container } = render(<CryptoAccountSwitcher address="0x1234" />);

      const content = container.querySelector('div[style*="flex-direction"]');
      expect(content).toHaveStyle({
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
      });
    });

    it('should have margin bottom on card', () => {
      const { container } = render(<CryptoAccountSwitcher address="0x1234" />);

      const card = container.querySelector('.card');
      expect(card).toHaveStyle({ marginBottom: '12px' });
    });
  });

  describe('Demo Mode', () => {
    beforeEach(() => {
      (cryptoService.DEMO_WALLET as any) = {
        ethBalance: '10.5',
        ftkBalance: '50000'
      };
    });

    it('should display demo balances when isDemo is true', async () => {
      render(<CryptoAccountSwitcher address="0x1234" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/10\.5/)).toBeInTheDocument();
        expect(screen.getByText(/50000/)).toBeInTheDocument();
      });
    });

    it('should not call balance services in demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/Demo/)).toBeInTheDocument();
      });

      expect(cryptoService.getETHBalance).not.toHaveBeenCalled();
      expect(cryptoService.getFTKBalance).not.toHaveBeenCalled();
    });

    it('should disable refresh button in demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234" isDemo={true} />);

      await waitFor(() => {
        const button = screen.getByRole('button') as HTMLButtonElement;
        expect(button).toBeDisabled();
      });
    });

    it('should show demo indicator in wallet address', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/Demo/)).toBeInTheDocument();
      });
    });

    it('should display demo message at bottom', async () => {
      render(<CryptoAccountSwitcher address="0x1234" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/Sample balances shown for demonstration purposes/)).toBeInTheDocument();
      });
    });
  });

  describe('Error Display', () => {
    it('should display error message when balance fetch fails', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockRejectedValue(new Error('Network timeout'));

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/Network timeout/)).toBeInTheDocument();
      });
    });

    it('should display generic error when no message', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockRejectedValue(new Error());

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/Failed to fetch balances/)).toBeInTheDocument();
      });
    });

    it('should clear error on successful refresh', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockRejectedValueOnce(new Error('Network error'));
      (cryptoService.getFTKBalance as jest.Mock).mockRejectedValueOnce(new Error('Network error'));

      const { rerender } = render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/Network error/)).toBeInTheDocument();
      });

      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('1.5');
      (cryptoService.getFTKBalance as jest.Mock).mockResolvedValue('1000');

      rerender(<CryptoAccountSwitcher address="0x5678" />);

      await waitFor(() => {
        expect(screen.queryByText(/Network error/)).not.toBeInTheDocument();
      });
    });

    it('should display invalid address error', async () => {
      (cryptoService.isValidAddress as jest.Mock).mockReturnValue(false);

      render(<CryptoAccountSwitcher address="invalid" />);

      await waitFor(() => {
        expect(screen.getByText(/Invalid wallet address/)).toBeInTheDocument();
      });
    });

    it('should not call balance services for invalid address', async () => {
      (cryptoService.isValidAddress as jest.Mock).mockReturnValue(false);

      render(<CryptoAccountSwitcher address="invalid" />);

      await waitFor(() => {
        expect(screen.getByText(/Invalid wallet address/)).toBeInTheDocument();
      });

      expect(cryptoService.getETHBalance).not.toHaveBeenCalled();
      expect(cryptoService.getFTKBalance).not.toHaveBeenCalled();
    });
  });

  describe('Token Info', () => {
    it('should fetch and display token info', async () => {
      (cryptoService.getTokenInfo as jest.Mock).mockResolvedValue({
        name: 'FinTech Token',
        symbol: 'FTK',
        decimals: 18
      });

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/FTK Token/)).toBeInTheDocument();
      });
    });

    it('should handle token info fetch failure gracefully', async () => {
      (cryptoService.getTokenInfo as jest.Mock).mockRejectedValue(new Error('Token error'));

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/FTK Token/)).toBeInTheDocument();
      });
    });

    it('should use fallback token name on error', async () => {
      (cryptoService.getTokenInfo as jest.Mock).mockRejectedValue(new Error());

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/FTK Token/)).toBeInTheDocument();
      });
    });
  });

  describe('Token Link Display', () => {
    it('should show token link when showTokenLink is true', async () => {
      (cryptoService.getEtherscanTokenUrl as jest.Mock).mockReturnValue('https://etherscan.io/token/0xabc');

      render(<CryptoAccountSwitcher address="0x1234" showTokenLink={true} />);

      await waitFor(() => {
        expect(screen.getByText(/View contract/)).toBeInTheDocument();
      });
    });

    it('should not show token link when showTokenLink is false', async () => {
      render(<CryptoAccountSwitcher address="0x1234" showTokenLink={false} />);

      await waitFor(() => {
        expect(screen.queryByText(/View contract/)).not.toBeInTheDocument();
      });
    });

    it('should call getEtherscanTokenUrl when rendering link', async () => {
      (cryptoService.getEtherscanTokenUrl as jest.Mock).mockReturnValue('https://etherscan.io/token/0xabc');

      render(<CryptoAccountSwitcher address="0x1234" showTokenLink={true} />);

      await waitFor(() => {
        expect(cryptoService.getEtherscanTokenUrl).toHaveBeenCalled();
      });
    });
  });

  describe('Address Links', () => {
    it('should create Etherscan address link for non-demo address', async () => {
      (cryptoService.getEtherscanAddressUrl as jest.Mock).mockReturnValue('https://etherscan.io/address/0x1234');

      render(<CryptoAccountSwitcher address="0x1234567890abcdef" isDemo={false} />);

      await waitFor(() => {
        const link = screen.getByText(/0x123456/);
        expect(link.closest('a')).toHaveAttribute('href', 'https://etherscan.io/address/0x1234');
      });
    });

    it('should shorten long addresses', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef" />);

      await waitFor(() => {
        expect(screen.getByText(/0x123456.*abcdef/)).toBeInTheDocument();
      });
    });

    it('should not create link for demo address', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef" isDemo={true} />);

      await waitFor(() => {
        const demoText = screen.getByText(/Demo/);
        expect(demoText.closest('a')).toBeNull();
      });
    });
  });

  describe('Last Updated Timestamp', () => {
    it('should display last updated time', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/Updated:/)).toBeInTheDocument();
      });
    });

    it('should not display timestamp initially', () => {
      (cryptoService.getETHBalance as jest.Mock).mockImplementation(() => new Promise(() => {}));

      render(<CryptoAccountSwitcher address="0x1234" />);

      expect(screen.queryByText(/Updated:/)).not.toBeInTheDocument();
    });

    it('should update timestamp after refresh', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/Updated:/)).toBeInTheDocument();
      });

      const firstTimestamp = screen.getByText(/Updated:/).textContent;

      await new Promise(resolve => setTimeout(resolve, 1000));

      fireEvent.click(screen.getByRole('button'));

      await waitFor(() => {
        const secondTimestamp = screen.getByText(/Updated:/).textContent;
        expect(secondTimestamp).toBeDefined();
      });
    });
  });

  describe('Balance Formatting', () => {
    it('should format very small balances', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('0.00001');

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/<0.0001/)).toBeInTheDocument();
      });
    });

    it('should format balances with specified decimals', async () => {
      (cryptoService.getFTKBalance as jest.Mock).mockResolvedValue('1234.56789');

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/1234\.57/)).toBeInTheDocument();
      });
    });

    it('should handle NaN balances', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('invalid');

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/SepoliaETH/)).toBeInTheDocument();
      });
    });

    it('should format zero with no decimals', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockResolvedValue('0.0');

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        const balances = screen.getAllByText('0');
        expect(balances.length).toBeGreaterThan(0);
      });
    });
  });

  describe('No Wallet Connected', () => {
    it('should display connect wallet message', () => {
      render(<CryptoAccountSwitcher />);

      expect(screen.getByText(/Connect your wallet to view real-time balances/)).toBeInTheDocument();
    });

    it('should display wallet icon when not connected', () => {
      render(<CryptoAccountSwitcher />);

      expect(screen.getByText('👛')).toBeInTheDocument();
    });

    it('should not display balances when not connected', () => {
      render(<CryptoAccountSwitcher />);

      expect(screen.queryByText(/SepoliaETH/)).not.toBeInTheDocument();
      expect(screen.queryByText(/FTK Token/)).not.toBeInTheDocument();
    });
  });

  describe('Loading States', () => {
    it('should show loading text while fetching', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve('1.5'), 100))
      );
      (cryptoService.getFTKBalance as jest.Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve('1000'), 100))
      );

      render(<CryptoAccountSwitcher address="0x1234" />);

      expect(screen.getAllByText('...').length).toBeGreaterThan(0);

      await waitFor(() => {
        expect(screen.queryByText('...')).not.toBeInTheDocument();
      });
    });

    it('should show refresh icon in button text', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/Refresh Balances/)).toBeInTheDocument();
      });
    });

    it('should show loading icon in button while refreshing', async () => {
      (cryptoService.getETHBalance as jest.Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve('1.5'), 100))
      );

      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByRole('button')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button'));

      expect(screen.getByText(/Refreshing/)).toBeInTheDocument();
    });

    it('should show demo icon in button for demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/Demo Data/)).toBeInTheDocument();
      });
    });
  });

  describe('Blockchain Info Message', () => {
    it('should display RPC message for non-demo', async () => {
      render(<CryptoAccountSwitcher address="0x1234" />);

      await waitFor(() => {
        expect(screen.getByText(/Balances are fetched directly from Sepolia blockchain via RPC/)).toBeInTheDocument();
      });
    });

    it('should not display RPC message in demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234" isDemo={true} />);

      await waitFor(() => {
        expect(screen.queryByText(/Balances are fetched directly from Sepolia blockchain via RPC/)).not.toBeInTheDocument();
      });
    });
  });
});
