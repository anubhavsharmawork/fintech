// @ts-nocheck
import * as React from 'react';

interface FeedbackModalProps {
  isOpen: boolean;
  onClose: () => void;
  token?: string | null;
  message: string;
  onMessageChange: (value: string) => void;
  submitting: boolean;
  onSubmit: () => void;
}

const FeedbackModal: React.FC<FeedbackModalProps> = ({
  isOpen,
  onClose,
  message,
  onMessageChange,
  submitting,
  onSubmit,
}) => {
  const [touched, setTouched] = React.useState(false);

  React.useEffect(() => {
    if (!isOpen) setTouched(false);
  }, [isOpen]);

  if (!isOpen) return null;

  const tooShort = message.length > 0 && message.length < 10;

  return (
    <>
      <div className="feedback-overlay" onClick={onClose} aria-hidden="true" />
      <div
        className="feedback-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="feedback-modal-heading"
      >
        <h2 id="feedback-modal-heading">Send Feedback</h2>
        <p className="feedback-hint">
          Please do not include sensitive financial information such as account numbers, card numbers, or passwords.
        </p>
        <textarea
          className="feedback-textarea"
          value={message}
          onChange={(e) => { onMessageChange(e.target.value); if (!touched) setTouched(true); }}
          onBlur={() => setTouched(true)}
          maxLength={2000}
          rows={5}
          placeholder="Tell us what you think…"
          aria-label="Feedback message"
          aria-invalid={tooShort}
        />
        <div className="feedback-meta">
          <span className={`feedback-counter${message.length > 1900 ? ' feedback-counter--warn' : ''}`}>
            {message.length}/2000
          </span>
        </div>
        {touched && tooShort && (
          <p className="feedback-validation" role="alert">
            Feedback must be at least 10 characters.
          </p>
        )}
        <div className="feedback-actions">
          <button
            type="button"
            className="btn btn-secondary feedback-cancel"
            onClick={onClose}
            disabled={submitting}
          >
            Cancel
          </button>
          <button
            type="button"
            className="btn btn-primary feedback-submit"
            onClick={onSubmit}
            disabled={message.trim().length < 10 || submitting}
          >
            {submitting ? 'Sending\u2026' : 'Submit'}
          </button>
        </div>
      </div>
    </>
  );
};

export default FeedbackModal;
