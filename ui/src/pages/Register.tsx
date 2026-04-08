import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { setAuth } from '../auth';
import { apiRequest } from '../api/apiClient';
import { API, ROUTES } from '../config/constants';

const Register: React.FC = () => {
  const [formData, setFormData] = React.useState({
    email: '',
    password: '',
    firstName: '',
    lastName: '',
    clientType: 'Individual' as 'Individual' | 'Corporate',
    companyName: '',
    registrationNumber: ''
  });
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const navigate = useNavigate();

  const isBusiness = formData.clientType === 'Corporate';

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const res = await apiRequest(API.REGISTER, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(formData),
        skipAuth: true,
      } as any);

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error((data as any)?.message || 'Registration failed');
      }

      const data = await res.json();
      const token = (data as any)?.token as string | undefined;
      const id = (data as any)?.id as string | undefined;
      if (token) {
        setAuth(token, id);
        navigate(ROUTES.ACCOUNTS, { replace: true });
      } else {
        throw new Error('Invalid response from server');
      }
    } catch (err: any) {
      setError(err.message || 'Registration failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-split-layout">
      <div className="auth-split-brand">
        <div className="auth-split-brand__inner">
          <div className="auth-split-brand__logo">
            <img src="/money-security.svg" alt="FinTech logo" width="32" height="32" />
            <span className="auth-split-brand__wordmark">Fintech Application</span>
          </div>
          <h1 className="auth-split-brand__heading">Every account. Every chain.</h1>
        </div>
      </div>
      <div className="auth-split-form">
        <div className="auth-split-form__inner">
          <div className="card auth-card">
            <h2 style={{ textAlign: 'center', marginBottom: '1.5rem', color: '#4b5563' }}>Create your account</h2>
            <div className="alert alert-info" style={{ textAlign: 'center', padding: '20px' }}>
              <strong>Registration Disabled</strong>
              <p style={{ marginTop: '10px' }}>New account registration is currently disabled for security reasons.</p>
            </div>
            <form onSubmit={handleSubmit} style={{ opacity: 0.5, pointerEvents: 'none' }}>
              <div className="form-group">
                <label htmlFor="clientType">Account Type</label>
                <select
                  id="clientType"
                  name="clientType"
                  value={formData.clientType}
                  onChange={handleChange}
                  aria-label="Account type"
                >
                  <option value="Individual">Individual</option>
                  <option value="Corporate">Business</option>
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="firstName">First Name</label>
                <input
                  type="text"
                  id="firstName"
                  name="firstName"
                  value={formData.firstName}
                  onChange={handleChange}
                  required
                  placeholder="John"
                />
              </div>
              <div className="form-group">
                <label htmlFor="lastName">Last Name</label>
                <input
                  type="text"
                  id="lastName"
                  name="lastName"
                  value={formData.lastName}
                  onChange={handleChange}
                  required
                  placeholder="Doe"
                />
              </div>
              {isBusiness && (
                <>
                  <div className="form-group">
                    <label htmlFor="companyName">Company Name</label>
                    <input
                      type="text"
                      id="companyName"
                      name="companyName"
                      value={formData.companyName}
                      onChange={handleChange}
                      required={isBusiness}
                      placeholder="Acme Corp"
                    />
                  </div>
                  <div className="form-group">
                    <label htmlFor="registrationNumber">Registration Number</label>
                    <input
                      type="text"
                      id="registrationNumber"
                      name="registrationNumber"
                      value={formData.registrationNumber}
                      onChange={handleChange}
                      required={isBusiness}
                      placeholder="NZ1234567"
                    />
                  </div>
                </>
              )}
              <div className="form-group">
                <label htmlFor="email">Email address</label>
                <input
                  type="text"
                  id="email"
                  name="email"
                  value={formData.email}
                  onChange={handleChange}
                  required
                  placeholder="you@example.com"
                />
              </div>
              <div className="form-group">
                <label htmlFor="password">Password</label>
                <input
                  type="password"
                  id="password"
                  name="password"
                  value={formData.password}
                  onChange={handleChange}
                  required
                  placeholder="••••••••"
                />
              </div>
              {error && <div className="alert alert-error">{error}</div>}
              <button type="submit" className="btn btn-primary btn-block" disabled={true} style={{ marginTop: '1rem' }}>
                Register Disabled
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Register;