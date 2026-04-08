
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import ApprovalsPage from './Approvals';
import * as corporate from '../services/corporate';
import * as auth from '../auth';

jest.mock('../services/corporate');
jest.mock('../auth');

const mockGetPendingApprovals = corporate.getPendingApprovals as jest.Mock;
const mockGetPaymentBatches = corporate.getPaymentBatches as jest.Mock;
const mockDecideBatch = corporate.decideBatch as jest.Mock;
const mockGetOrganisationRole = auth.getOrganisationRole as jest.Mock;

const mockBatches = [
  {
    id: 'b1',
    organisationId: 'org1',
    submittedByUserId: 'u1',
    status: 'PendingApproval',
    currency: 'USD',
    totalAmount: 1000,
    itemCount: 2,
    createdAt: '2024-01-01',
    submittedAt: '2024-01-02',
    executedAt: null
  }
];

describe('ApprovalsPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetOrganisationRole.mockReturnValue('Admin');
  });

  it('should render loading state', () => {
    mockGetPendingApprovals.mockImplementation(() => new Promise(() => {}));
    mockGetPaymentBatches.mockImplementation(() => new Promise(() => {}));

    render(
      <BrowserRouter>
        <ApprovalsPage />
      </BrowserRouter>
    );

    expect(screen.getByRole('heading', { name: /approvals/i })).toBeInTheDocument();
  });

  it('should load and display pending approvals', async () => {
    mockGetPendingApprovals.mockResolvedValue(mockBatches);
    mockGetPaymentBatches.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <ApprovalsPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/USD Payment Batch/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/2 payments/i)).toBeInTheDocument();
  });

  it('should display error message on load failure', async () => {
    mockGetPendingApprovals.mockRejectedValue(new Error('Network error'));
    mockGetPaymentBatches.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <ApprovalsPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/network error/i)).toBeInTheDocument();
    });
  });

  it('should show no pending approvals message when empty', async () => {
    mockGetPendingApprovals.mockResolvedValue([]);
    mockGetPaymentBatches.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <ApprovalsPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/no pending approvals/i)).toBeInTheDocument();
    });
  });

  it('should handle approve button click', async () => {
    const user = userEvent.setup();
    mockGetPendingApprovals.mockResolvedValue(mockBatches);
    mockGetPaymentBatches.mockResolvedValue([]);
    mockDecideBatch.mockResolvedValue({} as any);

    render(
      <BrowserRouter>
        <ApprovalsPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/USD Payment Batch/i)).toBeInTheDocument();
    });

    const approveBtn = screen.getByRole('button', { name: /approve/i });
    await user.click(approveBtn);

    await waitFor(() => {
      expect(corporate.decideBatch).toHaveBeenCalledWith('b1', 'Approved');
    });
  });

  it('should switch to history tab', async () => {
    const user = userEvent.setup();
    const decidedBatches = [{ ...mockBatches[0], status: 'Approved' }];
    mockGetPendingApprovals.mockResolvedValue([]);
    mockGetPaymentBatches.mockResolvedValue(decidedBatches);

    render(
      <BrowserRouter>
        <ApprovalsPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /history/i })).toBeInTheDocument();
    });

    const historyTab = screen.getByRole('tab', { name: /history/i });
    await user.click(historyTab);

    expect(historyTab).toHaveAttribute('aria-selected', 'true');
  });

  it('should show role warning for non-approvers', async () => {
    mockGetOrganisationRole.mockReturnValue('Viewer');
    mockGetPendingApprovals.mockResolvedValue([]);
    mockGetPaymentBatches.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <ApprovalsPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/need an Approver or Admin role/i)).toBeInTheDocument();
    });
  });
});

