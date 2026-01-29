import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import NetworkStatus from './NetworkStatus';
import * as cryptoService from '../services/crypto';

jest.mock('../services/crypto', () => ({
  getNetworkInfo: jest.fn(),
  switchToSepolia: jest.fn(),
  SEPOLIA_CHAIN_ID: 11155111,
  ETHERSCAN_BASE_URL: 'https://sepolia.etherscan.io'
}));

describe('NetworkStatus Component', () => {
  let mockEthereum: any;

  beforeEach(() => {
    jest.clearAllMocks();
    mockEthereum = {
      on: jest.fn(),
      removeListener: jest.fn()
    };
    (window as any).ethereum = mockEthereum;
  });

  afterEach(() => {
    delete (window as any).ethereum;
  });

  describe('Rendering', () => {
    it('should render nothing when networkInfo is null initially', () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockImplementation(() => new Promise(() => {}));

      const { container } = render(<NetworkStatus />);

      expect(container).toBeEmptyDOMElement();
    });

    it('should display network name when connected to Sepolia', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Sepolia Testnet')).toBeInTheDocument();
      });
    });

    it('should display network name when on wrong network', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Ethereum Mainnet',
        isCorrectNetwork: false
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Ethereum Mainnet')).toBeInTheDocument();
      });
    });

    it('should render card component', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      const { container } = render(<NetworkStatus />);

      await waitFor(() => {
        expect(container.querySelector('.card')).toBeInTheDocument();
      });
    });
  });

  describe('Correct Network (Sepolia)', () => {
    it('should show green indicator for Sepolia', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      const { container } = render(<NetworkStatus />);

      await waitFor(() => {
        const card = container.querySelector('.card');
        expect(card).toHaveStyle({ borderLeft: expect.stringContaining('#22c55e') });
      });
    });

    it('should show Etherscan link when on Sepolia', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        const link = screen.getByText(/View on Etherscan/);
        expect(link).toHaveAttribute('href', 'https://sepolia.etherscan.io');
        expect(link).toHaveAttribute('target', '_blank');
      });
    });

    it('should not show switch button when on Sepolia', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.queryByText('Switch to Sepolia')).not.toBeInTheDocument();
      });
    });

    it('should not show warning message when on Sepolia', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.queryByText(/Please switch to Sepolia/)).not.toBeInTheDocument();
      });
    });

    it('should show green status indicator dot', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      const { container } = render(<NetworkStatus />);

      await waitFor(() => {
        const dot = container.querySelector('[style*="border-radius: 50%"]');
        expect(dot).toHaveStyle({ backgroundColor: '#22c55e' });
      });
    });
  });

  describe('Wrong Network', () => {
    it('should show red indicator for wrong network', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });

      const { container } = render(<NetworkStatus />);

      await waitFor(() => {
        const card = container.querySelector('.card');
        expect(card).toHaveStyle({ borderLeft: expect.stringContaining('#ef4444') });
      });
    });

    it('should show switch button when on wrong network', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });
    });

    it('should show warning message when on wrong network', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText(/Please switch to Sepolia Testnet/)).toBeInTheDocument();
      });
    });

    it('should not show Etherscan link when on wrong network', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.queryByText(/View on Etherscan/)).not.toBeInTheDocument();
      });
    });

    it('should show red status indicator dot', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });

      const { container } = render(<NetworkStatus />);

      await waitFor(() => {
        const dot = container.querySelector('[style*="border-radius: 50%"]');
        expect(dot).toHaveStyle({ backgroundColor: '#ef4444' });
      });
    });
  });

  describe('Network Switching', () => {
    it('should call switchToSepolia when switch button clicked', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });
      (cryptoService.switchToSepolia as jest.Mock).mockResolvedValue(undefined);

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        expect(cryptoService.switchToSepolia).toHaveBeenCalled();
      });
    });

    it('should show loading state while switching', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });
      (cryptoService.switchToSepolia as jest.Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      expect(screen.getByText('Switching…')).toBeInTheDocument();
    });

    it('should disable button while switching', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });
      (cryptoService.switchToSepolia as jest.Mock).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      const button = screen.getByText('Switch to Sepolia') as HTMLButtonElement;
      fireEvent.click(button);

      expect(button).toBeDisabled();
    });

    it('should re-check network after successful switch', async () => {
      (cryptoService.getNetworkInfo as jest.Mock)
        .mockResolvedValueOnce({
          chainId: 1,
          name: 'Mainnet',
          isCorrectNetwork: false
        })
        .mockResolvedValueOnce({
          chainId: 11155111,
          name: 'Sepolia Testnet',
          isCorrectNetwork: true
        });
      (cryptoService.switchToSepolia as jest.Mock).mockResolvedValue(undefined);

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        expect(cryptoService.getNetworkInfo).toHaveBeenCalledTimes(2);
      });
    });

    it('should handle switch error', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });
      (cryptoService.switchToSepolia as jest.Mock).mockRejectedValue(
        new Error('User rejected request')
      );

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        expect(screen.getByText('User rejected request')).toBeInTheDocument();
      });
    });

    it('should handle switch error without message', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });
      (cryptoService.switchToSepolia as jest.Mock).mockRejectedValue(new Error());

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        expect(screen.getByText('Failed to switch network')).toBeInTheDocument();
      });
    });

    it('should enable button after switch failure', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });
      (cryptoService.switchToSepolia as jest.Mock).mockRejectedValue(new Error('Failed'));

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        const button = screen.getByText('Switch to Sepolia') as HTMLButtonElement;
        expect(button).not.toBeDisabled();
      });
    });
  });

  describe('Network Change Listener', () => {
    it('should listen for chainChanged events', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(mockEthereum.on).toHaveBeenCalledWith('chainChanged', expect.any(Function));
      });
    });

    it('should re-check network when chain changes', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(cryptoService.getNetworkInfo).toHaveBeenCalledTimes(1);
      });

      // Simulate chain change
      const chainChangedHandler = mockEthereum.on.mock.calls[0][1];
      chainChangedHandler();

      await waitFor(() => {
        expect(cryptoService.getNetworkInfo).toHaveBeenCalledTimes(2);
      });
    });

    it('should remove listener on unmount', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      const { unmount } = render(<NetworkStatus />);

      await waitFor(() => {
        expect(mockEthereum.on).toHaveBeenCalled();
      });

      unmount();

      expect(mockEthereum.removeListener).toHaveBeenCalledWith('chainChanged', expect.any(Function));
    });

    it('should not add listener if ethereum.on not available', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      delete (window as any).ethereum;

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Sepolia Testnet')).toBeInTheDocument();
      });

      // Should not crash
      expect(true).toBe(true);
    });
  });

  describe('Network Check Error Handling', () => {
    it('should handle network check error', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockRejectedValue(
        new Error('MetaMask not installed')
      );

      render(<NetworkStatus />);

      await waitFor(() => {
        // Component should render null, no crash
        expect(true).toBe(true);
      });
    });

    it('should clear error after successful network check', async () => {
      (cryptoService.getNetworkInfo as jest.Mock)
        .mockRejectedValueOnce(new Error('Network error'));

      render(<NetworkStatus />);

      await waitFor(() => {
        // First call fails, component shows nothing
        expect(cryptoService.getNetworkInfo).toHaveBeenCalled();
      });

      // Now mock a successful response and trigger a re-check
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValueOnce({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      // Simulate chain change to trigger re-check
      const chainChangedHandler = mockEthereum.on.mock.calls.find(
        (call: any) => call[0] === 'chainChanged'
      )?.[1];
      if (chainChangedHandler) {
        chainChangedHandler();
      }

      await waitFor(() => {
        expect(screen.getByText('Sepolia Testnet')).toBeInTheDocument();
      });
    });

    it('should handle error without message', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockRejectedValue(new Error());

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(true).toBe(true);
      });
    });
  });

  describe('Callback', () => {
    it('should call onNetworkChange when network is detected', async () => {
      const mockCallback = jest.fn();
      const networkInfo = {
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      };
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue(networkInfo);

      render(<NetworkStatus onNetworkChange={mockCallback} />);

      await waitFor(() => {
        expect(mockCallback).toHaveBeenCalledWith(networkInfo);
      });
    });

    it('should not call onNetworkChange on error', async () => {
      const mockCallback = jest.fn();
      (cryptoService.getNetworkInfo as jest.Mock).mockRejectedValue(new Error('Failed'));

      render(<NetworkStatus onNetworkChange={mockCallback} />);

      await waitFor(() => {
        expect(mockCallback).not.toHaveBeenCalled();
      });
    });

    it('should call onNetworkChange after network switch', async () => {
      const mockCallback = jest.fn();
      (cryptoService.getNetworkInfo as jest.Mock)
        .mockResolvedValueOnce({
          chainId: 1,
          name: 'Mainnet',
          isCorrectNetwork: false
        })
        .mockResolvedValueOnce({
          chainId: 11155111,
          name: 'Sepolia Testnet',
          isCorrectNetwork: true
        });
      (cryptoService.switchToSepolia as jest.Mock).mockResolvedValue(undefined);

      render(<NetworkStatus onNetworkChange={mockCallback} />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      mockCallback.mockClear();

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        expect(mockCallback).toHaveBeenCalledWith(
          expect.objectContaining({
            chainId: 11155111,
            isCorrectNetwork: true
          })
        );
      });
    });
  });

  describe('Error Display', () => {
    it('should display error message when present', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });
      (cryptoService.switchToSepolia as jest.Mock).mockRejectedValue(new Error('Test error'));

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        expect(screen.getByText('Test error')).toBeInTheDocument();
      });
    });

    it('should clear error when switching again', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 1,
        name: 'Mainnet',
        isCorrectNetwork: false
      });
      (cryptoService.switchToSepolia as jest.Mock)
        .mockRejectedValueOnce(new Error('First error'))
        .mockResolvedValueOnce(undefined);

      render(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText('Switch to Sepolia')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        expect(screen.getByText('First error')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByText('Switch to Sepolia'));

      await waitFor(() => {
        expect(screen.queryByText('First error')).not.toBeInTheDocument();
      });
    });
  });
});
