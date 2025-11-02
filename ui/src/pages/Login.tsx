import * as React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { setAuth } from '../auth';

const Login: React.FC = () => {
  const [email, setEmail] = React.useState('demo');
  const [password, setPassword] = React.useState('Demo@2026');
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const navigate = useNavigate();
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
        navigate(redirectTo, { replace: true });
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
    <div>
      <h2>Login</h2>
      <div className="card">
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="email">Username or Email:</label>
            <input
              type="text"
              id="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="password">Password:</label>
            <input
              type="password"
              id="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>
          {error && <p style={{ color: 'red' }}>{error}</p>}
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Logging in...' : 'Login'}
          </button>
          <p style={{ marginTop: '8px', color: '#666' }}>Demo login is prefilled as demo/Demo@2026.</p>
        </form>
      </div>
    </div>
  );
};

export default Login;