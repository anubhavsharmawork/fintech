import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import CryptoModeBanner from './CryptoModeBanner';

describe('CryptoModeBanner Component', () => {
  describe('Visibility', () => {
    it('should render when enabled is true', () => {
      render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should not render when enabled is false', () => {
      const { container } = render(<CryptoModeBanner enabled={false} />);

      expect(container.firstChild).toBeNull();
    });

    it('should render and unrender when enabled changes', () => {
      const { rerender, container } = render(<CryptoModeBanner enabled={false} />);

      expect(container.firstChild).toBeNull();

      rerender(<CryptoModeBanner enabled={true} />);

      expect(screen.getByRole('region')).toBeInTheDocument();
    });
  });

  describe('Content Display', () => {
    it('should display F-Mode enabled message', () => {
      render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByText(/F-Mode enabled:/)).toBeInTheDocument();
    });

    it('should display description text', () => {
      render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByText(/showing crypto accounts and features/)).toBeInTheDocument();
    });

    it('should display full message together', () => {
      render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByText(/F-Mode enabled: showing crypto accounts and features/)).toBeInTheDocument();
    });

    it('should have strong tag around F-Mode enabled', () => {
      render(<CryptoModeBanner enabled={true} />);

      const strong = screen.getByText('F-Mode enabled:');
      expect(strong.tagName).toBe('STRONG');
    });
  });

  describe('Dismiss Button', () => {
    it('should display dismiss button', () => {
      render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByText('Dismiss')).toBeInTheDocument();
    });

    it('should have type button on dismiss button', () => {
      render(<CryptoModeBanner enabled={true} />);

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('type', 'button');
    });

    it('should have btn and btn-secondary classes', () => {
      render(<CryptoModeBanner enabled={true} />);

      const button = screen.getByRole('button');
      expect(button).toHaveClass('btn', 'btn-secondary');
    });

    it('should have aria-label on dismiss button', () => {
      render(<CryptoModeBanner enabled={true} />);

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-label', 'Dismiss F-Mode notification');
    });
  });

  describe('Dismiss Callback', () => {
    it('should call onDismiss when button clicked', () => {
      const onDismiss = jest.fn();
      render(<CryptoModeBanner enabled={true} onDismiss={onDismiss} />);

      fireEvent.click(screen.getByText('Dismiss'));

      expect(onDismiss).toHaveBeenCalled();
    });

    it('should call onDismiss only once on single click', () => {
      const onDismiss = jest.fn();
      render(<CryptoModeBanner enabled={true} onDismiss={onDismiss} />);

      fireEvent.click(screen.getByText('Dismiss'));

      expect(onDismiss).toHaveBeenCalledTimes(1);
    });

    it('should work without onDismiss callback', () => {
      expect(() => {
        render(<CryptoModeBanner enabled={true} />);
        fireEvent.click(screen.getByText('Dismiss'));
      }).not.toThrow();
    });

    it('should call onDismiss on Enter key', () => {
      const onDismiss = jest.fn();
      render(<CryptoModeBanner enabled={true} onDismiss={onDismiss} />);

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: 'Enter' });

      expect(onDismiss).toHaveBeenCalled();
    });

    it('should call onDismiss on Space key', () => {
      const onDismiss = jest.fn();
      render(<CryptoModeBanner enabled={true} onDismiss={onDismiss} />);

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: ' ' });

      expect(onDismiss).toHaveBeenCalled();
    });

    it('should not call onDismiss on other keys', () => {
      const onDismiss = jest.fn();
      render(<CryptoModeBanner enabled={true} onDismiss={onDismiss} />);

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: 'a' });
      fireEvent.keyDown(button, { key: 'Escape' });
      fireEvent.keyDown(button, { key: 'Tab' });

      expect(onDismiss).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('should have region role', () => {
      render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should have aria-live polite', () => {
      render(<CryptoModeBanner enabled={true} />);

      const region = screen.getByRole('region');
      expect(region).toHaveAttribute('aria-live', 'polite');
    });

    it('should have aria-label for notification', () => {
      render(<CryptoModeBanner enabled={true} />);

      const region = screen.getByRole('region');
      expect(region).toHaveAttribute('aria-label', 'F-Mode notification');
    });

    it('should announce content to screen readers', () => {
      render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByText(/F-Mode enabled:/)).toBeInTheDocument();
      expect(screen.getByText(/showing crypto accounts and features/)).toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('should have teal background color', () => {
      render(<CryptoModeBanner enabled={true} />);

      const region = screen.getByRole('region');
      expect(region).toHaveStyle({ background: '#14b8a6' });
    });

    it('should have white text color', () => {
      render(<CryptoModeBanner enabled={true} />);

      const region = screen.getByRole('region');
      expect(region).toHaveStyle({ color: '#fff' });
    });

    it('should have padding', () => {
      render(<CryptoModeBanner enabled={true} />);

      const region = screen.getByRole('region');
      expect(region).toHaveStyle({ padding: '10px 16px' });
    });

    it('should have border radius', () => {
      render(<CryptoModeBanner enabled={true} />);

      const region = screen.getByRole('region');
      expect(region).toHaveStyle({ borderRadius: '8px' });
    });

    it('should have margin bottom', () => {
      render(<CryptoModeBanner enabled={true} />);

      const region = screen.getByRole('region');
      expect(region).toHaveStyle({ marginBottom: '12px' });
    });

    it('should have flex layout', () => {
      render(<CryptoModeBanner enabled={true} />);

      const region = screen.getByRole('region');
      expect(region).toHaveStyle({
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
      });
    });
  });

  describe('Props Combinations', () => {
    it('should render with enabled true and callback', () => {
      const onDismiss = jest.fn();
      render(<CryptoModeBanner enabled={true} onDismiss={onDismiss} />);

      expect(screen.getByRole('region')).toBeInTheDocument();
      expect(screen.getByText('Dismiss')).toBeInTheDocument();
    });

    it('should not render with enabled false and callback', () => {
      const onDismiss = jest.fn();
      const { container } = render(
        <CryptoModeBanner enabled={false} onDismiss={onDismiss} />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should render with only enabled prop', () => {
      render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should handle optional onDismiss', () => {
      render(<CryptoModeBanner enabled={true} onDismiss={undefined} />);

      expect(screen.getByRole('region')).toBeInTheDocument();
      expect(screen.getByText('Dismiss')).toBeInTheDocument();
    });
  });

  describe('Interaction', () => {
    it('should be interactive when enabled', () => {
      render(<CryptoModeBanner enabled={true} />);

      const button = screen.getByRole('button');
      expect(button).not.toBeDisabled();
    });

    it('should handle multiple dismiss attempts', () => {
      const onDismiss = jest.fn();
      const { rerender } = render(
        <CryptoModeBanner enabled={true} onDismiss={onDismiss} />
      );

      fireEvent.click(screen.getByText('Dismiss'));
      expect(onDismiss).toHaveBeenCalledTimes(1);

      // Re-enable and try again
      onDismiss.mockClear();
      rerender(<CryptoModeBanner enabled={true} onDismiss={onDismiss} />);

      fireEvent.click(screen.getByText('Dismiss'));
      expect(onDismiss).toHaveBeenCalledTimes(1);
    });

    it('should handle rapid clicks', () => {
      const onDismiss = jest.fn();
      render(<CryptoModeBanner enabled={true} onDismiss={onDismiss} />);

      const button = screen.getByRole('button');
      fireEvent.click(button);
      fireEvent.click(button);
      fireEvent.click(button);

      expect(onDismiss).toHaveBeenCalledTimes(3);
    });
  });

  describe('Dynamic State Changes', () => {
    it('should show banner when enabled changes to true', () => {
      const { rerender } = render(<CryptoModeBanner enabled={false} />);

      expect(screen.queryByRole('region')).not.toBeInTheDocument();

      rerender(<CryptoModeBanner enabled={true} />);

      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should hide banner when enabled changes to false', () => {
      const { rerender } = render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByRole('region')).toBeInTheDocument();

      rerender(<CryptoModeBanner enabled={false} />);

      expect(screen.queryByRole('region')).not.toBeInTheDocument();
    });

    it('should update callback when onDismiss changes', () => {
      const onDismiss1 = jest.fn();
      const onDismiss2 = jest.fn();

      const { rerender } = render(
        <CryptoModeBanner enabled={true} onDismiss={onDismiss1} />
      );

      fireEvent.click(screen.getByText('Dismiss'));
      expect(onDismiss1).toHaveBeenCalled();
      expect(onDismiss2).not.toHaveBeenCalled();

      rerender(<CryptoModeBanner enabled={true} onDismiss={onDismiss2} />);

      fireEvent.click(screen.getByText('Dismiss'));
      expect(onDismiss2).toHaveBeenCalled();
    });
  });

  describe('Edge Cases', () => {
    it('should handle null onDismiss gracefully', () => {
      const { container } = render(
        <CryptoModeBanner enabled={true} onDismiss={null as any} />
      );

      expect(() => {
        fireEvent.click(screen.getByText('Dismiss'));
      }).not.toThrow();
    });

    it('should render with enabled changing multiple times', () => {
      const { rerender } = render(<CryptoModeBanner enabled={true} />);

      expect(screen.getByRole('region')).toBeInTheDocument();

      rerender(<CryptoModeBanner enabled={false} />);
      expect(screen.queryByRole('region')).not.toBeInTheDocument();

      rerender(<CryptoModeBanner enabled={true} />);
      expect(screen.getByRole('region')).toBeInTheDocument();

      rerender(<CryptoModeBanner enabled={false} />);
      expect(screen.queryByRole('region')).not.toBeInTheDocument();
    });
  });
});
