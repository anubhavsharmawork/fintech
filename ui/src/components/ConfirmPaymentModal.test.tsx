import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import ConfirmPaymentModal from './ConfirmPaymentModal';

describe('ConfirmPaymentModal Component', () => {
  const defaultProps = {
    amount: '100.00',
    payeeName: 'John Doe',
    onConfirm: jest.fn(),
    onCancel: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render modal with dialog role', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('should have aria-modal attribute', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true');
    });

    it('should display Confirm Payment title', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByText('Confirm Payment')).toBeInTheDocument();
    });

    it('should display formatted amount', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByText('$100.00')).toBeInTheDocument();
    });

    it('should display payee name', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByText('John Doe')).toBeInTheDocument();
    });

    it('should display Cancel button', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByText('Cancel')).toBeInTheDocument();
    });

    it('should display Confirm button', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByText('Confirm')).toBeInTheDocument();
    });
  });

  describe('Currency Formatting', () => {
    it('should format amount as NZD by default', () => {
      render(<ConfirmPaymentModal {...defaultProps} amount="1234.56" />);

      expect(screen.getByText('$1,234.56')).toBeInTheDocument();
    });

    it('should format amount with specified currency', () => {
      render(<ConfirmPaymentModal {...defaultProps} amount="100" currency="USD" />);

      expect(screen.getByText(/\$100/)).toBeInTheDocument();
    });

    it('should handle invalid amount gracefully', () => {
      render(<ConfirmPaymentModal {...defaultProps} amount="invalid" />);

      // Should display the raw amount if parsing fails
      expect(screen.getByText('invalid')).toBeInTheDocument();
    });

    it('should handle decimal amounts', () => {
      render(<ConfirmPaymentModal {...defaultProps} amount="99.99" />);

      expect(screen.getByText('$99.99')).toBeInTheDocument();
    });

    it('should handle large amounts', () => {
      render(<ConfirmPaymentModal {...defaultProps} amount="1000000" />);

      expect(screen.getByText('$1,000,000.00')).toBeInTheDocument();
    });
  });

  describe('Button Interactions', () => {
    it('should call onConfirm when Confirm button is clicked', () => {
      const onConfirm = jest.fn();
      render(<ConfirmPaymentModal {...defaultProps} onConfirm={onConfirm} />);

      fireEvent.click(screen.getByText('Confirm'));

      expect(onConfirm).toHaveBeenCalledTimes(1);
    });

    it('should call onCancel when Cancel button is clicked', () => {
      const onCancel = jest.fn();
      render(<ConfirmPaymentModal {...defaultProps} onCancel={onCancel} />);

      fireEvent.click(screen.getByText('Cancel'));

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('should have btn classes on buttons', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByText('Cancel')).toHaveClass('btn', 'btn-secondary');
      expect(screen.getByText('Confirm')).toHaveClass('btn', 'btn-primary');
    });

    it('should have type button on buttons', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByText('Cancel')).toHaveAttribute('type', 'button');
      expect(screen.getByText('Confirm')).toHaveAttribute('type', 'button');
    });

    it('should auto-focus the Confirm button', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      expect(screen.getByText('Confirm')).toHaveFocus();
    });
  });

  describe('Keyboard Interactions', () => {
    it('should call onCancel when Escape key is pressed', () => {
      const onCancel = jest.fn();
      render(<ConfirmPaymentModal {...defaultProps} onCancel={onCancel} />);

      fireEvent.keyDown(document, { key: 'Escape' });

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('should not call onCancel for other keys', () => {
      const onCancel = jest.fn();
      render(<ConfirmPaymentModal {...defaultProps} onCancel={onCancel} />);

      fireEvent.keyDown(document, { key: 'Enter' });
      fireEvent.keyDown(document, { key: 'Tab' });

      expect(onCancel).not.toHaveBeenCalled();
    });

    it('should clean up event listener on unmount', () => {
      const onCancel = jest.fn();
      const { unmount } = render(<ConfirmPaymentModal {...defaultProps} onCancel={onCancel} />);

      unmount();

      fireEvent.keyDown(document, { key: 'Escape' });

      expect(onCancel).not.toHaveBeenCalled();
    });
  });

  describe('Backdrop Click', () => {
    it('should call onCancel when backdrop is clicked', () => {
      const onCancel = jest.fn();
      render(<ConfirmPaymentModal {...defaultProps} onCancel={onCancel} />);

      // Click on the backdrop (the dialog element itself)
      fireEvent.click(screen.getByRole('dialog'));

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('should not call onCancel when modal content is clicked', () => {
      const onCancel = jest.fn();
      render(<ConfirmPaymentModal {...defaultProps} onCancel={onCancel} />);

      // Click on the title (inside the modal content)
      fireEvent.click(screen.getByText('Confirm Payment'));

      expect(onCancel).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('should have aria-labelledby pointing to title', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-labelledby', 'confirm-payment-title');
    });

    it('should have title with correct id', () => {
      render(<ConfirmPaymentModal {...defaultProps} />);

      const title = screen.getByText('Confirm Payment');
      expect(title).toHaveAttribute('id', 'confirm-payment-title');
    });
  });

  describe('Payment Message', () => {
    it('should display sending message with amount and payee', () => {
      render(<ConfirmPaymentModal {...defaultProps} amount="50" payeeName="Jane Smith" />);

      expect(screen.getByText(/You are sending/)).toBeInTheDocument();
      expect(screen.getByText('Jane Smith')).toBeInTheDocument();
    });
  });
});
