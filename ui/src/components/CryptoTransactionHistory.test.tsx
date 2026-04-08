import React from 'react';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import CryptoTransactionHistory from './CryptoTransactionHistory';
import * as cryptoService from '../services/crypto';

// Mock crypto service
jest.mock('../services/crypto', () => ({
  getRecentTransfers: jest.fn(),
  getEtherscanAddressUrl: jest.fn(addr => `url/addr/${addr}`),
  getEtherscanTokenUrl: jest.fn(() => `url/token`),
  FTK_TOKEN_ADDRESS: '0xftk'
}));

describe('CryptoTransactionHistory Component', () => {
  const mockAddress = '0x1234567890123456789012345678901234567890';
  const mockTransfers = [
    {
      txHash: '0xhash1',
      from: mockAddress, // Outgoing
      to: '0xreceiver1',
      amount: '100.0',
      blockNumber: 100,
      etherscanUrl: 'url/hash1'
    },
    {
      txHash: '0xhash2',
      from: '0xsomesender',
      to: mockAddress, // Incoming
      amount: '50.0',
      blockNumber: 101,
      etherscanUrl: 'url/hash2'
    }
  ];

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders nothing when address is null', () => {
    const { container } = render(<CryptoTransactionHistory address={null} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('loads and displays transfers initially', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue(mockTransfers);

    render(<CryptoTransactionHistory address={mockAddress} />);

    // Should show loading or just wait
    expect(cryptoService.getRecentTransfers).toHaveBeenCalledWith(mockAddress, 5000);

    await waitFor(() => {
      expect(screen.getByText('On-Chain FTK Transfers')).toBeInTheDocument();
    });

    // Check outgoing
    expect(screen.getByText('100.0 FTK')).toBeInTheDocument();
    // Check incoming
    expect(screen.getByText('50.0 FTK')).toBeInTheDocument();
  });

  it('displays error message on fetch failure', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockRejectedValue(new Error('Fetch failed'));

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText('Fetch failed')).toBeInTheDocument();
    });
  });

  it('refresh button reloads data', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue(mockTransfers);

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText('On-Chain FTK Transfers')).toBeInTheDocument();
    });

    const refreshBtn = screen.getByText(/Refresh/i);
    fireEvent.click(refreshBtn);

    expect(cryptoService.getRecentTransfers).toHaveBeenCalledTimes(2);
  });

  it('shows empty state when transfers list is empty', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue([]);

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText(/No FTK transfers found/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/Make a transfer to see it appear here/i)).toBeInTheDocument();
  });

  it('shows loading state while fetching', async () => {
    let resolveTransfers: (v: any) => void;
    const promise = new Promise<any>((res) => { resolveTransfers = res; });
    (cryptoService.getRecentTransfers as jest.Mock).mockReturnValue(promise);

    render(<CryptoTransactionHistory address={mockAddress} />);

    expect(screen.getByText(/Loading on-chain transfers/i)).toBeInTheDocument();

    resolveTransfers!([]);
    await waitFor(() => {
      expect(screen.queryByText(/Loading on-chain transfers/i)).not.toBeInTheDocument();
    });
  });

  it('marks outgoing transfers (from === address)', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue([mockTransfers[0]]);

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText('100.0 FTK')).toBeInTheDocument();
    });
    // outgoing — the transfer row links to the etherscan URL
    const link = screen.getByRole('link', { name: /100\.0 FTK/i });
    expect(link).toHaveAttribute('href', 'url/hash1');
  });

  it('marks incoming transfers (to === address)', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue([mockTransfers[1]]);

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText('50.0 FTK')).toBeInTheDocument();
    });
    const link = screen.getByRole('link', { name: /50\.0 FTK/i });
    expect(link).toHaveAttribute('href', 'url/hash2');
  });

  it('renders "View all on Etherscan" link for the wallet address', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue([]);

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText(/No FTK transfers found/i)).toBeInTheDocument();
    });

    const etherscanLink = screen.getByText(/View all on Etherscan/i).closest('a');
    expect(etherscanLink).toHaveAttribute('href', `url/addr/${mockAddress}`);
    expect(etherscanLink).toHaveAttribute('target', '_blank');
  });

  it('renders "View Token" link', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue([]);

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText(/No FTK transfers found/i)).toBeInTheDocument();
    });

    const tokenLink = screen.getByText(/View Token/i).closest('a');
    expect(tokenLink).toHaveAttribute('href', 'url/token');
  });

  it('shows error message with fallback text when error has no message', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockRejectedValue({});

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load transfer history')).toBeInTheDocument();
    });
  });

  it('reloads when refreshTrigger prop changes', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue([]);

    const { rerender } = render(<CryptoTransactionHistory address={mockAddress} refreshTrigger={0} />);

    await waitFor(() => {
      expect(cryptoService.getRecentTransfers).toHaveBeenCalledTimes(1);
    });

    rerender(<CryptoTransactionHistory address={mockAddress} refreshTrigger={1} />);

    await waitFor(() => {
      expect(cryptoService.getRecentTransfers).toHaveBeenCalledTimes(2);
    });
  });

  it('renders transfer rows with external link target', async () => {
    (cryptoService.getRecentTransfers as jest.Mock).mockResolvedValue(mockTransfers);

    render(<CryptoTransactionHistory address={mockAddress} />);

    await waitFor(() => {
      expect(screen.getByText('100.0 FTK')).toBeInTheDocument();
    });

    const transferLinks = screen.getAllByRole('link').filter(
      (l) => l.getAttribute('href')?.startsWith('url/hash')
    );
    transferLinks.forEach((link) => {
      expect(link).toHaveAttribute('target', '_blank');
      expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    });
  });
});
