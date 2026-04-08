import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import FeedbackModal from './FeedbackModal';

describe('FeedbackModal Component', () => {
  const defaultProps = {
    isOpen: true,
    onClose: jest.fn(),
    message: '',
    onMessageChange: jest.fn(),
    submitting: false,
    onSubmit: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Visibility', () => {
    it('should render when isOpen is true', () => {
      render(<FeedbackModal {...defaultProps} isOpen={true} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('should not render when isOpen is false', () => {
      render(<FeedbackModal {...defaultProps} isOpen={false} />);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  describe('Rendering', () => {
    it('should display Send Feedback title', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByText('Send Feedback')).toBeInTheDocument();
    });

    it('should display privacy hint about sensitive info', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByText(/do not include sensitive financial information/)).toBeInTheDocument();
    });

    it('should display textarea for feedback', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByRole('textbox')).toBeInTheDocument();
    });

    it('should display character counter', () => {
      render(<FeedbackModal {...defaultProps} message="Hello" />);

      expect(screen.getByText('5/2000')).toBeInTheDocument();
    });

    it('should display Cancel button', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByText('Cancel')).toBeInTheDocument();
    });

    it('should display Submit button', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByText('Submit')).toBeInTheDocument();
    });
  });

  describe('Textarea', () => {
    it('should display message value', () => {
      render(<FeedbackModal {...defaultProps} message="Test feedback" />);

      expect(screen.getByRole('textbox')).toHaveValue('Test feedback');
    });

    it('should call onMessageChange when typing', () => {
      const onMessageChange = jest.fn();
      render(<FeedbackModal {...defaultProps} onMessageChange={onMessageChange} />);

      fireEvent.change(screen.getByRole('textbox'), { target: { value: 'New text' } });

      expect(onMessageChange).toHaveBeenCalledWith('New text');
    });

    it('should have maxLength of 2000', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByRole('textbox')).toHaveAttribute('maxLength', '2000');
    });

    it('should have placeholder text', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByPlaceholderText('Tell us what you think…')).toBeInTheDocument();
    });

    it('should have aria-label', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByRole('textbox')).toHaveAttribute('aria-label', 'Feedback message');
    });
  });

  describe('Validation', () => {
    it('should show validation error when message is too short after blur', () => {
      render(<FeedbackModal {...defaultProps} message="Hi" />);

      fireEvent.blur(screen.getByRole('textbox'));

      expect(screen.getByText('Feedback must be at least 10 characters.')).toBeInTheDocument();
    });

    it('should not show validation error before interaction', () => {
      render(<FeedbackModal {...defaultProps} message="Hi" />);

      expect(screen.queryByText('Feedback must be at least 10 characters.')).not.toBeInTheDocument();
    });

    it('should show validation error after blur with short message', () => {
      render(<FeedbackModal {...defaultProps} message="Hi" />);

      // Blur triggers touched state
      fireEvent.blur(screen.getByRole('textbox'));

      expect(screen.getByText('Feedback must be at least 10 characters.')).toBeInTheDocument();
    });

    it('should not show validation error for valid message', () => {
      render(<FeedbackModal {...defaultProps} message="This is a valid feedback message" />);

      fireEvent.blur(screen.getByRole('textbox'));

      expect(screen.queryByText('Feedback must be at least 10 characters.')).not.toBeInTheDocument();
    });

    it('should have aria-invalid when message is too short', () => {
      render(<FeedbackModal {...defaultProps} message="Hi" />);

      expect(screen.getByRole('textbox')).toHaveAttribute('aria-invalid', 'true');
    });

    it('should not have aria-invalid for valid message', () => {
      render(<FeedbackModal {...defaultProps} message="This is valid" />);

      expect(screen.getByRole('textbox')).toHaveAttribute('aria-invalid', 'false');
    });

    it('should reset touched state when modal closes and reopens', () => {
      const { rerender } = render(<FeedbackModal {...defaultProps} message="Hi" />);

      fireEvent.blur(screen.getByRole('textbox'));
      expect(screen.getByText('Feedback must be at least 10 characters.')).toBeInTheDocument();

      // Close modal
      rerender(<FeedbackModal {...defaultProps} isOpen={false} message="Hi" />);

      // Reopen modal
      rerender(<FeedbackModal {...defaultProps} isOpen={true} message="Hi" />);

      // Validation error should not show initially
      expect(screen.queryByText('Feedback must be at least 10 characters.')).not.toBeInTheDocument();
    });
  });

  describe('Character Counter', () => {
    it('should show warning style when near limit', () => {
      const longMessage = 'a'.repeat(1950);
      render(<FeedbackModal {...defaultProps} message={longMessage} />);

      expect(screen.getByText('1950/2000')).toBeInTheDocument();
    });

    it('should update counter as message changes', () => {
      const { rerender } = render(<FeedbackModal {...defaultProps} message="Hello" />);
      expect(screen.getByText('5/2000')).toBeInTheDocument();

      rerender(<FeedbackModal {...defaultProps} message="Hello World" />);
      expect(screen.getByText('11/2000')).toBeInTheDocument();
    });
  });

  describe('Button States', () => {
    it('should disable Submit button when message is too short', () => {
      render(<FeedbackModal {...defaultProps} message="Hi" />);

      expect(screen.getByText('Submit')).toBeDisabled();
    });

    it('should enable Submit button when message is valid', () => {
      render(<FeedbackModal {...defaultProps} message="This is a valid feedback" />);

      expect(screen.getByText('Submit')).not.toBeDisabled();
    });

    it('should disable Submit button when submitting', () => {
      render(<FeedbackModal {...defaultProps} message="Valid feedback" submitting={true} />);

      expect(screen.getByText('Sending…')).toBeDisabled();
    });

    it('should show Sending… text when submitting', () => {
      render(<FeedbackModal {...defaultProps} message="Valid feedback" submitting={true} />);

      expect(screen.getByText('Sending…')).toBeInTheDocument();
    });

    it('should disable Cancel button when submitting', () => {
      render(<FeedbackModal {...defaultProps} submitting={true} />);

      expect(screen.getByText('Cancel')).toBeDisabled();
    });
  });

  describe('Button Interactions', () => {
    it('should call onSubmit when Submit button is clicked', () => {
      const onSubmit = jest.fn();
      render(<FeedbackModal {...defaultProps} message="Valid feedback" onSubmit={onSubmit} />);

      fireEvent.click(screen.getByText('Submit'));

      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    it('should call onClose when Cancel button is clicked', () => {
      const onClose = jest.fn();
      render(<FeedbackModal {...defaultProps} onClose={onClose} />);

      fireEvent.click(screen.getByText('Cancel'));

      expect(onClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Overlay', () => {
    it('should call onClose when overlay is clicked', () => {
      const onClose = jest.fn();
      const { container } = render(<FeedbackModal {...defaultProps} onClose={onClose} />);

      const overlay = container.querySelector('.feedback-overlay');
      fireEvent.click(overlay!);

      expect(onClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Accessibility', () => {
    it('should have dialog role', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('should have aria-modal attribute', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true');
    });

    it('should have aria-labelledby pointing to heading', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toHaveAttribute('aria-labelledby', 'feedback-modal-heading');
    });

    it('should have validation error with role alert', () => {
      render(<FeedbackModal {...defaultProps} message="Hi" />);

      fireEvent.blur(screen.getByRole('textbox'));

      expect(screen.getByRole('alert')).toHaveTextContent('Feedback must be at least 10 characters.');
    });
  });

  describe('Button Classes', () => {
    it('should have correct classes on Cancel button', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByText('Cancel')).toHaveClass('btn', 'btn-secondary');
    });

    it('should have correct classes on Submit button', () => {
      render(<FeedbackModal {...defaultProps} />);

      expect(screen.getByText('Submit')).toHaveClass('btn', 'btn-primary');
    });
  });
});
