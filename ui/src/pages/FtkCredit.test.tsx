import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import FtkCredit from './FtkCredit';
import { ToastProvider } from '../components/Toast';
import * as fModeHook from '../hooks/useFMode';
import * as creditService from '../services/credit';

jest.mock('../hooks/useFMode');
jest.mock('../services/credit');
jest.mock('../components/ConnectWallet', () => ({
  __esModule: true,
  default: ({ onConnected, onDisconnected }: any) => (
    <div data-testid="connect-wallet">
      <button onClick={() => onConnected('0xWalletAddress123')}>Connect Wallet</button>
      <button onClick={() => onDisconnected()}>Disconnect</button>
    </div>
  ),
}));
jest.mock('../components/charts/ChartShell', () => ({
  __esModule: true,
  default: ({ children }: any) => <div data-testid="chart-shell">{children}</div>,
}));
jest.mock('recharts', () => ({
  RadialBarChart: ({ children }: any) => <div data-testid="radial-chart">{children}</div>,
  RadialBar: () => <div data-testid="radial-bar" />,
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
}));

const mockFacility = {
  id: 'fac1',
  userId: 'user1',
  walletAddress: '0xWalletAddress123',
  creditLimit: 10000,
  drawnAmount: 2000,
  outstandingBalance: 2000,
  availableCredit: 8000,
  currency: 'FTK',
  status: 'Active',
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-15T00:00:00Z',
};

const mockRepayments = [
  {
    id: 'rep1',
    facilityId: 'fac1',
    amount: 500,
    currency: 'FTK',
    status: 'Completed',
    createdAt: '2024-01-10T00:00:00Z',
  },
];

describe('FtkCredit Page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (window as any).ethereum = undefined;
    (fModeHook.useFMode as jest.Mock).mockReturnValue({ enabled: true, toggle: jest.fn() });
    (creditService.getCreditFacility as jest.Mock).mockResolvedValue(mockFacility);
    (creditService.getRepayments as jest.Mock).mockResolvedValue(mockRepayments);
    (creditService.requestDrawdown as jest.Mock).mockResolvedValue({ ...mockFacility, drawnAmount: 3000, availableCredit: 7000 });
    (creditService.submitRepayment as jest.Mock).mockResolvedValue({ facility: { ...mockFacility, drawnAmount: 1500 }, repayment: mockRepayments[0] });
  });

  const renderComponent = () =>
    render(
      <BrowserRouter>
        <ToastProvider>
          <FtkCredit />
        </ToastProvider>
      </BrowserRouter>,
    );

  describe('fiat mode gate', () => {
    it('shows fiat-mode message when F-Mode is disabled', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({ enabled: false, toggle: jest.fn() });
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText(/switch to f-mode/i)).toBeInTheDocument();
        expect(screen.getByText('Fiat Mode')).toBeInTheDocument();
      });
    });

    it('does not show fiat message when F-Mode is enabled', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.queryByText(/switch to f-mode/i)).not.toBeInTheDocument();
      });
    });
  });

  describe('F-Mode active — wallet not connected', () => {
    it('shows connect wallet prompt after wallet check', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText(/connect your metamask wallet/i)).toBeInTheDocument();
      });
    });

    it('renders ConnectWallet component', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByTestId('connect-wallet')).toBeInTheDocument();
      });
    });

    it('shows hero title', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('Drawdown & Repayments')).toBeInTheDocument();
      });
    });
  });

  describe('after wallet connection', () => {
    const connectWallet = async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByTestId('connect-wallet')).toBeInTheDocument());
      fireEvent.click(screen.getByText('Connect Wallet'));
    };

    it('loads credit facility after connecting', async () => {
      await connectWallet();
      await waitFor(() => {
        expect(creditService.getCreditFacility).toHaveBeenCalledWith('0xWalletAddress123');
        expect(creditService.getRepayments).toHaveBeenCalledWith('0xWalletAddress123');
      });
    });

    it('displays credit facility overview', async () => {
      await connectWallet();
      await waitFor(() => {
        expect(screen.getByLabelText('Credit facility overview')).toBeInTheDocument();
      });
    });

    it('shows credit limit value', async () => {
      await connectWallet();
      await waitFor(() => {
        expect(screen.getByText(/10,000\.00/)).toBeInTheDocument();
      });
    });

    it('shows available credit', async () => {
      await connectWallet();
      await waitFor(() => {
        expect(screen.getByText(/8,000\.00/)).toBeInTheDocument();
      });
    });

    it('shows drawdown form', async () => {
      await connectWallet();
      await waitFor(() => {
        expect(screen.getByLabelText(/drawdown amount/i)).toBeInTheDocument();
      });
    });

    it('shows repayment form', async () => {
      await connectWallet();
      await waitFor(() => {
        expect(screen.getByLabelText(/repayment amount/i)).toBeInTheDocument();
      });
    });

    it('shows repayments history section', async () => {
      await connectWallet();
      await waitFor(() => {
        expect(screen.getByText(/repayment history/i)).toBeInTheDocument();
      });
    });
  });

  describe('drawdown', () => {
    const connectAndLoad = async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByTestId('connect-wallet')).toBeInTheDocument());
      fireEvent.click(screen.getByText('Connect Wallet'));
      await waitFor(() => expect(screen.getByLabelText(/drawdown amount/i)).toBeInTheDocument());
    };

    it('submits drawdown with valid amount', async () => {
      await connectAndLoad();
      fireEvent.change(screen.getByLabelText(/drawdown amount/i), { target: { value: '500' } });
      const form = screen.getByLabelText(/drawdown amount/i).closest('form')!;
      fireEvent.submit(form);
      await waitFor(() => {
        expect(creditService.requestDrawdown).toHaveBeenCalledWith('0xWalletAddress123', 500);
      });
    });

    it('does not submit drawdown with zero amount', async () => {
      await connectAndLoad();
      fireEvent.change(screen.getByLabelText(/drawdown amount/i), { target: { value: '0' } });
      const form = screen.getByLabelText(/drawdown amount/i).closest('form')!;
      fireEvent.submit(form);
      await waitFor(() => {
        expect(creditService.requestDrawdown).not.toHaveBeenCalled();
      });
    });

    it('shows toast error on drawdown failure', async () => {
      (creditService.requestDrawdown as jest.Mock).mockRejectedValue(new Error('Drawdown failed'));
      await connectAndLoad();
      fireEvent.change(screen.getByLabelText(/drawdown amount/i), { target: { value: '1000' } });
      const form = screen.getByLabelText(/drawdown amount/i).closest('form')!;
      fireEvent.submit(form);
      await waitFor(() => {
        expect(screen.getByText('Drawdown failed')).toBeInTheDocument();
      });
    });
  });

  describe('repayment', () => {
    const connectAndLoad = async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByTestId('connect-wallet')).toBeInTheDocument());
      fireEvent.click(screen.getByText('Connect Wallet'));
      await waitFor(() => expect(screen.getByLabelText(/repayment amount/i)).toBeInTheDocument());
    };

    it('submits repayment with valid amount', async () => {
      await connectAndLoad();
      fireEvent.change(screen.getByLabelText(/repayment amount/i), { target: { value: '250' } });
      const forms = document.querySelectorAll('form');
      const repaymentForm = Array.from(forms).find(f => f.querySelector('[aria-label*="Repayment"]') || f.contains(screen.getByLabelText(/repayment amount/i)));
      if (repaymentForm) fireEvent.submit(repaymentForm);
      await waitFor(() => {
        expect(creditService.submitRepayment).toHaveBeenCalledWith('0xWalletAddress123', 250);
      });
    });

    it('shows toast error on repayment failure', async () => {
      (creditService.submitRepayment as jest.Mock).mockRejectedValue(new Error('Repayment failed'));
      await connectAndLoad();
      fireEvent.change(screen.getByLabelText(/repayment amount/i), { target: { value: '100' } });
      const forms = document.querySelectorAll('form');
      const repaymentForm = Array.from(forms).find(f => f.contains(screen.getByLabelText(/repayment amount/i)));
      if (repaymentForm) fireEvent.submit(repaymentForm);
      await waitFor(() => {
        expect(screen.getByText('Repayment failed')).toBeInTheDocument();
      });
    });
  });

  describe('load error', () => {
    it('shows error message and retry button on load failure', async () => {
      (creditService.getCreditFacility as jest.Mock).mockRejectedValue(new Error('Failed to load'));
      renderComponent();
      await waitFor(() => expect(screen.getByTestId('connect-wallet')).toBeInTheDocument());
      fireEvent.click(screen.getByText('Connect Wallet'));
      await waitFor(() => {
        expect(screen.getByText(/failed to load/i)).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
      });
    });
  });

  describe('wallet disconnect', () => {
    it('clears facility on disconnect', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByTestId('connect-wallet')).toBeInTheDocument());
      fireEvent.click(screen.getByText('Connect Wallet'));
      await waitFor(() => expect(screen.getByLabelText('Credit facility overview')).toBeInTheDocument());
      fireEvent.click(screen.getByText('Disconnect'));
      await waitFor(() => {
        expect(screen.queryByLabelText('Credit facility overview')).not.toBeInTheDocument();
      });
    });
  });
});
