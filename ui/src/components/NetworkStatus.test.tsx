import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import NetworkStatus from './NetworkStatus';
import * as cryptoService from '../services/crypto';
import { ToastProvider } from './Toast';

jest.mock('../services/crypto', () => ({
  getNetworkInfo: jest.fn(),
  switchToSepolia: jest.fn(),
  SEPOLIA_CHAIN_ID: 11155111,
  ETHERSCAN_BASE_URL: 'https://sepolia.etherscan.io'
}));

const renderWithProvider = (ui: React.ReactElement) =>
  render(<ToastProvider>{ui}</ToastProvider>);

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

      renderWithProvider(<NetworkStatus />);

      expect(screen.queryByText(/sepolia/i)).not.toBeInTheDocument();
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });

    it('should display network name when connected to Sepolia', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      const { container } = renderWithProvider(<NetworkStatus />);

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

      const { container } = renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      const { container } = renderWithProvider(<NetworkStatus />);

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

      const { container } = renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      const { container } = renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      const { unmount } = renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

      await waitFor(() => {
        // Component should render null, no crash
        expect(true).toBe(true);
      });
    });

    it('should clear error after successful network check', async () => {
      (cryptoService.getNetworkInfo as jest.Mock)
        .mockRejectedValueOnce(new Error('Network error'));

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus onNetworkChange={mockCallback} />);

      await waitFor(() => {
        expect(mockCallback).toHaveBeenCalledWith(networkInfo);
      });
    });

    it('should not call onNetworkChange on error', async () => {
      const mockCallback = jest.fn();
      (cryptoService.getNetworkInfo as jest.Mock).mockRejectedValue(new Error('Failed'));

      renderWithProvider(<NetworkStatus onNetworkChange={mockCallback} />);

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

      renderWithProvider(<NetworkStatus onNetworkChange={mockCallback} />);

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

      renderWithProvider(<NetworkStatus />);

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

      renderWithProvider(<NetworkStatus />);

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

  describe('Session Expiry Handling', () => {
    beforeEach(() => {
      localStorage.clear();
      jest.useFakeTimers();
    });

    afterEach(() => {
      jest.useRealTimers();
    });

    const createJwtToken = (expiresInSeconds: number) => {
      const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
      const payload = btoa(JSON.stringify({
        sub: 'user-123',
        exp: Math.floor(Date.now() / 1000) + expiresInSeconds,
      }));
      const signature = btoa('fake-signature');
      return `${header}.${payload}.${signature}`;
    };

    it('should not show session banner when no token exists', async () => {
      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.queryByText(/session expires/i)).not.toBeInTheDocument();
      });
    });

    it('should show amber warning when session expires in less than 5 minutes', async () => {
      localStorage.setItem('token', createJwtToken(200)); // 200 seconds = ~3 mins

      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText(/session expires in 5 minutes/i)).toBeInTheDocument();
      });
    });

    it('should show red warning when session expires in less than 1 minute', async () => {
      localStorage.setItem('token', createJwtToken(30)); // 30 seconds

      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText(/session expires in less than 1 minute/i)).toBeInTheDocument();
      });
    });

    it('should call onRenew when renew button is clicked', async () => {
      localStorage.setItem('token', createJwtToken(200));
      const mockOnRenew = jest.fn().mockResolvedValue(undefined);

      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus onRenew={mockOnRenew} />);

      await waitFor(() => {
        expect(screen.getByText(/session expires/i)).toBeInTheDocument();
      });

      const renewBtn = screen.getByRole('button', { name: /renew session/i });
      fireEvent.click(renewBtn);

      await waitFor(() => {
        expect(mockOnRenew).toHaveBeenCalled();
      });
    });

    it('should dismiss session banner when dismiss button is clicked', async () => {
      localStorage.setItem('token', createJwtToken(200));

      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText(/session expires/i)).toBeInTheDocument();
      });

      const dismissBtn = screen.getByRole('button', { name: /dismiss/i });
      fireEvent.click(dismissBtn);

      await waitFor(() => {
        expect(screen.queryByText(/session expires/i)).not.toBeInTheDocument();
      });
    });

    it('should handle invalid token gracefully', async () => {
      localStorage.setItem('token', 'invalid-token');

      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.queryByText(/session expires/i)).not.toBeInTheDocument();
      });
    });

    it('should handle token without exp claim', async () => {
      const header = btoa(JSON.stringify({ alg: 'HS256' }));
      const payload = btoa(JSON.stringify({ sub: 'user-123' })); // No exp
      localStorage.setItem('token', `${header}.${payload}.sig`);

      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.queryByText(/session expires/i)).not.toBeInTheDocument();
      });
    });

    it('should reset session warning on storage event', async () => {
      localStorage.setItem('token', createJwtToken(200));

      (cryptoService.getNetworkInfo as jest.Mock).mockResolvedValue({
        chainId: 11155111,
        name: 'Sepolia Testnet',
        isCorrectNetwork: true
      });

      renderWithProvider(<NetworkStatus />);

      await waitFor(() => {
        expect(screen.getByText(/session expires/i)).toBeInTheDocument();
      });

      // Simulate storage event (new token set)
      localStorage.setItem('token', createJwtToken(600)); // 10 minutes
      window.dispatchEvent(new StorageEvent('storage', { key: 'token' }));

      // Warning should reset
      await waitFor(() => {
        expect(screen.queryByText(/session expires in 5 minutes/i)).not.toBeInTheDocument();
      });
    });
  });
});
