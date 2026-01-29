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
});
