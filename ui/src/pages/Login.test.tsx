import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter, MemoryRouter } from 'react-router-dom';
import Login from './Login';
import * as auth from '../auth';

jest.mock('../auth');

describe('Login Page', () => {
  const mockSetAuth = auth.setAuth as jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    (global.fetch as jest.Mock).mockClear();
    // Mock window.location.href setter
    delete (window as any).location;
    window.location = { href: '' } as any;
  });

  const renderLogin = (initialPath = '/login') => {
    return render(
      <MemoryRouter initialEntries={[initialPath]}>
        <Login />
      </MemoryRouter>
    );
  };

  describe('Form Rendering', () => {
    it('should render login form', () => {
      renderLogin();

      expect(screen.getByRole('heading', { name: /log in to your account/i })).toBeInTheDocument();
    });

    it('should render email input field', () => {
      renderLogin();

      expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
    });

    it('should render password input field', () => {
      renderLogin();

      expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    });

    it('should render login button', () => {
      renderLogin();

      expect(screen.getByRole('button', { name: /log in/i })).toBeInTheDocument();
    });

    it('should have default email value', () => {
      renderLogin();

      const emailInput = screen.getByLabelText(/email address/i) as HTMLInputElement;
      expect(emailInput.value).toBe('demo');
    });

    it('should have default password value', () => {
      renderLogin();

      const passwordInput = screen.getByLabelText(/password/i) as HTMLInputElement;
      expect(passwordInput.value).toBe('Demo@2026');
    });

    it('should have email input as required', () => {
      renderLogin();

      const emailInput = screen.getByLabelText(/email address/i);
      expect(emailInput).toBeRequired();
    });

    it('should have password input type as password', () => {
      renderLogin();

      const passwordInput = screen.getByLabelText(/password/i);
      expect(passwordInput).toHaveAttribute('type', 'password');
    });
  });

  describe('Form Input', () => {
    it('should update email on change', () => {
      renderLogin();

      const emailInput = screen.getByLabelText(/email address/i) as HTMLInputElement;
      fireEvent.change(emailInput, { target: { value: 'test@example.com' } });

      expect(emailInput.value).toBe('test@example.com');
    });

    it('should update password on change', () => {
      renderLogin();

      const passwordInput = screen.getByLabelText(/password/i) as HTMLInputElement;
      fireEvent.change(passwordInput, { target: { value: 'newpassword123' } });

      expect(passwordInput.value).toBe('newpassword123');
    });
  });

  describe('Form Submission', () => {
    it('should call fetch with correct credentials', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ token: 'test-token', userId: 'user-123' }),
      });

      renderLogin();

      const emailInput = screen.getByLabelText(/email address/i);
      const passwordInput = screen.getByLabelText(/password/i);
      const submitButton = screen.getByRole('button', { name: /log in/i });

      fireEvent.change(emailInput, { target: { value: 'user@example.com' } });
      fireEvent.change(passwordInput, { target: { value: 'password123' } });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith('/users/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email: 'user@example.com', password: 'password123' }),
          credentials: 'include',
        });
      });
    });

    it('should call setAuth with token and userId on success', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ token: 'test-token', userId: 'user-123' }),
      });

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(mockSetAuth).toHaveBeenCalledWith('test-token', 'user-123');
      });
    });

    it('should redirect to /accounts after successful login', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ token: 'test-token', userId: 'user-123' }),
      });

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(window.location.href).toBe('/accounts');
      });
    });

    it('should show loading state during submission', async () => {
      (global.fetch as jest.Mock).mockImplementation(() => new Promise(() => {}));

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /logging in|loading/i })).toBeInTheDocument();
      });
    });

    it('should disable button during submission', async () => {
      (global.fetch as jest.Mock).mockImplementation(() => new Promise(() => {}));

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole('button')).toBeDisabled();
      });
    });
  });

  describe('Error Handling', () => {
    it('should display error message on login failure', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        json: async () => ({ message: 'Invalid credentials' }),
      });

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/invalid credentials/i)).toBeInTheDocument();
      });
    });

    it('should display generic error when no message provided', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        json: async () => ({}),
      });

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/login failed/i)).toBeInTheDocument();
      });
    });

    it('should display error when response has no token', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ userId: 'user-123' }), // No token
      });

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/invalid response from server/i)).toBeInTheDocument();
      });
    });

    it('should handle network error', async () => {
      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/network error/i)).toBeInTheDocument();
      });
    });

    it('should handle JSON parse error in error response', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        json: async () => { throw new Error('Parse error'); },
      });

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/login failed/i)).toBeInTheDocument();
      });
    });

    it('should clear error when form is resubmitted', async () => {
      (global.fetch as jest.Mock)
        .mockResolvedValueOnce({
          ok: false,
          json: async () => ({ message: 'Invalid credentials' }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ token: 'test-token', userId: 'user-123' }),
        });

      renderLogin();

      const submitButton = screen.getByRole('button', { name: /log in/i });
      
      // First submit - should show error
      fireEvent.click(submitButton);
      await waitFor(() => {
        expect(screen.getByText(/invalid credentials/i)).toBeInTheDocument();
      });

      // Second submit - error should be cleared
      fireEvent.click(submitButton);
      await waitFor(() => {
        expect(screen.queryByText(/invalid credentials/i)).not.toBeInTheDocument();
      });
    });
  });

  describe('Redirect Path', () => {
    it('should redirect to the original path after login', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ token: 'test-token', userId: 'user-123' }),
      });

      render(
        <MemoryRouter 
          initialEntries={[{ 
            pathname: '/login', 
            state: { from: { pathname: '/budget' } } 
          }]}
        >
          <Login />
        </MemoryRouter>
      );

      const submitButton = screen.getByRole('button', { name: /log in/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(window.location.href).toBe('/budget');
      });
    });
  });

  describe('Accessibility', () => {
    it('should have form inputs with labels', () => {
      renderLogin();

      expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    });

    it('should have input ids matching label for attributes', () => {
      renderLogin();

      const emailInput = screen.getByLabelText(/email address/i);
      const passwordInput = screen.getByLabelText(/password/i);

      expect(emailInput).toHaveAttribute('id', 'email');
      expect(passwordInput).toHaveAttribute('id', 'password');
    });

    it('should have placeholders for inputs', () => {
      renderLogin();

      const emailInput = screen.getByLabelText(/email address/i);
      expect(emailInput).toHaveAttribute('placeholder', 'Enter your email');
    });
  });

  describe('Styling', () => {
    it('should have auth-container class on wrapper', () => {
      const { container } = renderLogin();

      expect(container.querySelector('.auth-container')).toBeInTheDocument();
    });

    it('should have auth-card class on form card', () => {
      const { container } = renderLogin();

      expect(container.querySelector('.auth-card')).toBeInTheDocument();
    });
  });
});
