// @ts-nocheck
import * as React from 'react';
import * as Router from 'react-router-dom';
import { clearAuth, onAuthChange } from '../auth';
import { useFMode } from '../hooks/useFMode';
import { useToast } from './Toast';

interface LayoutProps {
 children: React.ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
 const navigate = Router.useNavigate();
 const { info } = useToast();
 const [token, setToken] = React.useState(typeof window !== 'undefined' ? localStorage.getItem('token') : null);
 const [progress, setProgress] = React.useState(0);
 const { enabled: fModeEnabled, toggle: toggleFMode } = useFMode();
 const skipLinkRef = React.useRef<HTMLAnchorElement>(null);

 const handleToggle = (value: boolean) => {
 if (fModeEnabled === value) return;
 toggleFMode(value);
 info(value ? 'Switched to F-Mode (DeFi Enabled)' : 'Switched to Fiat Mode');
 };

 React.useEffect(() => {
 const onScroll = () => {
 const h = document.documentElement;
 const scrolled = (h.scrollTop) / ((h.scrollHeight - h.clientHeight) ||1);
 setProgress(Math.max(0, Math.min(1, scrolled)) *100);
 };
 onScroll();
 window.addEventListener('scroll', onScroll, { passive: true });

 const off = onAuthChange((t) => setToken(t));

 const onStorage = (e: StorageEvent) => {
 if (e.key === 'token') setToken(localStorage.getItem('token'));
 };
 window.addEventListener('storage', onStorage);

 return () => {
 window.removeEventListener('scroll', onScroll as any);
 window.removeEventListener('storage', onStorage);
 off?.();
 };
 }, []);

 const logout = async () => {
 try { await fetch('/users/logout', { method: 'POST', credentials: 'include' }); } catch {}
 clearAuth();
 navigate('/login', { replace: true });
 };

 const year = new Date().getFullYear();

 return (
 <div>
    <a href="#main-content" className="skip-link" ref={skipLinkRef}>
      Skip to main content
    </a>
 <div className="progress-wrap" aria-hidden="true"><div className="progress-bar" style={{ width: `${progress}%` }} /></div>
  <header className="header" role="banner">
  <div className="container" style={{ padding: '0 24px' }}>
  {/* Logo and Title - First Row */}
  <div style={{ display:'flex', flexDirection:'column', marginBottom: '0.75rem' }}>
  <Router.Link to="/" aria-label="Home" style={{ display:'inline-flex', alignItems:'center', gap:10, color:'inherit', textDecoration:'none' }}>
  <img src="/money-security.svg" alt="FinTech logo" style={{ height:28, width: 'auto' }} />
  <h1 style={{ margin:0, fontSize: '1.25rem' }}>FinTech Application</h1>
  </Router.Link>
  <span className="hero-badge" style={{ marginTop: 2, padding: '2px 8px', fontSize: '0.7rem', alignSelf: 'flex-start' }} aria-label="Unique value proposition">Simple, fast and secure finance. Own your money flow.</span>
  </div>

  {/* Navigation and Toggle - Second Row */}
  <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', gap:12, flexWrap:'wrap' }}>
 <nav className="nav" role="navigation" aria-label="Main" style={{ display:'flex', alignItems:'center', gap:12, flexWrap:'wrap' }}>
 {token && <Router.Link to="/" aria-label="Dashboard">Dashboard</Router.Link>}
 {token && (
 <>
 {!fModeEnabled && <Router.Link to="/accounts" aria-label="Accounts">Accounts</Router.Link>}
 <Router.Link to="/transactions" aria-label="Transactions">Transactions</Router.Link>
 {!fModeEnabled && <Router.Link to="/budget" aria-label="Budget">Budget</Router.Link>}
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
      {token && (
        <div className="segmented-control" role="group" aria-label="Mode selection">
          <div 
            className={`selection-pill ${fModeEnabled ? 'f-mode-active' : ''}`} 
            style={{ 
              transform: fModeEnabled ? 'translateX(100%)' : 'translateX(0)',
              background: fModeEnabled ? 'var(--accent)' : 'var(--primary)'
            }} 
            aria-hidden="true"
          />
          <button 
            className={`option ${!fModeEnabled ? 'active' : ''}`}
            onClick={(e) => { e.stopPropagation(); handleToggle(false); }}
            aria-pressed={!fModeEnabled}
            aria-label="Switch to Fiat Mode"
            type="button"
          >
            💰 Fiat
          </button>
          <button 
            className={`option ${fModeEnabled ? 'active' : ''}`}
            onClick={(e) => { e.stopPropagation(); handleToggle(true); }}
            aria-pressed={fModeEnabled}
            aria-label="Switch to F-Mode (DeFi)"
            type="button"
          >
            🪙 F-Mode
          </button>
        </div>
      )}
 </div>
    </div>
  </header>
 <main className="container" role="main" id="main-content">
 {children}
 <div className="spacer" />
 <footer className="social" aria-label="Social Links">
 <a href="https://www.linkedin.com/in/anubhav-sharma-/" target="_blank" rel="noreferrer" aria-label="LinkedIn" className="linkedin-icon">
 <span aria-hidden="true">in</span>
 </a>
 <span style={{ 
   display: 'inline-flex', 
   alignItems: 'center', 
   gap: 6, 
   padding: '4px 12px', 
   background: 'linear-gradient(135deg, var(--primary), var(--accent))',
   borderRadius: 4,
   fontSize: '0.7rem',
   fontWeight: 600,
   letterSpacing: '0.1em',
   color: 'white',
   textTransform: 'uppercase'
 }}>
    Open Finance
 </span>
 <Router.Link to="/privacy" className="privacy-link">
   Privacy
 </Router.Link>
 <span className="small" style={{ marginLeft: 'auto', marginRight: 'auto', textAlign: 'center', fontStyle: 'italic' }}>
          Sandbox Environment
 </span>
 <span className="small" style={{ marginLeft: 'auto' }}>&copy; {year} Anubhav Sharma. All rights reserved.</span>
 </footer>
 </main>
 </div>
 );
};

export default Layout;