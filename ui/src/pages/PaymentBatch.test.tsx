
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import PaymentBatchPage from './PaymentBatch';
import * as corporate from '../services/corporate';

jest.mock('../services/corporate');

const mockGetPaymentBatches = corporate.getPaymentBatches as jest.Mock;

describe('PaymentBatchPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render loading state', () => {
    mockGetPaymentBatches.mockImplementation(() => new Promise(() => {}));

    render(
      <BrowserRouter>
        <PaymentBatchPage />
      </BrowserRouter>
    );

    expect(screen.getByRole('heading', { name: /payment batches/i })).toBeInTheDocument();
  });

  it('should load and display payment batches', async () => {
    const mockBatches = [
      {
        id: 'b1',
        organisationId: 'org1',
        submittedByUserId: 'u1',
        status: 'Draft',
        currency: 'USD',
        totalAmount: 5000,
        itemCount: 3,
        createdAt: '2024-01-01',
        submittedAt: null,
        executedAt: null
      }
    ];
    mockGetPaymentBatches.mockResolvedValue(mockBatches);

    render(
      <BrowserRouter>
        <PaymentBatchPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/3 payments/i)).toBeInTheDocument();
    });
  });

  it('should display error on load failure', async () => {
    mockGetPaymentBatches.mockRejectedValue(new Error('Load failed'));

    render(
      <BrowserRouter>
        <PaymentBatchPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/load failed/i)).toBeInTheDocument();
    });
  });

  it('should show create batch form when button clicked', async () => {
    const user = userEvent.setup();
    mockGetPaymentBatches.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <PaymentBatchPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /create new batch/i })).toBeInTheDocument();
    });

    const createBtn = screen.getByRole('button', { name: /create new batch/i });
    await user.click(createBtn);

    expect(screen.getByText(/payee name/i)).toBeInTheDocument();
  });

  it('should show empty state when no batches', async () => {
    mockGetPaymentBatches.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <PaymentBatchPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/no payment batches yet/i)).toBeInTheDocument();
    });
  });
});

