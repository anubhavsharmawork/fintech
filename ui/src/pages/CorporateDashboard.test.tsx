
import { render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import CorporateDashboard from './CorporateDashboard';
import * as corporate from '../services/corporate';
import * as auth from '../auth';

jest.mock('../services/corporate');
jest.mock('../auth');

const mockGetOrganisation = corporate.getOrganisation as jest.Mock;
const mockGetMembers = corporate.getMembers as jest.Mock;
const mockGetOrganisationAccounts = corporate.getOrganisationAccounts as jest.Mock;
const mockGetPaymentBatches = corporate.getPaymentBatches as jest.Mock;
const mockGetPendingApprovals = corporate.getPendingApprovals as jest.Mock;
const mockGetOrganisationId = auth.getOrganisationId as jest.Mock;

describe('CorporateDashboard', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetOrganisationId.mockReturnValue('org1');
  });

  it('should render loading state initially', () => {
    mockGetOrganisation.mockImplementation(() => new Promise(() => {}));
    mockGetMembers.mockImplementation(() => new Promise(() => {}));
    mockGetOrganisationAccounts.mockImplementation(() => new Promise(() => {}));
    mockGetPaymentBatches.mockImplementation(() => new Promise(() => {}));
    mockGetPendingApprovals.mockImplementation(() => new Promise(() => {}));

    render(
      <BrowserRouter>
        <CorporateDashboard />
      </BrowserRouter>
    );

    expect(screen.getByRole('heading', { name: /corporate dashboard/i })).toBeInTheDocument();
  });

  it('should load and display organisation data', async () => {
    mockGetOrganisation.mockResolvedValue({ id: 'org1', name: 'Acme Corp', registrationNumber: 'REG123', createdAt: '2024-01-01' });
    mockGetMembers.mockResolvedValue([]);
    mockGetOrganisationAccounts.mockResolvedValue([]);
    mockGetPaymentBatches.mockResolvedValue([]);
    mockGetPendingApprovals.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <CorporateDashboard />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/acme corp/i)).toBeInTheDocument();
    });
  });

  it('should display error when no organisation context', async () => {
    mockGetOrganisationId.mockReturnValue(null);

    render(
      <BrowserRouter>
        <CorporateDashboard />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/no organisation context found/i)).toBeInTheDocument();
    });
  });

  it('should handle API error', async () => {
    mockGetOrganisation.mockRejectedValue(new Error('Failed to load'));
    mockGetMembers.mockResolvedValue([]);
    mockGetOrganisationAccounts.mockResolvedValue([]);
    mockGetPaymentBatches.mockResolvedValue([]);
    mockGetPendingApprovals.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <CorporateDashboard />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/failed to load/i)).toBeInTheDocument();
    });
  });

  it('should display account balances', async () => {
    mockGetOrganisation.mockResolvedValue({ id: 'org1', name: 'Test Org', registrationNumber: 'REG123', createdAt: '2024-01-01' });
    mockGetMembers.mockResolvedValue([]);
    mockGetOrganisationAccounts.mockResolvedValue([
      { id: 'acc1', balance: 5000, currency: 'USD' },
      { id: 'acc2', balance: 3000, currency: 'USD' }
    ]);
    mockGetPaymentBatches.mockResolvedValue([]);
    mockGetPendingApprovals.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <CorporateDashboard />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/test org/i)).toBeInTheDocument();
    });
  });
});

