import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import RequestCredit from './RequestCredit';
import * as auth from '../auth';
import { ToastProvider } from '../components/Toast';

jest.mock('../auth');

describe('RequestCredit Page', () => {
  const mockAuthFetch = auth.authFetch as jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
  });

  const renderPage = (search = '') =>
    render(
      <MemoryRouter initialEntries={[`/request-credit${search}`]}>
        <Routes>
          <Route
            path="/request-credit"
            element={
              <ToastProvider>
                <RequestCredit />
              </ToastProvider>
            }
          />
        </Routes>
      </MemoryRouter>
    );

  describe('No query params', () => {
    it('renders without crashing', () => {
      renderPage();
      expect(screen.getByRole('heading', { name: /request credit/i })).toBeInTheDocument();
    });

    it('shows the "no project linked" warning banner', () => {
      renderPage();
      expect(
        screen.getByText(/no project linked — submitting a general credit request/i)
      ).toBeInTheDocument();
    });

    it('does NOT show the project context banner', () => {
      renderPage();
      expect(screen.queryByText(/credit request for project/i)).not.toBeInTheDocument();
    });
  });

  describe('With project_id and project_name params', () => {
    it('shows the project context banner with the project name', () => {
      renderPage('?project_id=abc&project_name=MyProject');
      expect(screen.getByText(/credit request for project/i)).toBeInTheDocument();
      expect(screen.getByText('MyProject')).toBeInTheDocument();
    });

    it('falls back to project_id when project_name is absent', () => {
      renderPage('?project_id=abc123');
      expect(screen.getByText(/credit request for project/i)).toBeInTheDocument();
      expect(screen.getByText('abc123')).toBeInTheDocument();
    });

    it('does NOT show the "no project linked" warning', () => {
      renderPage('?project_id=abc&project_name=MyProject');
      expect(screen.queryByText(/no project linked/i)).not.toBeInTheDocument();
    });
  });

  describe('Form fields', () => {
    it('renders amount, currency, purpose and notes fields', () => {
      renderPage();
      expect(screen.getByLabelText(/requested amount/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/currency/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/purpose/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/notes/i)).toBeInTheDocument();
    });

    it('currency select defaults to NZD', () => {
      renderPage();
      const select = screen.getByLabelText(/currency/i) as HTMLSelectElement;
      expect(select.value).toBe('NZD');
    });
  });

  describe('Submit button state', () => {
    it('submit button is enabled initially', () => {
      renderPage();
      expect(screen.getByRole('button', { name: /submit request/i })).not.toBeDisabled();
    });

    it('submit button is disabled while busySubmit is true', async () => {
      // Keep the request pending so busySubmit stays true
      let resolveFetch!: (value: Response) => void;
      mockAuthFetch.mockReturnValue(new Promise<Response>(res => { resolveFetch = res; }));

      renderPage();

      fireEvent.change(screen.getByLabelText(/requested amount/i), { target: { value: '1000' } });
      fireEvent.change(screen.getByLabelText(/purpose/i), { target: { value: 'Equipment' } });
      fireEvent.click(screen.getByRole('button', { name: /submit request/i }));

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /submitting/i })).toBeDisabled();
      });

      // Resolve to avoid act() warnings
      resolveFetch({ ok: true, json: async () => ({}) } as Response);
    });
  });

  describe('Successful submission', () => {
    it('shows the success panel after a mocked successful POST', async () => {
      mockAuthFetch.mockResolvedValue({
        ok: true,
        json: async () => ({}),
      });

      renderPage('?project_id=proj1&project_name=TestProject');

      fireEvent.change(screen.getByLabelText(/requested amount/i), { target: { value: '2500' } });
      fireEvent.change(screen.getByLabelText(/purpose/i), { target: { value: 'Server hardware' } });
      fireEvent.click(screen.getByRole('button', { name: /submit request/i }));

      await waitFor(() => {
        expect(screen.getByText(/your credit request has been submitted/i)).toBeInTheDocument();
      });
    });

    it('shows a "Submit another request" button in the success panel', async () => {
      mockAuthFetch.mockResolvedValue({
        ok: true,
        json: async () => ({}),
      });

      renderPage();

      fireEvent.change(screen.getByLabelText(/requested amount/i), { target: { value: '100' } });
      fireEvent.change(screen.getByLabelText(/purpose/i), { target: { value: 'Office supplies' } });
      fireEvent.click(screen.getByRole('button', { name: /submit request/i }));

      await waitFor(() => {
        expect(
          screen.getByRole('button', { name: /submit another request/i })
        ).toBeInTheDocument();
      });
    });

    it('"Submit another request" resets back to the form', async () => {
      mockAuthFetch.mockResolvedValue({
        ok: true,
        json: async () => ({}),
      });

      renderPage();

      fireEvent.change(screen.getByLabelText(/requested amount/i), { target: { value: '100' } });
      fireEvent.change(screen.getByLabelText(/purpose/i), { target: { value: 'Office supplies' } });
      fireEvent.click(screen.getByRole('button', { name: /submit request/i }));

      await waitFor(() =>
        screen.getByRole('button', { name: /submit another request/i })
      );

      fireEvent.click(screen.getByRole('button', { name: /submit another request/i }));

      expect(screen.getByRole('button', { name: /submit request/i })).toBeInTheDocument();
    });
  });

  describe('POST payload', () => {
    it('includes projectId and projectName when present', async () => {
      mockAuthFetch.mockResolvedValue({ ok: true, json: async () => ({}) });

      renderPage('?project_id=proj-42&project_name=Alpha');

      fireEvent.change(screen.getByLabelText(/requested amount/i), { target: { value: '500' } });
      fireEvent.change(screen.getByLabelText(/purpose/i), { target: { value: 'Tools' } });
      fireEvent.click(screen.getByRole('button', { name: /submit request/i }));

      await waitFor(() => expect(mockAuthFetch).toHaveBeenCalledTimes(1));

      const [url, options] = mockAuthFetch.mock.calls[0];
      expect(url).toBe('/credit-requests');
      expect(options.method).toBe('POST');
      const body = JSON.parse(options.body);
      expect(body.projectId).toBe('proj-42');
      expect(body.projectName).toBe('Alpha');
      expect(body.amount).toBe(500);
      expect(body.currency).toBe('NZD');
      expect(body.purpose).toBe('Tools');
    });

    it('sends null for projectId and projectName when no params', async () => {
      mockAuthFetch.mockResolvedValue({ ok: true, json: async () => ({}) });

      renderPage();

      fireEvent.change(screen.getByLabelText(/requested amount/i), { target: { value: '300' } });
      fireEvent.change(screen.getByLabelText(/purpose/i), { target: { value: 'Maintenance' } });
      fireEvent.click(screen.getByRole('button', { name: /submit request/i }));

      await waitFor(() => expect(mockAuthFetch).toHaveBeenCalledTimes(1));

      const body = JSON.parse(mockAuthFetch.mock.calls[0][1].body);
      expect(body.projectId).toBeNull();
      expect(body.projectName).toBeNull();
    });
  });
});
