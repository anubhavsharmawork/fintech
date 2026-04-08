import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import SanctionDetail from './SanctionDetail';
import * as sanctions from '../services/sanctions';

jest.mock('../services/sanctions');

const mockGetSanctionById = sanctions.getSanctionById as jest.Mock;
const mockGetSanctionAudit = sanctions.getSanctionAudit as jest.Mock;
const mockDisburseSanction = sanctions.disburseSanction as jest.Mock;
const mockRejectSanction = sanctions.rejectSanction as jest.Mock;
const mockCancelSanction = sanctions.cancelSanction as jest.Mock;

const mockSanction = {
  id: 's1',
  externalProjectId: 'p1',
  externalTenantId: 't1',
  userId: 'u1',
  accountId: 'a1',
  requestedAmount: 5000,
  currency: 'USD',
  purpose: 'Equipment purchase',
  riskScore: 25,
  kycStatus: 'verified',
  amlStatus: 'clear',
  status: 'Approved',
  approvedAmount: 5000,
  decisionReason: 'Low risk',
  ftkTransactionRef: 'tx123',
  idempotencyKey: 'idem1',
  createdAt: '2024-01-01',
  updatedAt: '2024-01-02',
  createdBy: 'user1'
};

const renderWithRoute = (id: string = 's1') =>
  render(
    <BrowserRouter initialEntries={[`/sanctions/${id}`]}>
      <Routes>
        <Route path="/sanctions/:id" element={<SanctionDetail />} />
      </Routes>
    </BrowserRouter>
  );

describe('SanctionDetail', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render loading state', () => {
    mockGetSanctionById.mockImplementation(() => new Promise(() => {}));
    mockGetSanctionAudit.mockImplementation(() => new Promise(() => {}));

    render(
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<SanctionDetail />} />
        </Routes>
      </BrowserRouter>
    );

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it('should load and display sanction details', async () => {
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/5000/)).toBeInTheDocument();
  });

  it('should display error message on load failure', async () => {
    mockGetSanctionById.mockRejectedValue(new Error('Not found'));
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute('invalid');

    await waitFor(() => {
      expect(screen.getByText(/not found/i)).toBeInTheDocument();
    });
  });

  it('should handle disburse action for Approved sanction', async () => {
    const user = userEvent.setup();
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);
    mockDisburseSanction.mockResolvedValue({ ...mockSanction, status: 'Disbursed' });

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    const disburseBtn = screen.queryByRole('button', { name: /disburse/i });
    if (disburseBtn) {
      await user.click(disburseBtn);
      await waitFor(() => {
        expect(sanctions.disburseSanction).toHaveBeenCalledWith('s1');
      });
    }
  });

  it('should show reject and cancel buttons for Submitted sanction', async () => {
    const submittedSanction = { ...mockSanction, status: 'Submitted', approvedAmount: null };
    mockGetSanctionById.mockResolvedValue(submittedSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    expect(screen.queryByRole('button', { name: /reject/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('should call rejectSanction with reason', async () => {
    const user = userEvent.setup();
    const submittedSanction = { ...mockSanction, status: 'Submitted', approvedAmount: null };
    mockGetSanctionById.mockResolvedValue(submittedSanction);
    mockGetSanctionAudit.mockResolvedValue([]);
    mockRejectSanction.mockResolvedValue({ ...submittedSanction, status: 'Rejected' });

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    const rejectBtn = screen.queryByRole('button', { name: /reject/i });
    if (rejectBtn) {
      await user.click(rejectBtn);
      const reasonInput = screen.queryByPlaceholderText(/reason/i);
      if (reasonInput) {
        await user.type(reasonInput, 'High risk');
        const confirmBtn = screen.queryByRole('button', { name: /confirm/i });
        if (confirmBtn) {
          await user.click(confirmBtn);
          await waitFor(() => {
            expect(sanctions.rejectSanction).toHaveBeenCalledWith('s1', 'High risk');
          });
        }
      }
    }
  });

  it('should display audit log when available', async () => {
    const mockAudit = [
      {
        id: 'a1',
        sanctionRequestId: 's1',
        fromStatus: 'Submitted',
        toStatus: 'Approved',
        changedBy: 'admin',
        reason: 'Approved after review',
        timestamp: '2024-01-02',
        correlationId: 'cor1'
      }
    ];
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue(mockAudit);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/audit/i)).toBeInTheDocument();
    });
  });

  it('should display all sanction details fields', async () => {
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/project id/i)).toBeInTheDocument();
      expect(screen.getByText(/tenant id/i)).toBeInTheDocument();
      expect(screen.getByText(/user id/i)).toBeInTheDocument();
      expect(screen.getByText(/account id/i)).toBeInTheDocument();
      expect(screen.getByText(/requested amount/i)).toBeInTheDocument();
      expect(screen.getByText(/approved amount/i)).toBeInTheDocument();
      expect(screen.getByText(/risk score/i)).toBeInTheDocument();
      expect(screen.getByText(/kyc/i)).toBeInTheDocument();
      expect(screen.getByText(/aml/i)).toBeInTheDocument();
      expect(screen.getByText(/purpose/i)).toBeInTheDocument();
    });
  });

  it('should navigate back to sanctions list', async () => {
    const user = userEvent.setup();
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    const backBtn = screen.getByRole('button', { name: /back to sanctions/i });
    await user.click(backBtn);
  });

  it('should show status badge with correct color', async () => {
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      const statusBadge = screen.getByText('Approved');
      expect(statusBadge).toBeInTheDocument();
    });
  });

  it('should handle cancel action', async () => {
    const user = userEvent.setup();
    const draftSanction = { ...mockSanction, status: 'Draft', approvedAmount: null };
    mockGetSanctionById.mockResolvedValue(draftSanction);
    mockGetSanctionAudit.mockResolvedValue([]);
    mockCancelSanction.mockResolvedValue({ ...draftSanction, status: 'Cancelled' });

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    const reasonInput = screen.getByPlaceholderText(/required for reject\/cancel/i);
    await user.type(reasonInput, 'No longer needed');

    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelBtn);

    await waitFor(() => {
      expect(sanctions.cancelSanction).toHaveBeenCalledWith('s1', 'No longer needed');
    });
  });

  it('should disable reject/cancel buttons when reason is empty', async () => {
    const submittedSanction = { ...mockSanction, status: 'Submitted', approvedAmount: null };
    mockGetSanctionById.mockResolvedValue(submittedSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    const rejectBtn = screen.getByRole('button', { name: /reject/i });
    expect(rejectBtn).toBeDisabled();
  });

  it('should show error message on disburse failure', async () => {
    const user = userEvent.setup();
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);
    mockDisburseSanction.mockRejectedValue(new Error('Disbursement failed'));

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    const disburseBtn = screen.getByRole('button', { name: /disburse/i });
    await user.click(disburseBtn);

    await waitFor(() => {
      expect(screen.getByText(/disbursement failed/i)).toBeInTheDocument();
    });
  });

  it('should show error message on reject failure', async () => {
    const user = userEvent.setup();
    const submittedSanction = { ...mockSanction, status: 'Submitted', approvedAmount: null };
    mockGetSanctionById.mockResolvedValue(submittedSanction);
    mockGetSanctionAudit.mockResolvedValue([]);
    mockRejectSanction.mockRejectedValue(new Error('Rejection failed'));

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    const reasonInput = screen.getByPlaceholderText(/required for reject\/cancel/i);
    await user.type(reasonInput, 'Test reason');

    const rejectBtn = screen.getByRole('button', { name: /reject/i });
    await user.click(rejectBtn);

    await waitFor(() => {
      expect(screen.getByText(/rejection failed/i)).toBeInTheDocument();
    });
  });

  it('should display no audit entries message when audit is empty', async () => {
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/no audit entries/i)).toBeInTheDocument();
    });
  });

  it('should display audit trail table with all columns', async () => {
    const mockAudit = [
      {
        id: 'a1',
        sanctionRequestId: 's1',
        fromStatus: 'Submitted',
        toStatus: 'Approved',
        changedBy: 'admin',
        reason: 'Approved after review',
        timestamp: '2024-01-02T10:00:00Z',
        correlationId: 'cor-123'
      }
    ];
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue(mockAudit);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/submitted/i)).toBeInTheDocument();
      expect(screen.getByText(/admin/i)).toBeInTheDocument();
      expect(screen.getByText(/approved after review/i)).toBeInTheDocument();
      expect(screen.getByText(/cor-123/i)).toBeInTheDocument();
    });
  });

  it('should handle screening status actions', async () => {
    const screeningSanction = { ...mockSanction, status: 'Screening', approvedAmount: null };
    mockGetSanctionById.mockResolvedValue(screeningSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    // Screening status should have reject button
    expect(screen.queryByRole('button', { name: /reject/i })).toBeInTheDocument();
    // But not cancel button
    expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
  });

  it('should handle underwriting status actions', async () => {
    const underwritingSanction = { ...mockSanction, status: 'Underwriting', approvedAmount: null };
    mockGetSanctionById.mockResolvedValue(underwritingSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    // Underwriting status should have reject button
    expect(screen.queryByRole('button', { name: /reject/i })).toBeInTheDocument();
  });

  it('should not show action buttons for Disbursed status', async () => {
    const disbursedSanction = { ...mockSanction, status: 'Disbursed', approvedAmount: 5000, ftkTransactionRef: 'tx456' };
    mockGetSanctionById.mockResolvedValue(disbursedSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    expect(screen.queryByRole('button', { name: /disburse/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /reject/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
  });

  it('should display FTK transaction reference when available', async () => {
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/ftk tx ref/i)).toBeInTheDocument();
      expect(screen.getByText(/tx123/)).toBeInTheDocument();
    });
  });

  it('should display idempotency key', async () => {
    mockGetSanctionById.mockResolvedValue(mockSanction);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/idempotency key/i)).toBeInTheDocument();
      expect(screen.getByText(/idem1/)).toBeInTheDocument();
    });
  });

  it('should show not found message when sanction is null', async () => {
    mockGetSanctionById.mockResolvedValue(null);
    mockGetSanctionAudit.mockResolvedValue([]);

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/not found/i)).toBeInTheDocument();
    });
  });

  it('should clear reason input after successful reject', async () => {
    const user = userEvent.setup();
    const submittedSanction = { ...mockSanction, status: 'Submitted', approvedAmount: null };
    mockGetSanctionById.mockResolvedValueOnce(submittedSanction);
    mockGetSanctionAudit.mockResolvedValue([]);
    mockRejectSanction.mockResolvedValue({ ...submittedSanction, status: 'Rejected' });
    mockGetSanctionById.mockResolvedValue({ ...submittedSanction, status: 'Rejected' });

    renderWithRoute();

    await waitFor(() => {
      expect(screen.getByText(/equipment purchase/i)).toBeInTheDocument();
    });

    const reasonInput = screen.getByPlaceholderText(/required for reject\/cancel/i);
    await user.type(reasonInput, 'Test reason');

    const rejectBtn = screen.getByRole('button', { name: /reject/i });
    await user.click(rejectBtn);

    await waitFor(() => {
      expect(sanctions.rejectSanction).toHaveBeenCalled();
    });
  });
});
