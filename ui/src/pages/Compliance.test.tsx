
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import Compliance from './Compliance';
import * as complianceService from '../services/complianceService';
import * as auth from '../auth';

jest.mock('../services/complianceService');
jest.mock('../auth');

const mockGetKycStatus = complianceService.getKycStatus as jest.Mock;
const mockGetSarReports = complianceService.getSarReports as jest.Mock;
const mockIsCorporateUser = auth.isCorporateUser as jest.Mock;
const mockGetOrganisationId = auth.getOrganisationId as jest.Mock;

describe('Compliance', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockIsCorporateUser.mockReturnValue(false);
    mockGetOrganisationId.mockReturnValue(null);
  });

  it('should render loading state', () => {
    mockGetKycStatus.mockImplementation(() => new Promise(() => {}));
    mockGetSarReports.mockImplementation(() => new Promise(() => {}));

    render(
      <BrowserRouter>
        <Compliance />
      </BrowserRouter>
    );

    expect(screen.getByRole('heading', { name: /compliance/i })).toBeInTheDocument();
  });

  it('should load and display KYC status', async () => {
    mockGetKycStatus.mockResolvedValue({ userId: 'u1', status: 'Verified' });
    mockGetSarReports.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <Compliance />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/verified/i)).toBeInTheDocument();
    });
  });

  it('should display error on load failure', async () => {
    mockGetKycStatus.mockRejectedValue(new Error('Failed to load KYC'));
    mockGetSarReports.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <Compliance />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/failed to load/i)).toBeInTheDocument();
    });
  });

  it('should load and display SAR reports', async () => {
    mockGetKycStatus.mockResolvedValue({ userId: 'u1', status: 'Verified' });
    mockGetSarReports.mockResolvedValue([
      {
        id: 's1',
        transactionId: 't1',
        userId: 'u1',
        amount: 10000,
        currency: 'USD',
        reason: 'Large transaction',
        riskLevel: 'high',
        flaggedAt: '2024-01-01',
        status: 'open'
      }
    ]);

    render(
      <BrowserRouter>
        <Compliance />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/verified/i)).toBeInTheDocument();
    });

    const sarTab = screen.getByRole('tab', { name: /sar reports/i });
    await userEvent.setup().click(sarTab);

    await waitFor(() => {
      expect(screen.getByText(/large transaction/i)).toBeInTheDocument();
    });
  });

  it('should switch between KYC and SAR tabs', async () => {
    const user = userEvent.setup();
    mockGetKycStatus.mockResolvedValue({ userId: 'u1', status: 'Verified' });
    mockGetSarReports.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <Compliance />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /kyc status/i })).toBeInTheDocument();
    });

    const kycTab = screen.getByRole('tab', { name: /kyc status/i });
    const sarTab = screen.getByRole('tab', { name: /sar reports/i });

    expect(kycTab).toHaveAttribute('aria-selected', 'true');

    await user.click(sarTab);
    expect(sarTab).toHaveAttribute('aria-selected', 'true');

    await user.click(kycTab);
    expect(kycTab).toHaveAttribute('aria-selected', 'true');
  });

  it('should display empty state for SAR reports', async () => {
    mockGetKycStatus.mockResolvedValue({ userId: 'u1', status: 'Verified' });
    mockGetSarReports.mockResolvedValue([]);

    render(
      <BrowserRouter>
        <Compliance />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /sar reports/i })).toBeInTheDocument();
    });

    const sarTab = screen.getByRole('tab', { name: /sar reports/i });
    await userEvent.setup().click(sarTab);

    await waitFor(() => {
      expect(screen.getByText(/no sar reports/i)).toBeInTheDocument();
    });
  });
});

