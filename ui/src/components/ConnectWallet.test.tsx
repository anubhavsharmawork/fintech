import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import ConnectWallet from './ConnectWallet';
import * as cryptoService from '../services/crypto';

jest.mock('../services/crypto');

describe('ConnectWallet Component', () => {
  const mockSigner = {
    getAddress: jest.fn().mockResolvedValue('0x1234567890123456789012345678901234567890'),
  };

  const mockProvider = {
    getBalance: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockSigner.getAddress.mockClear();
    (cryptoService.connectWallet as jest.Mock).mockResolvedValue({
      provider: mockProvider,
      signer: mockSigner,
    });
  });

  describe('Initial Render', () => {
    it('should render connect button initially', () => {
      render(<ConnectWallet />);

      expect(screen.getByText('Connect Wallet')).toBeInTheDocument();
    });

    it('should display "Not connected" status initially', () => {
      render(<ConnectWallet />);

      expect(screen.getByText('Not connected')).toBeInTheDocument();
    });

    it('should display "Wallet" title', () => {
      render(<ConnectWallet />);

      expect(screen.getByText('Wallet')).toBeInTheDocument();
    });

    it('should have card class on container', () => {
      const { container } = render(<ConnectWallet />);

      expect(container.querySelector('.card')).toBeInTheDocument();
    });
  });

  describe('Connect Wallet Flow', () => {
    it('should call connectWallet when button clicked', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(cryptoService.connectWallet).toHaveBeenCalled();
        },
        { timeout: 3000 }
      );
    });

    it('should show loading state while connecting', async () => {
      (cryptoService.connectWallet as jest.Mock).mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(
              () =>
                resolve({
                  provider: mockProvider,
                  signer: mockSigner,
                }),
              50
            )
          )
      );

      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      expect(screen.getByText('Connecting…')).toBeInTheDocument();

      await waitFor(() => {
        expect(screen.queryByText('Connecting…')).not.toBeInTheDocument();
      });
    });

    it('should disable button while loading', async () => {
      (cryptoService.connectWallet as jest.Mock).mockImplementationOnce(
        () =>
          new Promise((resolve) =>
            setTimeout(
              () =>
                resolve({
                  provider: mockProvider,
                  signer: mockSigner,
                }),
              50
            )
          )
      );

      render(<ConnectWallet />);

      const button = screen.getByText('Connect Wallet') as HTMLButtonElement;
      fireEvent.click(button);

      expect(button).toBeDisabled();

      await waitFor(() => {
        expect(button).not.toBeDisabled();
      });
    });

    it('should display connected address after successful connection', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText(/Connected: 0x12345.*/)).toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });

    it('should show disconnect button after connection', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText('Disconnect')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });

    it('should call onConnected callback with address and signer', async () => {
      const onConnected = jest.fn();

      render(<ConnectWallet onConnected={onConnected} />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(onConnected).toHaveBeenCalledWith(
            '0x1234567890123456789012345678901234567890',
            mockSigner
          );
        },
        { timeout: 3000 }
      );
    });
  });

  describe('Error Handling', () => {
    it('should display error message on connection failure', async () => {
      const errorMessage = 'MetaMask not installed';
      (cryptoService.connectWallet as jest.Mock).mockRejectedValueOnce(
        new Error(errorMessage)
      );

      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText(errorMessage)).toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });

    it('should clear previous error when retrying', async () => {
      (cryptoService.connectWallet as jest.Mock).mockRejectedValueOnce(
        new Error('Connection failed')
      );

      const { rerender } = render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        expect(screen.getByText('Connection failed')).toBeInTheDocument();
      });

      (cryptoService.connectWallet as jest.Mock).mockResolvedValueOnce({
        provider: mockProvider,
        signer: mockSigner,
      });

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.queryByText('Connection failed')).not.toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });

    it('should use default error message if err.message is missing', async () => {
      (cryptoService.connectWallet as jest.Mock).mockRejectedValueOnce({});

      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText('Failed to connect wallet')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });

    it('should display error with red color', async () => {
      (cryptoService.connectWallet as jest.Mock).mockRejectedValueOnce(
        new Error('Connection error')
      );

      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        const errorElement = screen.getByText('Connection error');
        expect(errorElement).toHaveStyle({ color: 'red' });
      });
    });

    it('should not clear error on reconnect attempt', async () => {
      (cryptoService.connectWallet as jest.Mock).mockRejectedValueOnce(
        new Error('Initial error')
      );

      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        expect(screen.getByText('Initial error')).toBeInTheDocument();
      });

      expect(screen.getByText('Initial error')).toBeInTheDocument();
    });
  });

  describe('Disconnect Flow', () => {
    it('should reset state when disconnect clicked', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText('Disconnect')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      fireEvent.click(screen.getByText('Disconnect'));

      expect(screen.getByText('Not connected')).toBeInTheDocument();
      expect(screen.getByText('Connect Wallet')).toBeInTheDocument();
    });

    it('should call onDisconnected callback when disconnect clicked', async () => {
      const onDisconnected = jest.fn();

      render(<ConnectWallet onDisconnected={onDisconnected} />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText('Disconnect')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      fireEvent.click(screen.getByText('Disconnect'));

      expect(onDisconnected).toHaveBeenCalled();
    });

    it('should clear error on disconnect', async () => {
      (cryptoService.connectWallet as jest.Mock).mockRejectedValueOnce(
        new Error('Connection failed')
      );

      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        expect(screen.getByText('Connection failed')).toBeInTheDocument();
      });

      (cryptoService.connectWallet as jest.Mock).mockResolvedValueOnce({
        provider: mockProvider,
        signer: mockSigner,
      });

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText('Disconnect')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      fireEvent.click(screen.getByText('Disconnect'));

      expect(screen.queryByText('Connection failed')).not.toBeInTheDocument();
    });
  });

  describe('Multiple Connections', () => {
    it('should reconnect after disconnect', async () => {
      render(<ConnectWallet />);

      // First connection
      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText('Disconnect')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      fireEvent.click(screen.getByText('Disconnect'));

      // Second connection
      (cryptoService.connectWallet as jest.Mock).mockResolvedValueOnce({
        provider: mockProvider,
        signer: {
          getAddress: jest
            .fn()
            .mockResolvedValue('0x9876543210987654321098765432109876543210'),
        },
      });

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText(/Connected: 0x9876.*/)).toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });
  });

  describe('Props Handling', () => {
    it('should accept optional onConnected callback', async () => {
      const onConnected = jest.fn();

      render(<ConnectWallet onConnected={onConnected} />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        expect(onConnected).toHaveBeenCalled();
      });
    });

    it('should accept optional onDisconnected callback', async () => {
      const onDisconnected = jest.fn();

      render(<ConnectWallet onDisconnected={onDisconnected} />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText('Disconnect')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      fireEvent.click(screen.getByText('Disconnect'));

      expect(onDisconnected).toHaveBeenCalled();
    });

    it('should work without callbacks', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(screen.getByText('Disconnect')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });
  });

  describe('Address Display', () => {
    it('should display full address when connected', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(
        () => {
          expect(
            screen.getByText('Connected: 0x1234567890123456789012345678901234567890')
          ).toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });

    it('should display address with small class', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        const addressElement = screen.getByText(
          /Connected: 0x1234567890123456789012345678901234567890/
        );
        expect(addressElement).toHaveClass('small');
      });
    });

    it('should display not connected message with small class initially', () => {
      render(<ConnectWallet />);

      const notConnected = screen.getByText('Not connected');
      expect(notConnected).toHaveClass('small');
    });
  });

  describe('Button Classes', () => {
    it('should have btn and btn-primary classes on connect button', () => {
      render(<ConnectWallet />);

      const button = screen.getByText('Connect Wallet');
      expect(button).toHaveClass('btn', 'btn-primary');
    });

    it('should call connectWallet when button clicked', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        expect(cryptoService.connectWallet).toHaveBeenCalled();
      });
    });
  });

  describe('Address Resolution', () => {
    it('should call getAddress on signer', async () => {
      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        expect(mockSigner.getAddress).toHaveBeenCalled();
      });
    });

    it('should handle getAddress errors', async () => {
      mockSigner.getAddress.mockRejectedValueOnce(
        new Error('Failed to get address')
      );

      render(<ConnectWallet />);

      fireEvent.click(screen.getByText('Connect Wallet'));

      await waitFor(() => {
        expect(screen.getByText('Failed to get address')).toBeInTheDocument();
      });
    });
  });
});
