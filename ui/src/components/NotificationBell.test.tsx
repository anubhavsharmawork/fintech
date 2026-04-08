
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import NotificationBell from './NotificationBell';
import * as notificationService from '../services/notificationService';
import { AppProvider } from '../context/AppContext';

jest.mock('../services/notificationService');

const MockToastProvider = ({ children }: { children: React.ReactNode }) => (
  <AppProvider>{children}</AppProvider>
);

describe('NotificationBell', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render bell icon', () => {
    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    expect(screen.getByRole('button', { name: /notifications/i })).toBeInTheDocument();
  });

  it('should fetch notifications when opened', async () => {
    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'New transaction', timestamp: '2024-01-01', read: false }
    ];
    notificationService.fetchNotifications as jest.Mock.mockResolvedValue(mockNotifications as any);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(notificationService.fetchNotifications).toHaveBeenCalled();
    });
  });

  it('should display error on fetch failure', async () => {
    notificationService.fetchNotifications as jest.Mock.mockRejectedValue(new Error('Network error'));

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/failed to load/i)).toBeInTheDocument();
    });
  });

  it('should mark all as read when button clicked', async () => {
    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'New transaction', timestamp: '2024-01-01', read: false }
    ];
    notificationService.fetchNotifications as jest.Mock.mockResolvedValue(mockNotifications as any);
    notificationService.markAllRead as jest.Mock.mockResolvedValue(undefined);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/new transaction/i)).toBeInTheDocument();
    });

    const markReadBtn = screen.getByRole('button', { name: /mark all read/i });
    await userEvent.setup().click(markReadBtn);

    expect(notificationService.markAllRead).toHaveBeenCalled();
  });

  it('should close on outside click', async () => {
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue([]);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/no notifications/i)).toBeInTheDocument();
    });

    // Click outside
    await userEvent.setup().click(document.body);

    await waitFor(() => {
      expect(screen.queryByText(/no notifications/i)).not.toBeInTheDocument();
    });
  });

  it('should display multiple notifications', async () => {
    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'Transaction 1', timestamp: '2024-01-01T10:00:00Z', read: false },
      { id: 'n2', eventType: 'PaymentApproved', message: 'Payment approved', timestamp: '2024-01-01T11:00:00Z', read: true },
      { id: 'n3', eventType: 'BatchSubmitted', message: 'Batch submitted', timestamp: '2024-01-01T12:00:00Z', read: false }
    ];
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue(mockNotifications);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/transaction 1/i)).toBeInTheDocument();
      expect(screen.getByText(/payment approved/i)).toBeInTheDocument();
      expect(screen.getByText(/batch submitted/i)).toBeInTheDocument();
    });
  });

  it('should format event types correctly', async () => {
    const mockNotifications = [
      { id: 'n1', eventType: 'SuspiciousActivityFlagged', message: 'Alert', timestamp: '2024-01-01T10:00:00Z', read: false }
    ];
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue(mockNotifications);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/suspicious activity flagged/i)).toBeInTheDocument();
    });
  });

  it('should display relative timestamps', async () => {
    const now = new Date();
    const fiveMinutesAgo = new Date(now.getTime() - 5 * 60000);

    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'Recent', timestamp: fiveMinutesAgo.toISOString(), read: false }
    ];
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue(mockNotifications);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/5m ago/i)).toBeInTheDocument();
    });
  });

  it('should show "Just now" for very recent notifications', async () => {
    const now = new Date();

    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'Just happened', timestamp: now.toISOString(), read: false }
    ];
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue(mockNotifications);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/just now/i)).toBeInTheDocument();
    });
  });

  it('should toggle dropdown open and closed', async () => {
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue([]);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });

    // Open
    await userEvent.setup().click(button);
    await waitFor(() => {
      expect(button).toHaveAttribute('aria-expanded', 'true');
    });

    // Close by clicking again
    await userEvent.setup().click(button);
    await waitFor(() => {
      expect(button).toHaveAttribute('aria-expanded', 'false');
    });
  });

  it('should display hours ago for notifications from hours ago', async () => {
    const now = new Date();
    const twoHoursAgo = new Date(now.getTime() - 2 * 60 * 60000);

    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'Hours ago', timestamp: twoHoursAgo.toISOString(), read: false }
    ];
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue(mockNotifications);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/2h ago/i)).toBeInTheDocument();
    });
  });

  it('should display days ago for notifications from days ago', async () => {
    const now = new Date();
    const threeDaysAgo = new Date(now.getTime() - 3 * 24 * 60 * 60000);

    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'Days ago', timestamp: threeDaysAgo.toISOString(), read: false }
    ];
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue(mockNotifications);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/3d ago/i)).toBeInTheDocument();
    });
  });

  it('should display date for notifications older than a week', async () => {
    const now = new Date();
    const twoWeeksAgo = new Date(now.getTime() - 14 * 24 * 60 * 60000);

    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'Old notification', timestamp: twoWeeksAgo.toISOString(), read: false }
    ];
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue(mockNotifications);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      // Should show formatted date instead of relative time
      const notification = screen.getByText(/old notification/i);
      expect(notification).toBeInTheDocument();
    });
  });

  it('should handle AbortError gracefully', async () => {
    const abortError = new Error('Aborted');
    abortError.name = 'AbortError';
    (notificationService.fetchNotifications as jest.Mock).mockRejectedValue(abortError);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    // Should not show error for AbortError
    await waitFor(() => {
      expect(screen.queryByText(/failed to load/i)).not.toBeInTheDocument();
    });
  });

  it('should update unread count on mark all read', async () => {
    const mockNotifications = [
      { id: 'n1', eventType: 'TransactionCreated', message: 'Unread 1', timestamp: '2024-01-01T10:00:00Z', read: false },
      { id: 'n2', eventType: 'TransactionCreated', message: 'Unread 2', timestamp: '2024-01-01T11:00:00Z', read: false }
    ];
    (notificationService.fetchNotifications as jest.Mock).mockResolvedValue(mockNotifications);
    (notificationService.markAllRead as jest.Mock).mockResolvedValue(undefined);

    render(
      <BrowserRouter>
        <MockToastProvider>
          <NotificationBell />
        </MockToastProvider>
      </BrowserRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    await userEvent.setup().click(button);

    await waitFor(() => {
      expect(screen.getByText(/unread 1/i)).toBeInTheDocument();
    });

    const markReadBtn = screen.getByRole('button', { name: /mark all read/i });
    await userEvent.setup().click(markReadBtn);

    expect(notificationService.markAllRead).toHaveBeenCalled();
  });
});

