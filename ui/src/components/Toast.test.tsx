import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
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
      expect(closeBtn).toHaveTextContent('x');
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

      jest.advanceTimersByTime(3200);

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

      jest.advanceTimersByTime(1000);

      fireEvent.click(screen.getByText('Trigger'));
      const toasts = screen.getAllByText('Message');
      expect(toasts).toHaveLength(2);

      jest.advanceTimersByTime(2200);
      expect(screen.queryAllByText('Message')).toHaveLength(1);

      jest.advanceTimersByTime(1000);
      expect(screen.queryByText('Message')).not.toBeInTheDocument();
    });
  });

  describe('Toast Dismissal', () => {
    it('should dismiss toast when close button clicked', () => {
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

      expect(screen.queryByText('Success')).not.toBeInTheDocument();
    });

    it('should dismiss only clicked toast', () => {
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

      expect(screen.queryByText('Success 1')).not.toBeInTheDocument();
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

      const dot = document.querySelector('.toast-dot');
      expect(dot).toHaveAttribute('aria-hidden', 'true');
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
});
