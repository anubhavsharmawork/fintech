import * as React from 'react';
import { useLocation } from 'react-router-dom';
import { User, Building2 } from 'lucide-react';
import { setAuth } from '../auth';
import { syncBrowserTimezone } from '../services/timezone';
import { apiRequest } from '../api/apiClient';
import { API, ROUTES } from '../config/constants';

const Login: React.FC = () => {
    const [email, setEmail] = React.useState('demo');
    const [password, setPassword] = React.useState('Demo@2026');
    const [loading, setLoading] = React.useState(false);
    const [error, setError] = React.useState<string | null>(null);
    const [activeDemo, setActiveDemo] = React.useState<'individual' | 'corporate'>('individual');
    const location = useLocation() as any;

    const demoCredentials = {
        individual: { email: 'demo', password: 'Demo@2026', label: 'Individual', icon: <User size={20} color="currentColor" /> },
        corporate: { email: 'corpadmindemo', password: 'Corp@2026', label: 'Corporate', icon: <Building2 size={20} color="currentColor" /> }
    };

    const selectDemo = (type: 'individual' | 'corporate') => {
        setActiveDemo(type);
        setEmail(demoCredentials[type].email);
        setPassword(demoCredentials[type].password);
        setError(null);
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setLoading(true);
        try {
            const res = await apiRequest(API.LOGIN, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password }),
                skipAuth: true,
            } as any);

            if (!res.ok) {
                const data = await res.json().catch(() => ({}));
                throw new Error((data as any)?.message || 'Login failed');
            }

            const data = await res.json();
            const token = (data as any)?.token as string | undefined;
            const userId = (data as any)?.userId as string | undefined;
            if (token) {
                setAuth(token, userId);
                // Fire-and-forget: sync browser timezone to server after successful login
                syncBrowserTimezone().catch(() => { });
                const redirectTo = location.state?.from?.pathname || ROUTES.ACCOUNTS;
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
                        <h2 style={{ textAlign: 'center', marginBottom: '1.5rem', color: '#4b5563' }}>Log in to your account</h2>

                        {/* Demo credential selector */}
                        <div style={{ display: 'flex', gap: 8, marginBottom: '1.25rem' }}>
                            {(Object.keys(demoCredentials) as Array<'individual' | 'corporate'>).map(type => (
                                <button
                                    key={type}
                                    type="button"
                                    onClick={() => selectDemo(type)}
                                    style={{
                                        flex: 1,
                                        padding: '10px 12px',
                                        border: activeDemo === type ? '2px solid var(--primary)' : '2px solid var(--border)',
                                        borderRadius: 8,
                                        background: activeDemo === type ? 'var(--primary-bg, #eff6ff)' : 'transparent',
                                        cursor: 'pointer',
                                        transition: 'all 0.15s ease',
                                        textAlign: 'center'
                                    }}
                                    aria-pressed={activeDemo === type}
                                >
                                    <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 2, color: 'currentColor' }}>{demoCredentials[type].icon}</div>
                                    <div style={{ fontSize: '0.8rem', fontWeight: 600, color: activeDemo === type ? 'var(--primary)' : '#6b7280' }}>
                                        {demoCredentials[type].label}
                                    </div>
                                </button>
                            ))}
                        </div>

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
                        <p className="small">Select a demo profile above to prefill credentials.</p>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Login;
