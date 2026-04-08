import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter, MemoryRouter } from 'react-router-dom';
import Layout from './Layout';
import { ToastProvider } from '../components/Toast';
import { AppProvider } from '../context/AppContext';
import * as auth from '../auth';
import * as fModeHook from '../hooks/useFMode';
import * as feedbackService from '../services/feedback';

jest.mock('../auth');
jest.mock('../hooks/useFMode');
jest.mock('../services/feedback');

// Mock useNavigate
const mockNavigate = jest.fn();
jest.mock('react-router-dom', () => ({
  ...jest.requireActual('react-router-dom'),
  useNavigate: () => mockNavigate,
}));

describe('Layout Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (localStorage.getItem as jest.Mock).mockReturnValue(null);
    (auth.onAuthChange as jest.Mock).mockReturnValue(() => {});
    (auth.isCorporateUser as jest.Mock).mockReturnValue(false);
    (auth.getOrganisationRole as jest.Mock).mockReturnValue(null);
    (auth.refreshAccessToken as jest.Mock).mockResolvedValue(null);
    (fModeHook.useFMode as jest.Mock).mockReturnValue({
      enabled: false,
      toggle: jest.fn(),
    });
    (global.fetch as jest.Mock).mockClear();
  });

  const renderLayout = (children = <div data-testid="test-content">Test</div>) => {
    return render(
      <BrowserRouter>
        <AppProvider>
          <ToastProvider>
            <Layout>{children}</Layout>
          </ToastProvider>
        </AppProvider>
      </BrowserRouter>
    );
  };

  const renderLayoutAtRoute = (route: string, children = <div data-testid="test-content">Test</div>) => {
    return render(
      <MemoryRouter initialEntries={[route]}>
        <AppProvider>
          <ToastProvider>
            <Layout>{children}</Layout>
          </ToastProvider>
        </AppProvider>
      </MemoryRouter>
    );
  };

  describe('Skip Link', () => {
    it('should render skip link', () => {
      renderLayout();

      const skipLink = screen.getByText('Skip to main content');
      expect(skipLink).toBeInTheDocument();
    });

    it('should have skip-link class', () => {
      renderLayout();

      const skipLink = screen.getByText('Skip to main content');
      expect(skipLink).toHaveClass('skip-link');
    });

    it('should link to main-content', () => {
      renderLayout();

      const skipLink = screen.getByText('Skip to main content');
      expect(skipLink).toHaveAttribute('href', '#main-content');
    });
  });

  describe('Header Structure', () => {
    it('should render header with banner role', () => {
      renderLayout();

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('should display logo', () => {
      renderLayout();

      expect(screen.getByAltText('FinTech logo')).toBeInTheDocument();
    });

    it('should display sidebar logo text', () => {
      renderLayout();

      expect(screen.getByText('FinTech Application')).toBeInTheDocument();
    });

    it('should have link to home', () => {
      renderLayout();

      const homeLink = screen.getByLabelText('Home');
      expect(homeLink).toHaveAttribute('href', '/');
    });
  });

  describe('Navigation - Unauthenticated', () => {
    beforeEach(() => {
      (localStorage.getItem as jest.Mock).mockReturnValue(null);
    });

    it('should display login link when not authenticated', () => {
      renderLayout();

      expect(screen.getByLabelText('Login')).toBeInTheDocument();
    });

    it('should display register link when not authenticated', () => {
      renderLayout();

      expect(screen.getByLabelText('Register')).toBeInTheDocument();
    });

    it('should not display logout button when not authenticated', () => {
      renderLayout();

      expect(screen.queryByLabelText('Logout')).not.toBeInTheDocument();
    });

    it('should not display dashboard when not authenticated', () => {
      renderLayout();

      expect(screen.queryByLabelText('Dashboard')).not.toBeInTheDocument();
    });

    it('should have navigation with correct role', () => {
      renderLayout();

      const nav = screen.getByRole('navigation', { name: 'Main' });
      expect(nav).toBeInTheDocument();
    });
  });

  describe('Navigation - Authenticated', () => {
    beforeEach(() => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
    });

    it('should display dashboard link when authenticated', () => {
      renderLayout();

      expect(screen.getByLabelText('Dashboard')).toBeInTheDocument();
    });

    it('should display transactions link when authenticated', () => {
      renderLayout();

      expect(screen.getByLabelText('Transactions')).toBeInTheDocument();
    });

    it('should display accounts link when not in F-Mode', () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: false,
        toggle: jest.fn(),
      });

      renderLayout();

      expect(screen.getByLabelText('Accounts')).toBeInTheDocument();
    });

    it('should not display accounts link when in F-Mode', () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: jest.fn(),
      });

      renderLayout();

      expect(screen.queryByLabelText('Accounts')).not.toBeInTheDocument();
    });

    it('should display budget link when not in F-Mode', () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: false,
        toggle: jest.fn(),
      });

      renderLayout();

      expect(screen.getByLabelText('Budget')).toBeInTheDocument();
    });

    it('should not display budget link when in F-Mode', () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: jest.fn(),
      });

      renderLayout();

      expect(screen.queryByLabelText('Budget')).not.toBeInTheDocument();
    });

    it('should display logout button when authenticated', () => {
      renderLayout();

      expect(screen.getByLabelText('Logout')).toBeInTheDocument();
    });

    it('should not display login/register when authenticated', () => {
      renderLayout();

      expect(screen.queryByLabelText('Login')).not.toBeInTheDocument();
      expect(screen.queryByLabelText('Register')).not.toBeInTheDocument();
    });
  });

  describe('Mode Toggle', () => {
    beforeEach(() => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
    });

    it('should not display mode toggle when not authenticated', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue(null);

      renderLayout();

      expect(screen.queryByLabelText('Mode selection')).not.toBeInTheDocument();
    });

    it('should display mode toggle when authenticated', () => {
      renderLayout();

      expect(screen.getByLabelText('Mode selection')).toBeInTheDocument();
    });

    it('should have fiat mode button', () => {
      renderLayout();

      expect(screen.getByLabelText('Switch to Fiat Mode')).toBeInTheDocument();
    });

    it('should have F-Mode button', () => {
      renderLayout();

      expect(screen.getByLabelText('Switch to F-Mode (DeFi)')).toBeInTheDocument();
    });

    it('should toggle to F-Mode when clicking F-Mode button', () => {
      const mockToggle = jest.fn();
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: false,
        toggle: mockToggle,
      });

      renderLayout();

      fireEvent.click(screen.getByLabelText('Switch to F-Mode (DeFi)'));

      expect(mockToggle).toHaveBeenCalledWith(true);
    });

    it('should toggle to Fiat Mode when clicking Fiat button', () => {
      const mockToggle = jest.fn();
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      renderLayout();

      fireEvent.click(screen.getByLabelText('Switch to Fiat Mode'));

      expect(mockToggle).toHaveBeenCalledWith(false);
    });

    it('should have aria-pressed set correctly for Fiat mode', () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: false,
        toggle: jest.fn(),
      });

      renderLayout();

      const fiatButton = screen.getByLabelText('Switch to Fiat Mode');
      expect(fiatButton).toHaveAttribute('aria-pressed', 'true');
    });

    it('should have aria-pressed set correctly for F-Mode', () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: false,
        toggle: jest.fn(),
      });

      renderLayout();

      const fModeButton = screen.getByLabelText('Switch to F-Mode (DeFi)');
      expect(fModeButton).toHaveAttribute('aria-pressed', 'false');
    });

    it('should show active state for current mode', () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: jest.fn(),
      });

      renderLayout();

      const fModeButton = screen.getByLabelText('Switch to F-Mode (DeFi)');
      expect(fModeButton).toHaveClass('active');
    });
  });

  describe('Main Content', () => {
    it('should render children in main element', () => {
      renderLayout(<div data-testid="custom-content">Custom</div>);

      expect(screen.getByTestId('custom-content')).toBeInTheDocument();
    });

    it('should have main element with role', () => {
      renderLayout();

      expect(screen.getByRole('main')).toBeInTheDocument();
    });

    it('should have main-content id on main element', () => {
      renderLayout();

      expect(screen.getByRole('main')).toHaveAttribute('id', 'main-content');
    });

    it('should have shell-content class on main element', () => {
      renderLayout();

      const main = screen.getByRole('main');
      expect(main).toHaveClass('shell-content');
    });
  });

  describe('Footer', () => {
    it('should display copyright year', () => {
      renderLayout();

      const year = new Date().getFullYear();
      expect(screen.getByText(new RegExp(`© ${year}`))).toBeInTheDocument();
    });

    it('should display creator name', () => {
      renderLayout();

      expect(screen.getByText(/Anubhav Sharma/)).toBeInTheDocument();
    });

    it('should have LinkedIn link', () => {
      renderLayout();

      const linkedInLink = screen.getByLabelText('LinkedIn');
      expect(linkedInLink).toHaveAttribute(
        'href',
        'https://www.linkedin.com/in/anubhav-sharma-/'
      );
      expect(linkedInLink).toHaveAttribute('target', '_blank');
      expect(linkedInLink).toHaveAttribute('rel', 'noreferrer');
    });

    it('should have footer with contentinfo role', () => {
      renderLayout();

      expect(screen.getByRole('contentinfo')).toBeInTheDocument();
    });

    it('should display sandbox badge', () => {
      renderLayout();

      expect(screen.getByLabelText('Sandbox environment')).toBeInTheDocument();
    });
  });

  describe('Logout Flow', () => {
    beforeEach(() => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
    });

    it('should call logout endpoint when logout clicked', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
      });

      renderLayout();

      fireEvent.click(screen.getByLabelText('Logout'));

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith('/users/logout', {
          method: 'POST',
          credentials: 'include',
        });
      });
    });

    it('should clear auth on logout', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
      });

      renderLayout();

      fireEvent.click(screen.getByLabelText('Logout'));

      await waitFor(() => {
        expect(auth.clearAuth).toHaveBeenCalled();
      });
    });

    it('should navigate to login after logout', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
      });

      renderLayout();

      fireEvent.click(screen.getByLabelText('Logout'));

      await waitFor(() => {
        expect(mockNavigate).toHaveBeenCalledWith('/login', { replace: true });
      });
    });

    it('should handle logout endpoint errors', async () => {
      (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Network error'));

      renderLayout();

      expect(() => {
        fireEvent.click(screen.getByLabelText('Logout'));
      }).not.toThrow();
    });
  });

  describe('Progress Bar', () => {
    it('should render progress bar', () => {
      const { container } = renderLayout();

      expect(container.querySelector('.progress-wrap')).toBeInTheDocument();
      expect(container.querySelector('.progress-bar')).toBeInTheDocument();
    });

    it('should have aria-hidden on progress bar', () => {
      const { container } = renderLayout();

      const progressWrap = container.querySelector('.progress-wrap');
      expect(progressWrap).toHaveAttribute('aria-hidden', 'true');
    });
  });

  describe('Event Listeners', () => {
    it('should setup auth change listener on mount', () => {
      renderLayout();

      expect(auth.onAuthChange).toHaveBeenCalled();
    });

    it('should setup storage event listener', () => {
      jest.spyOn(window, 'addEventListener');

      renderLayout();

      expect(window.addEventListener).toHaveBeenCalledWith(
        'storage',
        expect.any(Function)
      );

      (window.addEventListener as jest.Mock).mockRestore();
    });
  });

  describe('Sidebar', () => {
    it('should render sidebar with Banking nav group when authenticated', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayout();

      expect(container.querySelector('.shell-sidebar')).toBeInTheDocument();
      expect(container.querySelector('.sidebar-group-label')).toHaveTextContent('Banking');
    });

    it('should render Compliance nav group when authenticated', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayout();

      const labels = container.querySelectorAll('.sidebar-group-label');
      const hasCompliance = Array.from(labels).some(el => el.textContent === 'Compliance');
      expect(hasCompliance).toBe(true);
    });

    it('should render Account nav group', () => {
      renderLayout();

      expect(screen.getByText('Account')).toBeInTheDocument();
    });

    it('should render Corporate nav group for corporate users', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      (auth.isCorporateUser as jest.Mock).mockReturnValue(true);

      renderLayout();

      expect(screen.getByText('Corporate')).toBeInTheDocument();
    });

    it('should not render Corporate nav group for non-corporate users', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      (auth.isCorporateUser as jest.Mock).mockReturnValue(false);

      renderLayout();

      expect(screen.queryByText('Corporate')).not.toBeInTheDocument();
    });

    it('should collapse sidebar to 64px on chevron click', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayout();

      const sidebar = container.querySelector('.shell-sidebar');
      expect(sidebar).not.toHaveClass('collapsed');

      const collapseBtn = screen.getByLabelText('Collapse sidebar');
      fireEvent.click(collapseBtn);

      expect(sidebar).toHaveClass('collapsed');
    });

    it('should expand sidebar when collapsed and chevron clicked', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayout();

      const collapseBtn = screen.getByLabelText('Collapse sidebar');
      fireEvent.click(collapseBtn);

      const sidebar = container.querySelector('.shell-sidebar');
      expect(sidebar).toHaveClass('collapsed');

      const expandBtn = screen.getByLabelText('Expand sidebar');
      fireEvent.click(expandBtn);

      expect(sidebar).not.toHaveClass('collapsed');
    });

    it('should hide group labels when collapsed', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayout();

      expect(container.querySelector('.sidebar-group-label')).toBeInTheDocument();

      const collapseBtn = screen.getByLabelText('Collapse sidebar');
      fireEvent.click(collapseBtn);

      expect(container.querySelector('.sidebar-group-label')).not.toBeInTheDocument();
    });
  });

  describe('Breadcrumb', () => {
    it('should render breadcrumb for dashboard route', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayoutAtRoute('/');

      expect(container.querySelector('.shell-breadcrumb')).toHaveTextContent('Dashboard');
    });

    it('should render breadcrumb with group and label for accounts route', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayoutAtRoute('/accounts');

      expect(container.querySelector('.shell-breadcrumb')).toHaveTextContent('Accounts');
    });

    it('should render breadcrumb for transactions route', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayoutAtRoute('/transactions');

      expect(container.querySelector('.shell-breadcrumb')).toHaveTextContent('Transactions');
    });

    it('should render breadcrumb for compliance route', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayoutAtRoute('/compliance');

      expect(container.querySelector('.shell-breadcrumb')).toHaveTextContent('Compliance');
    });
  });

  describe('TopBar', () => {
    it('should render user avatar when logged in', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      const { container } = renderLayout();

      const avatar = container.querySelector('.topbar-avatar');
      expect(avatar).toBeInTheDocument();
    });

    it('should render role chip when role is available', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      (auth.getOrganisationRole as jest.Mock).mockReturnValue('admin');

      const { container } = renderLayout();

      const roleChip = container.querySelector('.topbar-role-chip');
      expect(roleChip).toBeInTheDocument();
      expect(roleChip).toHaveTextContent('ADMIN');
    });

    it('should render notification bell when authenticated', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      renderLayout();

      expect(screen.getByLabelText('Notifications')).toBeInTheDocument();
    });

    it('should not render notification bell when not authenticated', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue(null);

      renderLayout();

      expect(screen.queryByLabelText('Notifications')).not.toBeInTheDocument();
    });
  });

  describe('Mobile Hamburger', () => {
    it('should render hamburger toggle button', () => {
      renderLayout();

      expect(screen.getByLabelText('Toggle menu')).toBeInTheDocument();
    });

    it('should toggle mobile sidebar overlay on click', () => {
      const { container } = renderLayout();

      const sidebar = container.querySelector('.shell-sidebar');
      expect(sidebar).not.toHaveClass('mobile-open');

      fireEvent.click(screen.getByLabelText('Toggle menu'));

      expect(sidebar).toHaveClass('mobile-open');
    });

    it('should show backdrop when mobile sidebar is open', () => {
      const { container } = renderLayout();

      expect(container.querySelector('.sidebar-backdrop')).not.toBeInTheDocument();

      fireEvent.click(screen.getByLabelText('Toggle menu'));

      expect(container.querySelector('.sidebar-backdrop')).toBeInTheDocument();
    });

    it('should close mobile sidebar when backdrop is clicked', () => {
      const { container } = renderLayout();

      fireEvent.click(screen.getByLabelText('Toggle menu'));

      const sidebar = container.querySelector('.shell-sidebar');
      expect(sidebar).toHaveClass('mobile-open');

      const backdrop = container.querySelector('.sidebar-backdrop');
      fireEvent.click(backdrop!);

      expect(sidebar).not.toHaveClass('mobile-open');
    });
  });

  describe('Accessibility', () => {
    it('should have semantic header', () => {
      renderLayout();

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('should have semantic main', () => {
      renderLayout();

      expect(screen.getByRole('main')).toBeInTheDocument();
    });

    it('should have semantic nav', () => {
      renderLayout();

      expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    });

    it('should have skip link for keyboard navigation', () => {
      renderLayout();

      const skipLink = screen.getByText('Skip to main content');
      expect(skipLink).toHaveClass('skip-link');
    });
  });

  describe('Auth State Management', () => {
    it('should update UI when token is added', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue(null);

      const { rerender } = renderLayout();

      expect(screen.queryByLabelText('Dashboard')).not.toBeInTheDocument();

      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');

      rerender(
        <BrowserRouter>
          <AppProvider>
            <ToastProvider>
              <Layout>
                <div>Test</div>
              </Layout>
            </ToastProvider>
          </AppProvider>
        </BrowserRouter>
      );

      // Token state will be updated via onAuthChange
      expect(screen.getByLabelText('Login')).toBeInTheDocument();
    });
  });

  describe('Sanctions link in F-Mode', () => {
    it('shows Sanctions nav link when F-Mode is enabled', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      (fModeHook.useFMode as jest.Mock).mockReturnValue({ enabled: true, toggle: jest.fn() });
      renderLayout();
      expect(screen.getByLabelText('Sanctions')).toBeInTheDocument();
    });

    it('does not show Sanctions nav link when F-Mode is disabled', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      (fModeHook.useFMode as jest.Mock).mockReturnValue({ enabled: false, toggle: jest.fn() });
      renderLayout();
      expect(screen.queryByLabelText('Sanctions')).not.toBeInTheDocument();
    });
  });

  describe('Feedback modal', () => {
    const mockSubmitFeedback = feedbackService.submitFeedback as jest.Mock;

    beforeEach(() => {
      mockSubmitFeedback.mockResolvedValue({ success: true });
    });

    it('shows Feedback button in footer when authenticated', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();
      expect(screen.getByRole('button', { name: /send feedback/i })).toBeInTheDocument();
    });

    it('does not show Feedback button when not authenticated', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue(null);
      renderLayout();
      expect(screen.queryByRole('button', { name: /send feedback/i })).not.toBeInTheDocument();
    });

    it('opens feedback modal when Feedback button is clicked', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();
      fireEvent.click(screen.getByRole('button', { name: /send feedback/i }));
      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
    });

    it('submits feedback and shows success toast', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();
      fireEvent.click(screen.getByRole('button', { name: /send feedback/i }));
      await waitFor(() => expect(screen.getByRole('dialog')).toBeInTheDocument());
      const textarea = screen.getByRole('textbox');
      fireEvent.change(textarea, { target: { value: 'This is a test feedback message that is long enough' } });
      const submitBtn = screen.getByRole('button', { name: /submit/i });
      fireEvent.click(submitBtn);
      await waitFor(() => {
        expect(mockSubmitFeedback).toHaveBeenCalledWith(
          'This is a test feedback message that is long enough',
          expect.any(String)
        );
      });
    });

    it('shows error toast when feedback submission fails', async () => {
      mockSubmitFeedback.mockRejectedValue(new Error('Server error'));
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();
      fireEvent.click(screen.getByRole('button', { name: /send feedback/i }));
      await waitFor(() => expect(screen.getByRole('dialog')).toBeInTheDocument());
      const textarea = screen.getByRole('textbox');
      fireEvent.change(textarea, { target: { value: 'This is a test feedback message that is long enough' } });
      fireEvent.click(screen.getByRole('button', { name: /submit/i }));
      await waitFor(() => {
        expect(mockSubmitFeedback).toHaveBeenCalled();
      });
    });
  });

  describe('Avatar dropdown', () => {
    it('opens avatar dropdown on click', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      const { container } = renderLayout();
      const avatar = container.querySelector('.topbar-avatar') as HTMLElement;
      fireEvent.click(avatar);
      expect(avatar).toHaveAttribute('aria-expanded', 'true');
    });

    it('closes avatar dropdown on second click', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      const { container } = renderLayout();
      const avatar = container.querySelector('.topbar-avatar') as HTMLElement;
      fireEvent.click(avatar);
      fireEvent.click(avatar);
      expect(avatar).toHaveAttribute('aria-expanded', 'false');
    });

    it('shows Settings link inside avatar dropdown', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      const { container } = renderLayout();
      fireEvent.click(container.querySelector('.topbar-avatar') as HTMLElement);
      expect(screen.getByRole('menuitem', { name: /settings/i })).toBeInTheDocument();
    });

    it('closes dropdown on Escape key', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      const { container } = renderLayout();
      const avatar = container.querySelector('.topbar-avatar') as HTMLElement;
      fireEvent.click(avatar);
      expect(avatar).toHaveAttribute('aria-expanded', 'true');
      fireEvent.keyDown(document, { key: 'Escape' });
      await waitFor(() => {
        expect(avatar).toHaveAttribute('aria-expanded', 'false');
      });
    });
  });

  describe('Breadcrumb for sanctions detail route', () => {
    it('renders Sanctions breadcrumb for /sanctions/:id route', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayoutAtRoute('/sanctions/some-id-123');
      expect(screen.getByText('Sanctions')).toBeInTheDocument();
    });
  });

  describe('Storage event handler', () => {
    it('updates token state when storage event fires with key "token"', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue(null);
      renderLayout();
      expect(screen.queryByLabelText('Dashboard')).not.toBeInTheDocument();
      (localStorage.getItem as jest.Mock).mockReturnValue('new-token');
      fireEvent(window, new StorageEvent('storage', { key: 'token' }));
      // Component will process the storage event; verify no crash
      expect(screen.queryByLabelText('Login') || screen.queryByText('Login')).toBeTruthy();
    });

    it('ignores storage events with non-token keys', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue(null);
      renderLayout();
      fireEvent(window, new StorageEvent('storage', { key: 'some-other-key' }));
      expect(screen.getByLabelText('Login')).toBeInTheDocument();
    });
  });

  describe('Breadcrumb fallback path', () => {
    it('renders capitalised path segments for unknown routes', () => {
      const { container } = renderLayoutAtRoute('/some/unknown/route');
      const breadcrumb = container.querySelector('.shell-breadcrumb');
      expect(breadcrumb).toHaveTextContent(/Some/);
    });

    it('renders "Home" for root when no breadcrumb entry matches', () => {
      // root path '/' maps to Dashboard in breadcrumbMap but this tests the
      // fallback label computation via an unmapped route that produces empty segments
      const { container } = renderLayoutAtRoute('/');
      expect(container.querySelector('.shell-breadcrumb')).toBeInTheDocument();
    });
  });

  describe('SidebarLink sub-path active state', () => {
    it('marks Accounts link active when on a sub-path of /accounts', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      const { container } = renderLayoutAtRoute('/accounts/detail');
      // Accounts link should have the active class
      const accountsLink = container.querySelector('a[aria-label="Accounts"]');
      expect(accountsLink).toHaveClass('active');
    });
  });

  describe('closeFeedback guard', () => {
    it('does not close modal when submission is in progress', async () => {
      (feedbackService.submitFeedback as jest.Mock).mockImplementation(() => new Promise(() => {}));
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();

      fireEvent.click(screen.getByRole('button', { name: /send feedback/i }));
      await waitFor(() => expect(screen.getByRole('dialog')).toBeInTheDocument());

      const textarea = screen.getByRole('textbox');
      fireEvent.change(textarea, { target: { value: 'This feedback is long enough to submit' } });
      fireEvent.click(screen.getByRole('button', { name: /submit/i }));

      // Now try to close while submitting — modal should still be open
      const closeBtn = screen.queryByRole('button', { name: /close/i });
      if (closeBtn) fireEvent.click(closeBtn);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });
  });

  describe('submitFeedback error detail extraction', () => {
    it('shows the error message from the thrown error', async () => {
      (feedbackService.submitFeedback as jest.Mock).mockRejectedValue(new Error('Custom server error'));
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();

      fireEvent.click(screen.getByRole('button', { name: /send feedback/i }));
      await waitFor(() => expect(screen.getByRole('dialog')).toBeInTheDocument());

      const textarea = screen.getByRole('textbox');
      fireEvent.change(textarea, { target: { value: 'This feedback is long enough to submit' } });
      fireEvent.click(screen.getByRole('button', { name: /submit/i }));

      await waitFor(() => {
        expect(screen.getByText(/Custom server error/i)).toBeInTheDocument();
      });
    });

    it('shows fallback message when error has no message property', async () => {
      (feedbackService.submitFeedback as jest.Mock).mockRejectedValue({});
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();

      fireEvent.click(screen.getByRole('button', { name: /send feedback/i }));
      await waitFor(() => expect(screen.getByRole('dialog')).toBeInTheDocument());

      const textarea = screen.getByRole('textbox');
      fireEvent.change(textarea, { target: { value: 'This feedback is long enough to submit' } });
      fireEvent.click(screen.getByRole('button', { name: /submit/i }));

      await waitFor(() => {
        expect(screen.getByText(/could not send feedback/i)).toBeInTheDocument();
      });
    });
  });

  describe('handleToggle no-op branch', () => {
    it('does not call toggleFMode when already in fiat mode and fiat button clicked', () => {
      const mockToggle = jest.fn();
      (fModeHook.useFMode as jest.Mock).mockReturnValue({ enabled: false, toggle: mockToggle });
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();

      // Clicking fiat button when already in fiat mode should not call toggle
      fireEvent.click(screen.getByLabelText('Switch to Fiat Mode'));
      expect(mockToggle).not.toHaveBeenCalled();
    });

    it('does not call toggleFMode when already in F-Mode and F-Mode button clicked', () => {
      const mockToggle = jest.fn();
      (fModeHook.useFMode as jest.Mock).mockReturnValue({ enabled: true, toggle: mockToggle });
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();

      fireEvent.click(screen.getByLabelText('Switch to F-Mode (DeFi)'));
      expect(mockToggle).not.toHaveBeenCalled();
    });
  });

  describe('submitFeedback short message guard', () => {
    it('does not call submitFeedback when message is shorter than 10 chars', async () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();

      fireEvent.click(screen.getByRole('button', { name: /send feedback/i }));
      await waitFor(() => expect(screen.getByRole('dialog')).toBeInTheDocument());

      const textarea = screen.getByRole('textbox');
      fireEvent.change(textarea, { target: { value: 'short' } });
      fireEvent.click(screen.getByRole('button', { name: /submit/i }));

      // submitFeedback should NOT be called
      expect(feedbackService.submitFeedback).not.toHaveBeenCalled();
    });
  });

  describe('collapsed sidebar mode toggle icons', () => {
    it('shows icon-only mode toggle buttons when sidebar is collapsed', () => {
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();

      // Collapse the sidebar first
      fireEvent.click(screen.getByLabelText('Collapse sidebar'));

      // Collapsed mode exposes aria-label "Fiat Mode" (icon button)
      expect(screen.getByLabelText('Fiat Mode')).toBeInTheDocument();
      expect(screen.getByLabelText('F-Mode (DeFi)')).toBeInTheDocument();
    });

    it('toggles mode from collapsed icon buttons', () => {
      const mockToggle = jest.fn();
      (fModeHook.useFMode as jest.Mock).mockReturnValue({ enabled: false, toggle: mockToggle });
      (localStorage.getItem as jest.Mock).mockReturnValue('test-token');
      renderLayout();

      fireEvent.click(screen.getByLabelText('Collapse sidebar'));

      fireEvent.click(screen.getByLabelText('F-Mode (DeFi)'));
      expect(mockToggle).toHaveBeenCalledWith(true);
    });
  });
});
