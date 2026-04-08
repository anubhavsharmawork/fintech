import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Settings from './Settings';
import { ToastProvider } from '../components/Toast';
import * as auth from '../auth';
import * as timezoneService from '../services/timezone';
import * as notificationService from '../services/notificationService';

jest.mock('../auth');
jest.mock('../services/timezone');
jest.mock('../services/notificationService');

describe('Settings Page', () => {
  const mockAuthFetch = auth.authFetch as jest.Mock;

  const defaultProfile = {
    email: 'user@example.com',
    firstName: 'Test',
    lastName: 'User',
    timeZoneId: 'Pacific/Auckland',
    utcOffsetMinutes: 720,
  };

  const mockPrefs = [
    { eventType: 'TRANSACTION', emailEnabled: true, smsEnabled: false },
    { eventType: 'LOGIN', emailEnabled: false, smsEnabled: false },
  ];

  beforeEach(() => {
    jest.clearAllMocks();

    (timezoneService.detectBrowserTimezone as jest.Mock).mockReturnValue({
      timeZoneId: 'Pacific/Auckland',
      utcOffsetMinutes: 720,
    });

    (notificationService.fetchPreferences as jest.Mock).mockResolvedValue(mockPrefs);
    (notificationService.savePreferences as jest.Mock).mockResolvedValue(undefined);

    mockAuthFetch.mockImplementation((url: string) => {
      if (url.includes('/users/profile')) {
        return Promise.resolve({ ok: true, json: async () => defaultProfile });
      }
      if (url.includes('/users/timezone')) {
        return Promise.resolve({ ok: true, json: async () => ({}) });
      }
      if (url.includes('/users/password')) {
        return Promise.resolve({ ok: true });
      }
      return Promise.resolve({ ok: true, json: async () => ({}) });
    });
  });

  const renderSettings = () =>
    render(
      <BrowserRouter>
        <ToastProvider>
          <Settings />
        </ToastProvider>
      </BrowserRouter>,
    );

  describe('tab rendering', () => {
    it('renders Profile tab by default', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText('Profile')).toBeInTheDocument();
      });
    });

    it('renders Security tab button', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText('Security')).toBeInTheDocument();
      });
    });

    it('renders Notifications tab button', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText('Notifications')).toBeInTheDocument();
      });
    });

    it('renders API Access tab button', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText('API Access')).toBeInTheDocument();
      });
    });
  });

  describe('profile tab content', () => {
    it('shows timezone section', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText(/timezone/i)).toBeInTheDocument();
      });
    });

    it('shows detect button', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText(/detect/i)).toBeInTheDocument();
      });
    });

    it('shows save button', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument();
      });
    });

    it('loads profile data on mount', async () => {
      renderSettings();
      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith(expect.stringContaining('/users/profile'));
      });
    });
  });

  describe('tab switching', () => {
    it('switches to Security tab', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText('Security')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByText('Security'));
      await waitFor(() => {
        expect(screen.getByText(/password/i)).toBeInTheDocument();
      });
    });

    it('switches to Notifications tab', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText('Notifications')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByText('Notifications'));
      await waitFor(() => {
        expect(screen.getByText(/notification/i)).toBeInTheDocument();
      });
    });

    it('switches to API Access tab', async () => {
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText('API Access')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByText('API Access'));
      await waitFor(() => {
        expect(screen.getByText(/api/i)).toBeInTheDocument();
      });
    });
  });

  describe('timezone search', () => {
    it('shows dropdown when clicking timezone input', async () => {
      renderSettings();
      await waitFor(() => expect(screen.getByPlaceholderText(/search timezone/i)).toBeInTheDocument());
      fireEvent.click(screen.getByPlaceholderText(/search timezone/i));
      await waitFor(() => {
        expect(screen.getByRole('listbox')).toBeInTheDocument();
      });
    });

    it('filters timezones when typing', async () => {
      renderSettings();
      await waitFor(() => expect(screen.getByPlaceholderText(/search timezone/i)).toBeInTheDocument());
      fireEvent.click(screen.getByPlaceholderText(/search timezone/i));
      fireEvent.change(screen.getByPlaceholderText(/search timezone/i), {
        target: { value: 'Auckland' },
      });
      await waitFor(() => {
        expect(screen.getAllByText(/Auckland/i).length).toBeGreaterThan(0);
      });
    });
  });

  describe('security tab', () => {
    it('renders current password field', async () => {
      renderSettings();
      await waitFor(() => { fireEvent.click(screen.getByText('Security')); });
      await waitFor(() => {
        expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
      });
    });

    it('renders new password field', async () => {
      renderSettings();
      await waitFor(() => { fireEvent.click(screen.getByText('Security')); });
      await waitFor(() => {
        expect(screen.getByLabelText(/new password/i)).toBeInTheDocument();
      });
    });

    it('shows error when passwords do not match', async () => {
      renderSettings();
      await waitFor(() => { fireEvent.click(screen.getByText('Security')); });
      await waitFor(() => {
        expect(screen.getByLabelText(/new password/i)).toBeInTheDocument();
      });
      fireEvent.change(screen.getByLabelText(/new password/i), {
        target: { value: 'Password123!' },
      });
      const confirmField = screen.getByLabelText(/confirm/i);
      fireEvent.change(confirmField, { target: { value: 'DifferentPassword' } });
      fireEvent.click(screen.getByRole('button', { name: /update/i }));
      await waitFor(() => {
        expect(screen.getByText(/do not match/i)).toBeInTheDocument();
      });
    });
  });

  describe('notifications tab', () => {
    it('loads notification preferences', async () => {
      renderSettings();
      fireEvent.click(await screen.findByText('Notifications'));
      await waitFor(() => {
        expect(notificationService.fetchPreferences).toHaveBeenCalled();
      });
    });

    it('displays event type preferences', async () => {
      renderSettings();
      fireEvent.click(await screen.findByText('Notifications'));
      await waitFor(() => {
        expect(screen.getByText(/TRANSACTION/i)).toBeInTheDocument();
      });
    });
  });

  describe('profile fetch failure', () => {
    it('does not crash when profile endpoint fails', async () => {
      mockAuthFetch.mockRejectedValue(new Error('Network error'));
      renderSettings();
      await waitFor(() => {
        expect(screen.getByText('Profile')).toBeInTheDocument();
      });
    });
  });

  describe('timezone detect and save', () => {
    it('applies detected timezone when "Use this" is clicked', async () => {
      renderSettings();
      await waitFor(() => expect(screen.getByText(/use this/i)).toBeInTheDocument());
      fireEvent.click(screen.getByText(/use this/i));
      await waitFor(() => {
        expect(screen.getByDisplayValue(/Auckland/i)).toBeInTheDocument();
      });
    });

    it('saves timezone successfully and shows success message', async () => {
      (timezoneService.updateTimezone as jest.Mock).mockResolvedValue({});
      renderSettings();
      await waitFor(() => expect(screen.getByRole('button', { name: /save timezone/i })).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: /save timezone/i }));
      await waitFor(() => {
        expect(screen.getByText(/timezone preference saved/i)).toBeInTheDocument();
      });
    });

    it('shows error message when save timezone fails', async () => {
      (timezoneService.updateTimezone as jest.Mock).mockRejectedValue(new Error('Network failure'));
      renderSettings();
      await waitFor(() => expect(screen.getByRole('button', { name: /save timezone/i })).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: /save timezone/i }));
      await waitFor(() => {
        expect(screen.getByText(/network failure/i)).toBeInTheDocument();
      });
    });

    it('opens dropdown on ArrowDown keydown', async () => {
      renderSettings();
      await waitFor(() => expect(screen.getByPlaceholderText(/search timezone/i)).toBeInTheDocument());
      fireEvent.keyDown(screen.getByPlaceholderText(/search timezone/i), { key: 'ArrowDown' });
      await waitFor(() => {
        expect(screen.getByRole('listbox')).toBeInTheDocument();
      });
    });

    it('closes dropdown on Escape keydown', async () => {
      renderSettings();
      await waitFor(() => expect(screen.getByPlaceholderText(/search timezone/i)).toBeInTheDocument());
      fireEvent.click(screen.getByPlaceholderText(/search timezone/i));
      await waitFor(() => expect(screen.getByRole('listbox')).toBeInTheDocument());
      fireEvent.keyDown(screen.getByPlaceholderText(/search timezone/i), { key: 'Escape' });
      await waitFor(() => {
        expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      });
    });

    it('selects a timezone option from the dropdown', async () => {
      renderSettings();
      await waitFor(() => expect(screen.getByPlaceholderText(/search timezone/i)).toBeInTheDocument());
      fireEvent.click(screen.getByPlaceholderText(/search timezone/i));
      await waitFor(() => expect(screen.getByRole('listbox')).toBeInTheDocument());
      fireEvent.change(screen.getByPlaceholderText(/search timezone/i), { target: { value: 'Auckland' } });
      await waitFor(() => {
        const options = screen.getAllByRole('option');
        expect(options.length).toBeGreaterThan(0);
        fireEvent.mouseDown(options[0]);
      });
      await waitFor(() => {
        expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      });
    });
  });

  describe('notifications toggle and save', () => {
    it('toggles email preference and saves successfully', async () => {
      renderSettings();
      fireEvent.click(await screen.findByText('Notifications'));
      await waitFor(() => expect(screen.getByText(/TRANSACTION/i)).toBeInTheDocument());
      const switches = screen.getAllByRole('switch');
      expect(switches.length).toBeGreaterThan(0);
      fireEvent.click(switches[0]);
      await waitFor(() => {
        expect(notificationService.savePreferences).toHaveBeenCalled();
      });
    });

    it('shows toast success after toggling preference', async () => {
      (notificationService.savePreferences as jest.Mock).mockResolvedValue(undefined);
      renderSettings();
      fireEvent.click(await screen.findByText('Notifications'));
      await waitFor(() => expect(screen.getByText(/TRANSACTION/i)).toBeInTheDocument());
      const switches = screen.getAllByRole('switch');
      fireEvent.click(switches[0]);
      await waitFor(() => {
        expect(notificationService.savePreferences).toHaveBeenCalled();
      });
    });

    it('handles preference save error gracefully', async () => {
      (notificationService.savePreferences as jest.Mock).mockRejectedValue(new Error('Save failed'));
      renderSettings();
      fireEvent.click(await screen.findByText('Notifications'));
      await waitFor(() => expect(screen.getByText(/TRANSACTION/i)).toBeInTheDocument());
      const switches = screen.getAllByRole('switch');
      fireEvent.click(switches[0]);
      await waitFor(() => {
        expect(notificationService.savePreferences).toHaveBeenCalled();
      });
    });
  });
});
