import * as React from 'react';
import { useLocation } from 'react-router-dom';
import { setAuth } from '../auth';

const Login: React.FC = () => {
  const [email, setEmail] = React.useState('demo');
  const [password, setPassword] = React.useState('Demo@2026');
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const location = useLocation() as any;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const res = await fetch('/users/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
        credentials: 'include'
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error((data as any)?.message || 'Login failed');
      }

      const data = await res.json();
      const token = (data as any)?.token as string | undefined;
      const userId = (data as any)?.userId as string | undefined;
      if (token) {
        setAuth(token, userId);
        const redirectTo = location.state?.from?.pathname || '/accounts';
        // navigate(redirectTo, { replace: true });
        // Force reload to ensure auth state updates globally if needed, or just navigate
        window.location.href = redirectTo; 
      } else {
        throw new Error('Invalid response from server');
      }
    } catch (err: any) {
      setError(err.message || 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <div className="card auth-card">
        <h2 style={{ textAlign: 'center', marginBottom: '1.5rem', color: '#4b5563' }}>Log in to your account</h2>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="email">Email address</label>
            <input
              type="text"
              id="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="form-control"
              placeholder="Enter your email"
            />
          </div>
          <div className="form-group">
            <label htmlFor="password">Password</label>
            <input
              type="password"
              id="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              className="form-control"
              placeholder="Enter your password"
            />
          </div>
          {error && <div className="alert alert-error">{error}</div>}
          <button type="submit" className="btn btn-primary btn-block" disabled={loading} style={{ marginTop: '1rem' }}>
            {loading ? 'Logging in...' : 'Log In'}
          </button>
        </form>
      </div>
      <div style={{ textAlign: 'center', marginTop: '1.5rem' }}>
        <p className="small">Demo login is prefilled as demo/Demo@2026.</p>
      </div>
    </div>
  );
};

export default Login;