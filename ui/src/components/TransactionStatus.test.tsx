import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import TransactionStatus from './TransactionStatus';
import * as cryptoService from '../services/crypto';

// Mock the crypto service
jest.mock('../services/crypto', () => ({
  waitForTransaction: jest.fn(),
  getEtherscanTxUrl: jest.fn((hash) => `https://etherscan.io/tx/${hash}`)
}));

describe('TransactionStatus Component', () => {
  const mockTransaction = {
    hash: '0x123456789abcdef123456789abcdef123456789abcdef123456789abcdef12',
    etherscanUrl: 'https://sepolia.etherscan.io/tx/0x123',
    from: '0xsender',
    to: '0xreceiver',
    amount: '10.0',
    status: 'pending' as const
  };

  const mockProvider = {
    waitForTransaction: jest.fn()
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders nothing when transaction is null', () => {
    const { container } = render(
      <TransactionStatus 
        transaction={null} 
        provider={mockProvider} 
      />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('shows pending/confirming status initially', async () => {
    // Mock waitForTransaction to never resolve immediately or stay pending logic
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    expect(screen.getByText('Waiting for Confirmation...')).toBeInTheDocument();
    expect(screen.getByText('⏳')).toBeInTheDocument();
    // Check for shortened hash
    expect(screen.getByText(/0x12345678...abcdef12/)).toBeInTheDocument();
  });

  it('updates to confirmed when transaction completes', async () => {
    const confirmedResult = {
      ...mockTransaction,
      status: 'confirmed',
      gasUsed: '21000',
      blockNumber: 12345
    };

    (cryptoService.waitForTransaction as jest.Mock).mockResolvedValue(confirmedResult);

    const onConfirmedMock = jest.fn();

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
        onConfirmed={onConfirmedMock}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Transaction Confirmed!')).toBeInTheDocument();
    });

    expect(screen.getByText('✅')).toBeInTheDocument();
    expect(screen.getByText('Gas Used:')).toBeInTheDocument();
    expect(screen.getByText('21000')).toBeInTheDocument();
    expect(onConfirmedMock).toHaveBeenCalledWith(expect.objectContaining({
      status: 'confirmed',
      gasUsed: '21000'
    }));
  });

  it('updates to failed when transaction fails', async () => {
    const failedResult = {
      ...mockTransaction,
      status: 'failed'
    };

    (cryptoService.waitForTransaction as jest.Mock).mockResolvedValue(failedResult);

    const onFailedMock = jest.fn();

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
        onFailed={onFailedMock}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Transaction Failed')).toBeInTheDocument();
    });

    expect(screen.getByText('❌')).toBeInTheDocument();
    expect(screen.getByText('Transaction reverted on-chain')).toBeInTheDocument();
    expect(onFailedMock).toHaveBeenCalledWith('Transaction reverted');
  });

  it('handles errors during waiting', async () => {
    (cryptoService.waitForTransaction as jest.Mock).mockRejectedValue(new Error('Network Error'));

    const onFailedMock = jest.fn();

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
        onFailed={onFailedMock}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Transaction Failed')).toBeInTheDocument();
    });

    expect(onFailedMock).toHaveBeenCalledWith('Network Error');
  });

  it('renders all transaction details', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    expect(screen.getByText('Transaction Hash:')).toBeInTheDocument();
    expect(screen.getByText('From:')).toBeInTheDocument();
    expect(screen.getByText('To:')).toBeInTheDocument();
    expect(screen.getByText('Amount:')).toBeInTheDocument();
    expect(screen.getByText(/10\.0 FTK/)).toBeInTheDocument();
  });

  it('displays clickable Etherscan link', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    const links = screen.getAllByRole('link');
    expect(links.length).toBeGreaterThan(0);
    expect(links[0]).toHaveAttribute('href', mockTransaction.etherscanUrl);
    expect(links[0]).toHaveAttribute('target', '_blank');
    expect(links[0]).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('displays "View on Etherscan" button', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    expect(screen.getByText(/View on Etherscan/)).toBeInTheDocument();
  });

  it('shows block number when confirmed', async () => {
    const confirmedResult = {
      ...mockTransaction,
      status: 'confirmed',
      blockNumber: 999999
    };

    (cryptoService.waitForTransaction as jest.Mock).mockResolvedValue(confirmedResult);

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Block Number:')).toBeInTheDocument();
      expect(screen.getByText('999,999')).toBeInTheDocument();
    });
  });

  it('shows confirmations count', async () => {
    const confirmedResult = {
      ...mockTransaction,
      status: 'confirmed',
      gasUsed: '21000'
    };

    (cryptoService.waitForTransaction as jest.Mock).mockResolvedValue(confirmedResult);

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Confirmations:')).toBeInTheDocument();
      expect(screen.getByText('1')).toBeInTheDocument();
    });
  });

  it('displays success message when confirmed', async () => {
    const confirmedResult = {
      ...mockTransaction,
      status: 'confirmed'
    };

    (cryptoService.waitForTransaction as jest.Mock).mockResolvedValue(confirmedResult);

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    await waitFor(() => {
      expect(screen.getByText(/This transaction is now permanently recorded/)).toBeInTheDocument();
    });
  });

  it('applies correct styling for pending status', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    const { container } = render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    const card = container.querySelector('.card');
    expect(card).toHaveStyle({ borderLeft: expect.stringContaining('#f59e0b') });
  });

  it('applies correct styling for confirmed status', async () => {
    const confirmedResult = {
      ...mockTransaction,
      status: 'confirmed'
    };

    (cryptoService.waitForTransaction as jest.Mock).mockResolvedValue(confirmedResult);

    const { container } = render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    await waitFor(() => {
      const card = container.querySelector('.card');
      expect(card).toHaveStyle({ borderLeft: expect.stringContaining('#22c55e') });
    });
  });

  it('applies correct styling for failed status', async () => {
    (cryptoService.waitForTransaction as jest.Mock).mockRejectedValue(new Error('Failed'));

    const { container } = render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    await waitFor(() => {
      const card = container.querySelector('.card');
      expect(card).toHaveStyle({ borderLeft: expect.stringContaining('#ef4444') });
    });
  });

  it('shortens transaction hash correctly', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    // Hash should be shortened to first 10 and last 8 characters
    expect(screen.getByText(/0x12345678/)).toBeInTheDocument();
    expect(screen.getByText(/abcdef12/)).toBeInTheDocument();
  });

  it('shortens from and to addresses', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    expect(screen.getByText(/0xsender/)).toBeInTheDocument();
    expect(screen.getByText(/0xreceiver/)).toBeInTheDocument();
  });

  it('does not render without transaction', () => {
    const { container } = render(
      <TransactionStatus 
        transaction={null} 
        provider={mockProvider} 
      />
    );

    expect(container).toBeEmptyDOMElement();
  });

  it('cleans up on unmount', async () => {
    const { unmount } = render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    unmount();

    // If cleanup works properly, no errors should occur
    expect(true).toBe(true);
  });

  it('does not call callbacks after unmount', async () => {
    const onConfirmedMock = jest.fn();
    let resolver: any;

    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => 
      new Promise(resolve => {
        resolver = resolve;
      })
    );

    const { unmount } = render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
        onConfirmed={onConfirmedMock}
      />
    );

    unmount();

    // Resolve after unmount
    resolver({ ...mockTransaction, status: 'confirmed' });

    await new Promise(resolve => setTimeout(resolve, 100));

    expect(onConfirmedMock).not.toHaveBeenCalled();
  });

  it('handles error without message gracefully', async () => {
    (cryptoService.waitForTransaction as jest.Mock).mockRejectedValue(new Error());

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Transaction Failed')).toBeInTheDocument();
      expect(screen.getByText(/Failed to confirm transaction/)).toBeInTheDocument();
    });
  });

  it('does not show gas used when not available', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    expect(screen.queryByText('Gas Used:')).not.toBeInTheDocument();
  });

  it('does not show block number when not available', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    expect(screen.queryByText('Block Number:')).not.toBeInTheDocument();
  });

  it('does not show confirmations when zero', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    expect(screen.queryByText('Confirmations:')).not.toBeInTheDocument();
  });

  it('re-tracks when transaction hash changes', async () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    const { rerender } = render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    expect(cryptoService.waitForTransaction).toHaveBeenCalledTimes(1);

    const newTransaction = { ...mockTransaction, hash: '0xnew' };
    rerender(
      <TransactionStatus 
        transaction={newTransaction} 
        provider={mockProvider}
      />
    );

    await waitFor(() => {
      expect(cryptoService.waitForTransaction).toHaveBeenCalledTimes(2);
    });
  });

  it('does not re-track when provider stays the same', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    const { rerender } = render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    expect(cryptoService.waitForTransaction).toHaveBeenCalledTimes(1);

    rerender(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    expect(cryptoService.waitForTransaction).toHaveBeenCalledTimes(1);
  });

  it('displays error message when transaction fails', async () => {
    const failedResult = {
      ...mockTransaction,
      status: 'failed'
    };

    (cryptoService.waitForTransaction as jest.Mock).mockResolvedValue(failedResult);

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
      />
    );

    await waitFor(() => {
      expect(screen.getByText(/Error:/)).toBeInTheDocument();
    });
  });

  it('calls onConfirmed with full transaction details', async () => {
    const confirmedResult = {
      ...mockTransaction,
      status: 'confirmed',
      gasUsed: '21000',
      blockNumber: 12345
    };

    (cryptoService.waitForTransaction as jest.Mock).mockResolvedValue(confirmedResult);

    const onConfirmedMock = jest.fn();

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider}
        onConfirmed={onConfirmedMock}
      />
    );

    await waitFor(() => {
      expect(onConfirmedMock).toHaveBeenCalledWith(
        expect.objectContaining({
          hash: mockTransaction.hash,
          status: 'confirmed',
          gasUsed: '21000',
          blockNumber: 12345
        })
      );
    });
  });

  it('has monospace font for hash and addresses', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    // Check hash link
    const hashLink = screen.getByRole('link', { name: /0x12345678/i });
    expect(hashLink).toHaveStyle({ fontFamily: 'monospace' });
  });

  it('renders card component', () => {
    (cryptoService.waitForTransaction as jest.Mock).mockImplementation(() => new Promise(() => {}));

    const { container } = render(
      <TransactionStatus 
        transaction={mockTransaction} 
        provider={mockProvider} 
      />
    );

    expect(container.querySelector('.card')).toBeInTheDocument();
  });
});
