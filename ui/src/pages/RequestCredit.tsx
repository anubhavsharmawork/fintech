import * as React from 'react';
import { CheckCircle, Briefcase, Info } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { useToast } from '../components/Toast';
import { authFetch } from '../auth';

interface CreditRequestForm {
  amount: string;
  currency: 'NZD' | 'FTK';
  purpose: string;
  notes: string;
}

const INITIAL_FORM: CreditRequestForm = {
  amount: '',
  currency: 'NZD',
  purpose: '',
  notes: '',
};

const RequestCredit: React.FC = () => {
  const [searchParams] = useSearchParams();
  const { success, error: toastError } = useToast();

  const projectId = searchParams.get('project_id');
  const projectName = searchParams.get('project_name');

  const [form, setForm] = React.useState<CreditRequestForm>(INITIAL_FORM);
  const [busySubmit, setBusySubmit] = React.useState(false);
  const [submitted, setSubmitted] = React.useState(false);

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) => {
    const { name, value } = e.target as HTMLInputElement;
    setForm(prev => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const amount = parseFloat(form.amount);
    if (Number.isNaN(amount) || amount <= 0) {
      toastError('Please enter a valid amount greater than 0.');
      return;
    }
    if (!form.purpose.trim()) {
      toastError('Purpose is required.');
      return;
    }

    setBusySubmit(true);
    try {
      const res = await authFetch('/credit-requests', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          projectId: projectId ?? null,
          projectName: projectName ?? null,
          amount,
          currency: form.currency,
          purpose: form.purpose.trim(),
          notes: form.notes.trim(),
        }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error((data as any)?.message || 'Failed to submit credit request.');
      }

      success('Credit request submitted');
      setSubmitted(true);
    } catch (err: any) {
      toastError(err?.message || 'Failed to submit credit request.');
    } finally {
      setBusySubmit(false);
    }
  };

  const handleReset = () => {
    setForm(INITIAL_FORM);
    setSubmitted(false);
  };

  if (submitted) {
    return (
      <div className="card" style={{ maxWidth: 560, margin: '2rem auto', padding: '2rem', textAlign: 'center' }}>
        <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '0.75rem', color: '#047857' }}><CheckCircle size={48} color="currentColor" /></div>
        <h3 style={{ marginTop: 0, marginBottom: '0.75rem' }}>Request Submitted</h3>
        <p style={{ color: 'var(--muted, #6b7280)', marginBottom: '1.5rem' }}>
          Your credit request has been submitted. You can close this tab or return to your project.
        </p>
        <button type="button" className="btn btn-secondary" onClick={handleReset}>
          Submit another request
        </button>
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 560, margin: '2rem auto' }}>
      <h2 style={{ marginBottom: '1.5rem' }}>Request Credit</h2>

      <div className="card" style={{ padding: '1.5rem' }}>
        {projectId ? (
          <div style={{
            background: 'var(--primary-bg, #eff6ff)',
            border: '1px solid var(--border, #e5e7eb)',
            borderRadius: 8,
            padding: '0.75rem 1rem',
            marginBottom: '1.25rem',
            fontSize: '0.9rem',
          }}>
            <Briefcase size={18} color="currentColor" style={{ verticalAlign: 'middle', marginRight: 6 }} /> Credit request for project: <strong>{projectName ?? projectId}</strong>
          </div>
        ) : (
          <div style={{
            background: '#fefce8',
            border: '1px solid #fde68a',
            borderRadius: 8,
            padding: '0.75rem 1rem',
            marginBottom: '1.25rem',
            fontSize: '0.9rem',
            color: '#92400e',
          }}>
            <Info size={18} color="currentColor" style={{ verticalAlign: 'middle', marginRight: 6 }} /> No project linked — submitting a general credit request
          </div>
        )}

        <form onSubmit={handleSubmit}>
          {projectId && (
            <input type="hidden" name="projectId" value={projectId} />
          )}

          <div className="form-group">
            <label htmlFor="amount">Requested Amount</label>
            <input
              id="amount"
              name="amount"
              type="number"
              min="1"
              step="0.01"
              value={form.amount}
              onChange={handleChange}
              required
              placeholder="e.g. 5000"
            />
          </div>

          <div className="form-group">
            <label htmlFor="currency">Currency</label>
            <select
              id="currency"
              name="currency"
              value={form.currency}
              onChange={handleChange}
            >
              <option value="NZD">NZD</option>
              <option value="FTK">FTK</option>
            </select>
          </div>

          <div className="form-group">
            <label htmlFor="purpose">Purpose</label>
            <input
              id="purpose"
              name="purpose"
              type="text"
              value={form.purpose}
              onChange={handleChange}
              required
              placeholder="e.g. Equipment purchase"
            />
          </div>

          <div className="form-group">
            <label htmlFor="notes">Notes <span style={{ color: 'var(--muted, #6b7280)', fontWeight: 400 }}>(optional)</span></label>
            <textarea
              id="notes"
              name="notes"
              value={form.notes}
              onChange={handleChange}
              maxLength={500}
              rows={4}
              placeholder="Any additional context for this request"
              style={{ width: '100%', resize: 'vertical' }}
            />
            <small style={{ color: 'var(--muted, #6b7280)' }}>
              {form.notes.length}/500
            </small>
          </div>

          <button
            type="submit"
            className="btn btn-primary"
            disabled={busySubmit}
            style={{ marginTop: '0.5rem' }}
          >
            {busySubmit ? (
              <><span className="spinner" /> Submitting…</>
            ) : (
              'Submit Request'
            )}
          </button>
        </form>
      </div>
    </div>
  );
};

export default RequestCredit;
