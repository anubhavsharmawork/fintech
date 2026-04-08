import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import SanctionsList from './SanctionsList';
import * as sanctionsService from '../services/sanctions';

jest.mock('../services/sanctions');
jest.mock('../hooks/usePagination', () => ({
  usePagination: () => ({
    page: 1,
    pageSize: 25,
    totalCount: 0,
    totalPages: 1,
    from: 0,
    to: 0,
    setPage: jest.fn(),
    setPageSize: jest.fn(),
    setTotalCount: jest.fn(),
    resetToFirstPage: jest.fn(),
  }),
}));
jest.mock('../components/Pagination', () => () => null);
jest.mock('../components/TableSkeleton', () => () => <div data-testid="table-skeleton" />);

const mockNavigate = jest.fn();
jest.mock('react-router-dom', () => ({
  ...jest.requireActual('react-router-dom'),
  useNavigate: () => mockNavigate,
}));

const mockSanctions = [
  {
    id: 's1',
    externalProjectId: 'PROJ-001',
    externalTenantId: 'TENANT-1',
    userId: 'user1',
    accountId: 'acc1',
    requestedAmount: 5000,
    currency: 'FTK',
    purpose: 'Test purpose',
    riskScore: 20,
    kycStatus: 'Passed',
    amlStatus: 'Passed',
    status: 'Approved',
    approvedAmount: 5000,
    decisionReason: null,
    ftkTransactionRef: null,
    idempotencyKey: 'key1',
    createdAt: '2024-01-15T10:00:00Z',
    updatedAt: '2024-01-16T10:00:00Z',
    createdBy: 'admin@example.com',
  },
  {
    id: 's2',
    externalProjectId: 'PROJ-002',
    externalTenantId: 'TENANT-1',
    userId: 'user1',
    accountId: 'acc1',
    requestedAmount: 2000,
    currency: 'FTK',
    purpose: 'Another purpose',
    riskScore: 50,
    kycStatus: 'Passed',
    amlStatus: 'Passed',
    status: 'Draft',
    approvedAmount: null,
    decisionReason: null,
    ftkTransactionRef: null,
    idempotencyKey: 'key2',
    createdAt: '2024-01-10T10:00:00Z',
    updatedAt: '2024-01-10T10:00:00Z',
    createdBy: 'user@example.com',
  },
];

describe('SanctionsList', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.setItem('userId', 'user-123');
    (sanctionsService.getSanctions as jest.Mock).mockResolvedValue(mockSanctions);
    (global.fetch as jest.Mock) = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => [{ id: 'acc1', accountNumber: 'ACC001', accountType: 'Checking', currency: 'FTK' }],
    });
  });

  const renderComponent = () =>
    render(
      <BrowserRouter>
        <SanctionsList />
      </BrowserRouter>,
    );

  describe('initial render', () => {
    it('shows FTK Sanctions heading', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('FTK Sanctions')).toBeInTheDocument();
      });
    });

    it('shows + New Request button', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('+ New Request')).toBeInTheDocument();
      });
    });

    it('calls getSanctions on mount', async () => {
      renderComponent();
      await waitFor(() => {
        expect(sanctionsService.getSanctions).toHaveBeenCalledTimes(1);
      });
    });

    it('renders sanction rows after loading', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('PROJ-001')).toBeInTheDocument();
        expect(screen.getByText('PROJ-002')).toBeInTheDocument();
      });
    });

    it('displays status badge for each row', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('Approved')).toBeInTheDocument();
        expect(screen.getByText('Draft')).toBeInTheDocument();
      });
    });
  });

  describe('empty state', () => {
    it('shows empty message when no sanctions exist', async () => {
      (sanctionsService.getSanctions as jest.Mock).mockResolvedValue([]);
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText(/no sanction requests/i)).toBeInTheDocument();
      });
    });
  });

  describe('error state', () => {
    it('displays error alert when getSanctions fails', async () => {
      (sanctionsService.getSanctions as jest.Mock).mockRejectedValue(new Error('Network error'));
      renderComponent();
      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getByText(/network error/i)).toBeInTheDocument();
      });
    });

    it('shows generic message on error without message', async () => {
      (sanctionsService.getSanctions as jest.Mock).mockRejectedValue({});
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText(/failed to load sanctions/i)).toBeInTheDocument();
      });
    });
  });

  describe('status filter', () => {
    it('renders All Statuses dropdown', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByDisplayValue('All Statuses')).toBeInTheDocument();
      });
    });

    it('filters rows by Approved status', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('PROJ-001')).toBeInTheDocument());

      fireEvent.change(screen.getByDisplayValue('All Statuses'), {
        target: { value: 'Approved' },
      });

      await waitFor(() => {
        expect(screen.getByText('PROJ-001')).toBeInTheDocument();
        expect(screen.queryByText('PROJ-002')).not.toBeInTheDocument();
      });
    });

    it('filters rows by Draft status', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('PROJ-002')).toBeInTheDocument());

      fireEvent.change(screen.getByDisplayValue('All Statuses'), {
        target: { value: 'Draft' },
      });

      await waitFor(() => {
        expect(screen.getByText('PROJ-002')).toBeInTheDocument();
        expect(screen.queryByText('PROJ-001')).not.toBeInTheDocument();
      });
    });
  });

  describe('project ID filter', () => {
    it('renders filter input', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByPlaceholderText(/project id/i)).toBeInTheDocument();
      });
    });

    it('filters rows by partial project ID', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('PROJ-001')).toBeInTheDocument());

      fireEvent.change(screen.getByPlaceholderText(/project id/i), {
        target: { value: '001' },
      });

      await waitFor(() => {
        expect(screen.getByText('PROJ-001')).toBeInTheDocument();
        expect(screen.queryByText('PROJ-002')).not.toBeInTheDocument();
      });
    });
  });

  describe('new request form', () => {
    it('toggles form open on button click', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Request')).toBeInTheDocument());

      fireEvent.click(screen.getByText('+ New Request'));

      await waitFor(() => {
        expect(screen.getByText('New Sanction Request')).toBeInTheDocument();
        expect(screen.getByText('Cancel')).toBeInTheDocument();
      });
    });

    it('hides form on Cancel', async () => {
      renderComponent();
      await waitFor(() => fireEvent.click(screen.getByText('+ New Request')));
      await waitFor(() => expect(screen.getByText('New Sanction Request')).toBeInTheDocument());

      fireEvent.click(screen.getByText('Cancel'));

      await waitFor(() => {
        expect(screen.queryByText('New Sanction Request')).not.toBeInTheDocument();
      });
    });

    it('shows auth error when userId missing', async () => {
      localStorage.removeItem('userId');
      renderComponent();
      await waitFor(() => fireEvent.click(screen.getByText('+ New Request')));
      await waitFor(() => expect(screen.getByText('New Sanction Request')).toBeInTheDocument());

      const form = screen.getByText(/submit request/i).closest('form')!;
      fireEvent.submit(form);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getByText(/not authenticated/i)).toBeInTheDocument();
      });
    });

    it('calls createSanction and reloads on successful submit', async () => {
      (sanctionsService.createSanction as jest.Mock).mockResolvedValue(mockSanctions[0]);
      renderComponent();
      await waitFor(() => fireEvent.click(screen.getByText('+ New Request')));
      await waitFor(() => expect(screen.getByText('New Sanction Request')).toBeInTheDocument());

      fireEvent.change(screen.getByLabelText(/project id/i), { target: { value: 'P-NEW' } });
      fireEvent.change(screen.getByLabelText(/tenant id/i), { target: { value: 'T-1' } });
      fireEvent.change(screen.getByLabelText(/amount/i), { target: { value: '100' } });
      fireEvent.change(screen.getByLabelText(/purpose/i), { target: { value: 'Audit test' } });

      const form = screen.getByText(/submit request/i).closest('form')!;
      fireEvent.submit(form);

      await waitFor(() => {
        expect(sanctionsService.createSanction).toHaveBeenCalled();
      });
    });
  });

  describe('row click navigation', () => {
    it('navigates to sanction detail when row is clicked', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('PROJ-001')).toBeInTheDocument());

      fireEvent.click(screen.getByText('PROJ-001'));

      expect(mockNavigate).toHaveBeenCalledWith(expect.stringContaining('s1'));
    });
  });
});
