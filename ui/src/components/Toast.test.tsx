import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { ToastProvider, useToast, Toast, ToastType } from './Toast';

describe('Toast Component', () => {
  describe('ToastProvider', () => {
    it('should render children', () => {
      render(
        <ToastProvider>
          <div data-testid="child">Test Content</div>
        </ToastProvider>
      );

      expect(screen.getByTestId('child')).toBeInTheDocument();
    });

    it('should create toast container', () => {
      render(
        <ToastProvider>
          <div />
        </ToastProvider>
      );

      expect(document.querySelector('.toast-container')).toBeInTheDocument();
    });

    it('should have aria-live polite on container', () => {
      render(
        <ToastProvider>
          <div />
        </ToastProvider>
      );

      const container = document.querySelector('.toast-container');
      expect(container).toHaveAttribute('aria-live', 'polite');
    });

    it('should have aria-atomic true on container', () => {
      render(
        <ToastProvider>
          <div />
        </ToastProvider>
      );

      const container = document.querySelector('.toast-container');
      expect(container).toHaveAttribute('aria-atomic', 'true');
    });
  });

  describe('useToast Hook', () => {
    it('should throw error when used outside ToastProvider', () => {
      const TestComponent = () => {
        useToast();
        return null;
      };

      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

      expect(() => render(<TestComponent />)).toThrow(
        'useToast must be used within ToastProvider'
      );

      consoleSpy.mockRestore();
    });

    it('should provide success method', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Test')}>Show Success</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      expect(screen.getByText('Show Success')).toBeInTheDocument();
    });

    it('should provide error method', () => {
      const TestComponent = () => {
        const { error } = useToast();
        return <button onClick={() => error('Test')}>Show Error</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      expect(screen.getByText('Show Error')).toBeInTheDocument();
    });

    it('should provide info method', () => {
      const TestComponent = () => {
        const { info } = useToast();
        return <button onClick={() => info('Test')}>Show Info</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      expect(screen.getByText('Show Info')).toBeInTheDocument();
    });
  });

  describe('Success Toast', () => {
    it('should display success toast message', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Operation successful')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText('Operation successful')).toBeInTheDocument();
    });

    it('should apply success class', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Success')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const toast = document.querySelector('.toast-success');
      expect(toast).toBeInTheDocument();
    });

    it('should have status role for success', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Success')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const toast = document.querySelector('.toast-success');
      expect(toast).toHaveAttribute('role', 'status');
    });

    it('should have close button with aria-label', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Success')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const closeBtn = screen.getByLabelText('Dismiss');
      expect(closeBtn).toBeInTheDocument();
    });
  });

  describe('Error Toast', () => {
    it('should display error toast message', () => {
      const TestComponent = () => {
        const { error } = useToast();
        return <button onClick={() => error('Something went wrong')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    });

    it('should apply error class', () => {
      const TestComponent = () => {
        const { error } = useToast();
        return <button onClick={() => error('Error')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const toast = document.querySelector('.toast-error');
      expect(toast).toBeInTheDocument();
    });

    it('should have alert role for error', () => {
      const TestComponent = () => {
        const { error } = useToast();
        return <button onClick={() => error('Error')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const toast = document.querySelector('.toast-error');
      expect(toast).toHaveAttribute('role', 'alert');
    });
  });

  describe('Info Toast', () => {
    it('should display info toast message', () => {
      const TestComponent = () => {
        const { info } = useToast();
        return <button onClick={() => info('Please note')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText('Please note')).toBeInTheDocument();
    });

    it('should apply info class', () => {
      const TestComponent = () => {
        const { info } = useToast();
        return <button onClick={() => info('Info')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const toast = document.querySelector('.toast-info');
      expect(toast).toBeInTheDocument();
    });

    it('should have status role for info', () => {
      const TestComponent = () => {
        const { info } = useToast();
        return <button onClick={() => info('Info')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const toast = document.querySelector('.toast-info');
      expect(toast).toHaveAttribute('role', 'status');
    });
  });

  describe('Toast Auto-Dismiss', () => {
    beforeEach(() => {
      jest.useFakeTimers();
    });

    afterEach(() => {
      jest.runOnlyPendingTimers();
      jest.useRealTimers();
    });

    it('should auto-dismiss toast after 3200ms', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Success')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));
      expect(screen.getByText('Success')).toBeInTheDocument();

      act(() => { jest.advanceTimersByTime(4500); })

      expect(screen.queryByText('Success')).not.toBeInTheDocument();
    });

    it('should auto-dismiss multiple toasts independently', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Message')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));
      const firstMessage = screen.getByText('Message');
      expect(firstMessage).toBeInTheDocument();

      act(() => { jest.advanceTimersByTime(1000); });

      fireEvent.click(screen.getByText('Trigger'));
      const toasts = screen.getAllByText('Message');
      expect(toasts).toHaveLength(2);

      act(() => { jest.advanceTimersByTime(3500); });
      expect(screen.queryAllByText('Message')).toHaveLength(1);

      act(() => { jest.advanceTimersByTime(1500); });
      expect(screen.queryByText('Message')).not.toBeInTheDocument();
    });
  });

  describe('Toast Dismissal', () => {
    it('should dismiss toast when close button clicked', async () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Success')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));
      expect(screen.getByText('Success')).toBeInTheDocument();

      const closeBtn = screen.getByLabelText('Dismiss');
      fireEvent.click(closeBtn);

      await waitFor(() => {
        expect(screen.queryByText('Success')).not.toBeInTheDocument();
      });
    });

    it('should dismiss only clicked toast', async () => {
      const TestComponent = () => {
        const { success, error } = useToast();
        return (
          <>
            <button onClick={() => success('Success 1')}>Success</button>
            <button onClick={() => error('Error 1')}>Error</button>
          </>
        );
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Success'));
      fireEvent.click(screen.getByText('Error'));

      expect(screen.getByText('Success 1')).toBeInTheDocument();
      expect(screen.getByText('Error 1')).toBeInTheDocument();

      const closeButtons = screen.getAllByLabelText('Dismiss');
      fireEvent.click(closeButtons[0]); // Close first toast

      await waitFor(() => {
        expect(screen.queryByText('Success 1')).not.toBeInTheDocument();
      });
      expect(screen.getByText('Error 1')).toBeInTheDocument();
    });
  });

  describe('Multiple Toasts', () => {
    it('should display multiple toasts simultaneously', () => {
      const TestComponent = () => {
        const { success, error, info } = useToast();
        return (
          <>
            <button onClick={() => success('Success toast')}>Trigger Success</button>
            <button onClick={() => error('Error toast')}>Trigger Error</button>
            <button onClick={() => info('Info toast')}>Trigger Info</button>
          </>
        );
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger Success'));
      fireEvent.click(screen.getByText('Trigger Error'));
      fireEvent.click(screen.getByText('Trigger Info'));

      expect(screen.getByText('Success toast')).toBeInTheDocument();
      expect(screen.getByText('Error toast')).toBeInTheDocument();
      expect(screen.getByText('Info toast')).toBeInTheDocument();
    });

    it('should give each toast unique ID', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return (
          <>
            <button onClick={() => success('Message 1')}>Trigger 1</button>
            <button onClick={() => success('Message 2')}>Trigger 2</button>
          </>
        );
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger 1'));
      fireEvent.click(screen.getByText('Trigger 2'));

      const messages = screen.getAllByText(/Message/);
      expect(messages).toHaveLength(2);
      expect(messages[0]).toHaveTextContent('Message 1');
      expect(messages[1]).toHaveTextContent('Message 2');
    });
  });

  describe('Accessibility', () => {
    it('should have aria-hidden on toast dot', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Success')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const icon = document.querySelector('.toast-icon');
      expect(icon).toHaveAttribute('aria-hidden', 'true');
    });

    it('should announce toasts to screen readers', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Operation completed')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      const container = document.querySelector('.toast-container');
      expect(container).toHaveAttribute('aria-live', 'polite');

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText('Operation completed')).toBeInTheDocument();
    });

    it('should use alert role for errors', () => {
      const TestComponent = () => {
        const { error } = useToast();
        return <button onClick={() => error('Error message')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const alert = screen.getByRole('alert');
      expect(alert).toHaveTextContent('Error message');
    });

    it('should have accessible close button', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('Success')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const closeBtn = screen.getByLabelText('Dismiss');
      expect(closeBtn).toBeInTheDocument();
      expect(closeBtn).toBeVisible();
    });
  });

  describe('Message Types', () => {
    it('should handle empty message', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success('')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));
      const toast = document.querySelector('.toast');
      expect(toast).toBeInTheDocument();
    });

    it('should handle long message', () => {
      const longMessage =
        'This is a very long message that might wrap to multiple lines and should still be displayed correctly in the toast';
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success(longMessage)}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText(longMessage)).toBeInTheDocument();
    });

    it('should handle special characters in message', () => {
      const specialMessage = 'Message with <html> & special chars!';
      const TestComponent = () => {
        const { success } = useToast();
        return <button onClick={() => success(specialMessage)}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText(specialMessage)).toBeInTheDocument();
    });
  });

  describe('Warning Toast', () => {
    it('should display warning toast message', () => {
      const TestComponent = () => {
        const { warning } = useToast();
        return <button onClick={() => warning('Low balance warning')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText('Low balance warning')).toBeInTheDocument();
    });

    it('should apply warning class', () => {
      const TestComponent = () => {
        const { warning } = useToast();
        return <button onClick={() => warning('Warning')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(document.querySelector('.toast-warning')).toBeInTheDocument();
    });

    it('should have alert role for warning', () => {
      const TestComponent = () => {
        const { warning } = useToast();
        return <button onClick={() => warning('Warning')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const toastEl = document.querySelector('.toast-warning');
      expect(toastEl).toHaveAttribute('role', 'alert');
    });

    it('should render action button when action is provided', () => {
      const actionFn = jest.fn();
      const TestComponent = () => {
        const { warning } = useToast();
        return (
          <button onClick={() => warning('Please renew', { label: 'Renew Now', onClick: actionFn })}>
            Trigger
          </button>
        );
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const actionBtn = screen.getByText('Renew Now');
      expect(actionBtn).toBeInTheDocument();
    });

    it('should call action onClick and dismiss when action button clicked', async () => {
      const actionFn = jest.fn();
      const TestComponent = () => {
        const { warning } = useToast();
        return (
          <button onClick={() => warning('Please renew', { label: 'Renew Now', onClick: actionFn })}>
            Trigger
          </button>
        );
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));
      fireEvent.click(screen.getByText('Renew Now'));

      expect(actionFn).toHaveBeenCalledTimes(1);
      await waitFor(() => {
        expect(screen.queryByText('Please renew')).not.toBeInTheDocument();
      });
    });

    it('should auto-dismiss warning after 8000ms', () => {
      jest.useFakeTimers();
      const TestComponent = () => {
        const { warning } = useToast();
        return <button onClick={() => warning('Warning message')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));
      expect(screen.getByText('Warning message')).toBeInTheDocument();

      act(() => { jest.advanceTimersByTime(8300); });
      expect(screen.queryByText('Warning message')).not.toBeInTheDocument();

      act(() => { jest.runOnlyPendingTimers(); });
      jest.useRealTimers();
    });
  });

  describe('Critical Toast', () => {
    it('should display critical banner message', () => {
      const TestComponent = () => {
        const { critical } = useToast();
        return <button onClick={() => critical('Security alert!')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText('Security alert!')).toBeInTheDocument();
    });

    it('should render critical banner with alert role', () => {
      const TestComponent = () => {
        const { critical } = useToast();
        return <button onClick={() => critical('Critical issue')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      const banner = document.querySelector('.toast-critical-banner');
      expect(banner).toBeInTheDocument();
      expect(banner).toHaveAttribute('role', 'alert');
    });

    it('should show Dismiss button on critical banner', () => {
      const TestComponent = () => {
        const { critical } = useToast();
        return <button onClick={() => critical('Critical issue')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText('Dismiss')).toBeInTheDocument();
    });

    it('should dismiss critical banner when Dismiss clicked', () => {
      const TestComponent = () => {
        const { critical } = useToast();
        return <button onClick={() => critical('Critical issue')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));
      expect(screen.getByText('Critical issue')).toBeInTheDocument();

      fireEvent.click(screen.getByText('Dismiss'));
      expect(screen.queryByText('Critical issue')).not.toBeInTheDocument();
    });

    it('should not auto-dismiss critical banners', () => {
      jest.useFakeTimers();
      const TestComponent = () => {
        const { critical } = useToast();
        return <button onClick={() => critical('Critical remains')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));
      act(() => { jest.advanceTimersByTime(30000); });

      expect(screen.getByText('Critical remains')).toBeInTheDocument();

      act(() => { jest.runOnlyPendingTimers(); });
      jest.useRealTimers();
    });

    it('should display only first critical in queue', () => {
      const TestComponent = () => {
        const { critical } = useToast();
        return (
          <>
            <button onClick={() => critical('First critical')}>First</button>
            <button onClick={() => critical('Second critical')}>Second</button>
          </>
        );
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('First'));
      fireEvent.click(screen.getByText('Second'));

      expect(screen.getByText('First critical')).toBeInTheDocument();
      expect(screen.queryByText('Second critical')).not.toBeInTheDocument();
    });
  });

  describe('MAX_VISIBLE cap', () => {
    it('should cap visible toasts at 4 by evicting oldest dismissible', () => {
      const TestComponent = () => {
        const { success } = useToast();
        return (
          <>
            {[1, 2, 3, 4, 5].map((n) => (
              <button key={n} onClick={() => success(`Toast ${n}`)}>Trigger {n}</button>
            ))}
          </>
        );
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      // Fire 5 success toasts — MAX_VISIBLE is 4 so the first is evicted
      fireEvent.click(screen.getByText('Trigger 1'));
      fireEvent.click(screen.getByText('Trigger 2'));
      fireEvent.click(screen.getByText('Trigger 3'));
      fireEvent.click(screen.getByText('Trigger 4'));
      fireEvent.click(screen.getByText('Trigger 5'));

      // Only 4 toasts should be visible at most
      const toasts = document.querySelectorAll('.toast');
      expect(toasts.length).toBeLessThanOrEqual(4);
    });

    it('should not evict persistent (error/critical) toasts when capping', () => {
      const TestComponent = () => {
        const { error, success } = useToast();
        return (
          <>
            <button onClick={() => error('Persistent error')}>Error</button>
            <button onClick={() => success('S1')}>S1</button>
            <button onClick={() => success('S2')}>S2</button>
            <button onClick={() => success('S3')}>S3</button>
            <button onClick={() => success('S4')}>S4</button>
          </>
        );
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Error'));
      fireEvent.click(screen.getByText('S1'));
      fireEvent.click(screen.getByText('S2'));
      fireEvent.click(screen.getByText('S3'));
      fireEvent.click(screen.getByText('S4'));

      // Error toast should still be present (it persists)
      expect(screen.getByText('Persistent error')).toBeInTheDocument();
    });
  });

  describe('Error Toast support text', () => {
    it('should show Contact support text for error toasts', () => {
      const TestComponent = () => {
        const { error } = useToast();
        return <button onClick={() => error('Something failed')}>Trigger</button>;
      };

      render(
        <ToastProvider>
          <TestComponent />
        </ToastProvider>
      );

      fireEvent.click(screen.getByText('Trigger'));

      expect(screen.getByText('Contact support')).toBeInTheDocument();
    });
  });
});
