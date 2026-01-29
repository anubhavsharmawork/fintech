import * as React from 'react';
import { useNavigate } from 'react-router-dom';
import { setAuth } from '../auth';

const Register: React.FC = () => {
  const [formData, setFormData] = React.useState({
    email: '',
    password: '',
    firstName: '',
    lastName: ''
  });
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const navigate = useNavigate();

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
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
      const res = await fetch('/users/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(formData),
        credentials: 'include'
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error((data as any)?.message || 'Registration failed');
      }

      const data = await res.json();
      const token = (data as any)?.token as string | undefined;
      const id = (data as any)?.id as string | undefined;
      if (token) {
        setAuth(token, id);
        navigate('/accounts', { replace: true });
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
    <div className="auth-container">
      <div className="card auth-card">
        <h2 style={{ textAlign: 'center', marginBottom: '1.5rem', color: '#4b5563' }}>Create your account</h2>
        <div className="alert alert-info" style={{ textAlign: 'center', padding: '20px' }}>
          <strong>Registration Disabled</strong>
          <p style={{ marginTop: '10px' }}>New account registration is currently disabled for security reasons.</p>
        </div>
        <form onSubmit={handleSubmit} style={{ opacity: 0.5, pointerEvents: 'none' }}>
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
  );
};

export default Register;