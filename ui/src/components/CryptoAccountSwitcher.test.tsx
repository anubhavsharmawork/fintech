import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import CryptoAccountSwitcher from './CryptoAccountSwitcher';
import * as cryptoService from '../services/crypto';

jest.mock('../services/crypto');

const mockGetETHBalance = cryptoService.getETHBalance as jest.Mock;
const mockGetFTKBalance = cryptoService.getFTKBalance as jest.Mock;
const mockGetTokenInfo = cryptoService.getTokenInfo as jest.Mock;
const mockIsValidAddress = cryptoService.isValidAddress as jest.Mock;
const mockGetEtherscanAddressUrl = cryptoService.getEtherscanAddressUrl as jest.Mock;
const mockGetEtherscanTokenUrl = cryptoService.getEtherscanTokenUrl as jest.Mock;

describe('CryptoAccountSwitcher Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetETHBalance.mockResolvedValue('1.5000');
    mockGetFTKBalance.mockResolvedValue('1000.00');
    mockGetTokenInfo.mockResolvedValue({ name: 'FintechToken', symbol: 'FTK', decimals: 18 });
    mockIsValidAddress.mockReturnValue(true);
    mockGetEtherscanAddressUrl.mockReturnValue('https://sepolia.etherscan.io/address/0x1234');
    mockGetEtherscanTokenUrl.mockReturnValue('https://sepolia.etherscan.io/token/0xFTK');
  });

  describe('Render Tests', () => {
    it('should render crypto balances title', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText('Crypto Balances')).toBeInTheDocument();
      });
    });

    it('should display card class', async () => {
      const { container } = render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(container.querySelector('.card')).toBeInTheDocument();
      });
    });

    it('should display refresh button', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).toBeInTheDocument();
      });
    });

    it('should have btn and btn-secondary classes on button', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).toHaveClass('btn', 'btn-secondary');
      });
    });
  });

  describe('Address Display', () => {
    it('should display shortened wallet address', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText(/0x123456/)).toBeInTheDocument();
      });
    });

    it('should display Wallet label', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText('Wallet:')).toBeInTheDocument();
      });
    });

    it('should display connect wallet message when no address provided', () => {
      render(<CryptoAccountSwitcher />);
      expect(screen.getByText('Connect your wallet to view real-time balances')).toBeInTheDocument();
    });
  });

  describe('Balance Display', () => {
    it('should display SepoliaETH balance', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText('SepoliaETH')).toBeInTheDocument();
        expect(screen.getByText('1.5000')).toBeInTheDocument();
      });
    });

    it('should display FTK token balance', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText(/FTK.*Token/)).toBeInTheDocument();
        expect(screen.getByText('1000.00')).toBeInTheDocument();
      });
    });

    it('should show For gas fees label under ETH balance', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText('For gas fees')).toBeInTheDocument();
      });
    });

    it('should handle zero balances', async () => {
      mockGetETHBalance.mockResolvedValue('0');
      mockGetFTKBalance.mockResolvedValue('0');

      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        const zeros = screen.getAllByText('0');
        expect(zeros.length).toBeGreaterThanOrEqual(2);
      });
    });
  });

  describe('Balance Fetching', () => {
    it('should call getETHBalance on mount', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(mockGetETHBalance).toHaveBeenCalledWith('0x1234567890abcdef1234567890abcdef12345678');
      });
    });

    it('should call getFTKBalance on mount', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(mockGetFTKBalance).toHaveBeenCalledWith('0x1234567890abcdef1234567890abcdef12345678');
      });
    });

    it('should not fetch if no address provided', () => {
      render(<CryptoAccountSwitcher />);
      expect(mockGetETHBalance).not.toHaveBeenCalled();
      expect(mockGetFTKBalance).not.toHaveBeenCalled();
    });
  });

  describe('Refresh Functionality', () => {
    it('should refresh balances when button clicked', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText(/Refresh Balances/)).toBeInTheDocument();
      });

      mockGetETHBalance.mockClear();
      mockGetFTKBalance.mockClear();

      fireEvent.click(screen.getByRole('button'));

      await waitFor(() => {
        expect(mockGetETHBalance).toHaveBeenCalled();
        expect(mockGetFTKBalance).toHaveBeenCalled();
      });
    });
  });

  describe('Error Handling', () => {
    it('should display error when balance fetch fails', async () => {
      mockGetETHBalance.mockRejectedValue(new Error('Network error'));

      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText(/Network error/)).toBeInTheDocument();
      });
    });

    it('should display error for invalid address', async () => {
      mockIsValidAddress.mockReturnValue(false);

      render(<CryptoAccountSwitcher address="invalid" />);

      await waitFor(() => {
        expect(screen.getByText(/Invalid wallet address/)).toBeInTheDocument();
      });
    });
  });

  describe('Demo Mode', () => {
    it('should show Demo Data button text when isDemo is true', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/Demo Data/)).toBeInTheDocument();
      });
    });

    it('should show (Demo) label next to address in demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/\(Demo\)/)).toBeInTheDocument();
      });
    });

    it('should disable refresh button in demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" isDemo={true} />);

      await waitFor(() => {
        const button = screen.getByRole('button');
        expect(button).toBeDisabled();
      });
    });

    it('should show sample balances message in demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/Sample balances shown for demonstration purposes/)).toBeInTheDocument();
      });
    });

    it('should not call balance APIs in demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" isDemo={true} />);

      await waitFor(() => {
        expect(screen.getByText(/Demo Data/)).toBeInTheDocument();
      });

      expect(mockGetETHBalance).not.toHaveBeenCalled();
      expect(mockGetFTKBalance).not.toHaveBeenCalled();
    });
  });

  describe('Token Link', () => {
    it('should show View contract link by default', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText(/View contract/)).toBeInTheDocument();
      });
    });

    it('should hide View contract link when showTokenLink is false', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" showTokenLink={false} />);

      await waitFor(() => {
        expect(screen.getByText('SepoliaETH')).toBeInTheDocument();
      });

      expect(screen.queryByText(/View contract/)).not.toBeInTheDocument();
    });
  });

  describe('Last Updated', () => {
    it('should show Updated timestamp after fetch', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText(/Updated:/)).toBeInTheDocument();
      });
    });
  });

  describe('Blockchain Info Message', () => {
    it('should display RPC message for non-demo', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" />);

      await waitFor(() => {
        expect(screen.getByText(/Balances are fetched directly from Sepolia blockchain via RPC/)).toBeInTheDocument();
      });
    });

    it('should not show RPC message in demo mode', async () => {
      render(<CryptoAccountSwitcher address="0x1234567890abcdef1234567890abcdef12345678" isDemo={true} />);

      await waitFor(() => {
        expect(screen.queryByText(/Balances are fetched directly from Sepolia blockchain via RPC/)).not.toBeInTheDocument();
      });
    });
  });
});
