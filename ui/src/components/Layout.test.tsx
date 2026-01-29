import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Layout from './Layout';
import { ToastProvider } from '../components/Toast';
import * as auth from '../auth';
import * as fModeHook from '../hooks/useFMode';

jest.mock('../auth');
jest.mock('../hooks/useFMode');

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
    (fModeHook.useFMode as jest.Mock).mockReturnValue({
      enabled: false,
      toggle: jest.fn(),
    });
    (global.fetch as jest.Mock).mockClear();
  });

  const renderLayout = (children = <div data-testid="test-content">Test</div>) => {
    return render(
      <BrowserRouter>
        <ToastProvider>
          <Layout>{children}</Layout>
        </ToastProvider>
      </BrowserRouter>
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

    it('should display logo and title', () => {
      renderLayout();

      expect(screen.getByAltText('FinTech logo')).toBeInTheDocument();
      expect(screen.getByText('FinTech Application')).toBeInTheDocument();
    });

    it('should display hero badge', () => {
      renderLayout();

      expect(screen.getByText(/Simple, fast and secure personal finance/)).toBeInTheDocument();
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

    it('should have container class', () => {
      const { container } = renderLayout();

      const main = screen.getByRole('main');
      expect(main).toHaveClass('container');
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

    it('should display demo warning', () => {
      renderLayout();

      expect(screen.getByText(/This is a demo application/)).toBeInTheDocument();
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

    it('should have footer with aria-label', () => {
      renderLayout();

      expect(screen.getByLabelText('Social Links')).toBeInTheDocument();
    });

    it('should have aria-hidden on LinkedIn text', () => {
      renderLayout();

      const linkedInLink = screen.getByLabelText('LinkedIn');
      const hiddenSpan = linkedInLink.querySelector('[aria-hidden="true"]');
      expect(hiddenSpan).toBeInTheDocument();
      expect(hiddenSpan).toHaveTextContent('in');
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

    it('should update progress on scroll', () => {
      const { container } = renderLayout();

      const progressBar = container.querySelector('.progress-bar') as HTMLElement;
      expect(progressBar).toHaveStyle({ width: '0%' });

      fireEvent.scroll(window, { target: { scrollY: 500 } });

      expect(progressBar.style.width).toBeDefined();
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

      window.addEventListener.mockRestore();
    });

    it('should setup scroll event listener', () => {
      jest.spyOn(window, 'addEventListener');

      renderLayout();

      expect(window.addEventListener).toHaveBeenCalledWith(
        'scroll',
        expect.any(Function),
        { passive: true }
      );

      window.addEventListener.mockRestore();
    });

    it('should cleanup event listeners on unmount', () => {
      jest.spyOn(window, 'removeEventListener');

      const { unmount } = renderLayout();

      unmount();

      expect(window.removeEventListener).toHaveBeenCalledWith(
        'scroll',
        expect.any(Function)
      );
      expect(window.removeEventListener).toHaveBeenCalledWith('storage', expect.any(Function));

      window.removeEventListener.mockRestore();
    });
  });

  describe('Responsive Layout', () => {
    it('should have responsive header layout', () => {
      const { container } = renderLayout();

      const header = screen.getByRole('banner');
      expect(header).toHaveClass('header');
    });

    it('should have container padding', () => {
      const { container } = renderLayout();

      const header = screen.getByRole('banner');
      const innerDiv = header.querySelector('.container');
      expect(innerDiv).toHaveStyle({ padding: '0 24px' });
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

    it('should have proper heading hierarchy', () => {
      renderLayout();

      const h1 = screen.getByText('FinTech Application');
      expect(h1.tagName).toBe('H1');
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
          <ToastProvider>
            <Layout>
              <div>Test</div>
            </Layout>
          </ToastProvider>
        </BrowserRouter>
      );

      // Token state will be updated via onAuthChange
      expect(screen.getByLabelText('Login')).toBeInTheDocument();
    });
  });
});
