// @ts-nocheck
import * as React from 'react';
import * as Router from 'react-router-dom';

interface LayoutProps {
  children: React.ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
  const navigate = Router.useNavigate();
  const token = typeof window !== 'undefined' ? localStorage.getItem('token') : null;
  const [progress, setProgress] = React.useState(0);

  React.useEffect(() => {
    const onScroll = () => {
      const h = document.documentElement;
      const scrolled = (h.scrollTop) / ((h.scrollHeight - h.clientHeight) || 1);
      setProgress(Math.max(0, Math.min(1, scrolled)) * 100);
    };
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll as any);
  }, []);

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('userId');
    navigate('/login');
  };

  const year = new Date().getFullYear();

  return (
    <div>
      <div className="progress-wrap" aria-hidden="true"><div className="progress-bar" style={{ width: `${progress}%` }} /></div>
      <header className="header" role="banner">
        <div className="container">
          <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', gap:12 }}>
            <div style={{ display:'flex', flexDirection:'column' }}>
              <Router.Link to="/" aria-label="Home" style={{ display:'inline-flex', alignItems:'center', gap:10, color:'inherit', textDecoration:'none' }}>
                <img src="/money-security.svg" alt="FinTech logo" style={{ height: 36, width: 'auto' }} />
                <h1 style={{ margin:0 }}>FinTech Application</h1>
              </Router.Link>
              <span className="hero-badge" aria-label="Unique value proposition" style={{ marginTop: 6 }}>Simple, fast and secure personal finance. Own your money flow.</span>
            </div>
            <nav className="nav" role="navigation" aria-label="Main">
              {token && <Router.Link to="/" aria-label="Dashboard">Dashboard</Router.Link>}
              {token && (
                <>
                  <Router.Link to="/accounts" aria-label="Accounts">Accounts</Router.Link>
                  <Router.Link to="/transactions" aria-label="Transactions">Transactions</Router.Link>
                </>
              )}
              {!token ? (
                <>
                  <Router.Link to="/login" aria-label="Login">Login</Router.Link>
                  <Router.Link to="/register" aria-label="Register">Register</Router.Link>
                </>
              ) : (
                <button onClick={logout} className="btn btn-secondary" aria-label="Logout">Logout</button>
              )}
            </nav>
          </div>
        </div>
      </header>
      <main className="container" role="main">
        {children}
        <div className="spacer" />
        <footer className="social" aria-label="Social Links">
          <a href="https://www.linkedin.com/in/anubhav-sharma-/" target="_blank" rel="noreferrer" aria-label="LinkedIn">
            <span aria-hidden>in</span>
          </a>
          <span className="small" style={{ marginLeft: 'auto' }}>&copy; {year} Anubhav Sharma. All rights reserved.</span>
        </footer>
      </main>
    </div>
  );
};

export default Layout;