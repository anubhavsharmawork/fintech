// @ts-nocheck
import * as React from 'react';
import * as Router from 'react-router-dom';
import { clearAuth, onAuthChange, isCorporateUser, authFetch, getOrganisationRole, refreshAccessToken } from '../auth';
import { ROUTES } from '../config/constants';
import { useFMode } from '../hooks/useFMode';
import { useToast } from './Toast';
import FeedbackModal from './FeedbackModal';
import GlobalSearch from './GlobalSearch';
import { useGlobalSearch } from '../hooks/useGlobalSearch';
import { submitFeedback as submitFeedbackApi } from '../services/feedback';
import { useAppContext } from '../context/AppContext';
import { SessionExpiryBanner } from './NetworkStatus';
import PageLoader from './PageLoader';
import NotificationBell from './NotificationBell';

/* ─── Avatar Dropdown Menu ─── */
const AvatarDropdown: React.FC<{
  userEmail: string;
  userInitial: string;
  onLogout: () => void;
}> = ({ userEmail, userInitial, onLogout }) => {
  const [isOpen, setIsOpen] = React.useState(false);
  const dropdownRef = React.useRef<HTMLDivElement>(null);
  const buttonRef = React.useRef<HTMLButtonElement>(null);

  // Close on outside click
  React.useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen]);

  // Escape key support
  React.useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        setIsOpen(false);
        buttonRef.current?.focus();
      }
    };
    if (isOpen) {
      document.addEventListener('keydown', handleKeyDown);
    }
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen]);

  return (
    <div className="avatar-dropdown-container" ref={dropdownRef}>
      <button
        ref={buttonRef}
        type="button"
        className="topbar-avatar"
        onClick={() => setIsOpen(prev => !prev)}
        aria-expanded={isOpen}
        aria-haspopup="menu"
        aria-label="User menu"
        title={userEmail}
      >
        {userInitial}
      </button>
      <div
        className={`avatar-dropdown-menu${isOpen ? ' open' : ''}`}
        role="menu"
        aria-hidden={!isOpen}
      >
        <div className="avatar-dropdown-email" role="menuitem" aria-disabled="true">
          {userEmail}
        </div>
        <div className="avatar-dropdown-divider" role="separator" />
        <Router.Link
          to={ROUTES.SETTINGS}
          className="avatar-dropdown-item"
          role="menuitem"
          onClick={() => setIsOpen(false)}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="3"/>
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9c.26.46.4.98.4 1.51V12"/>
          </svg>
          Settings
        </Router.Link>
        <button
          type="button"
          className="avatar-dropdown-item avatar-dropdown-signout"
          role="menuitem"
          aria-label="Logout"
          onClick={() => {
            setIsOpen(false);
            onLogout();
          }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
            <polyline points="16 17 21 12 16 7"/>
            <line x1="21" y1="12" x2="9" y2="12"/>
          </svg>
          Sign Out
        </button>
      </div>
    </div>
  );
};

interface LayoutProps {
 children: React.ReactNode;
}

/* ─── Sidebar SVG Icons ─── */
const IconDashboard = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>
);
const IconAccounts = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="2" y="7" width="20" height="14" rx="2" ry="2"/><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"/></svg>
);
const IconTransactions = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="17 1 21 5 17 9"/><path d="M3 11V9a4 4 0 0 1 4-4h14"/><polyline points="7 23 3 19 7 15"/><path d="M21 13v2a4 4 0 0 1-4 4H3"/></svg>
);
const IconCards = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>
);
const IconBudget = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
);
const IconCorporate = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
);
const IconBatches = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/></svg>
);
const IconApprovals = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
);
const IconCompliance = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
);
const IconSanctions = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
);
const IconAdmin = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg>
);
const IconSettings = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>
);
const IconWhitepaper = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>
);

/* ─── Breadcrumb Route Map ─── */
const breadcrumbMap: Record<string, { group: string; label: string; groupPath: string }> = {
  '/': { group: 'Banking', label: 'Dashboard', groupPath: '/' },
  '/accounts': { group: 'Banking', label: 'Accounts', groupPath: '/' },
  '/transactions': { group: 'Banking', label: 'Transactions', groupPath: '/' },
  '/cards': { group: 'Banking', label: 'Cards', groupPath: '/' },
  '/budget': { group: 'Banking', label: 'Budget', groupPath: '/' },
  '/corporate/dashboard': { group: 'Corporate', label: 'Corporate Dashboard', groupPath: '/corporate/dashboard' },
  '/corporate/batches': { group: 'Corporate', label: 'Batches', groupPath: '/corporate/dashboard' },
  '/corporate/approvals': { group: 'Corporate', label: 'Approvals', groupPath: '/corporate/dashboard' },
  '/compliance': { group: 'Compliance', label: 'Compliance', groupPath: '/compliance' },
  '/sanctions': { group: 'Compliance', label: 'Sanctions', groupPath: '/compliance' },
  '/admin': { group: 'Compliance', label: 'Admin', groupPath: '/compliance' },
  '/settings': { group: 'Account', label: 'Settings', groupPath: '/settings' },
  '/whitepaper': { group: 'Account', label: 'Whitepaper', groupPath: '/whitepaper' },
  '/credit': { group: 'Banking', label: 'FTK Credit', groupPath: '/' },
  '/request-credit': { group: 'Banking', label: 'Request Credit', groupPath: '/' },
};

function getBreadcrumb(pathname: string) {
  if (breadcrumbMap[pathname]) return breadcrumbMap[pathname];
  if (pathname.startsWith('/sanctions/')) return breadcrumbMap['/sanctions'];
  return null;
}

/* ─── Sidebar Link ─── */
const SidebarLink: React.FC<{
  path: string;
  label: string;
  icon: React.ReactNode;
  collapsed: boolean;
  currentPath: string;
}> = ({ path, label, icon, collapsed, currentPath }) => {
  const isActive = path === '/'
    ? currentPath === '/'
    : (currentPath === path || currentPath.startsWith(path + '/'));
  return (
    <Router.Link
      to={path}
      className={`sidebar-link${isActive ? ' active' : ''}`}
      aria-label={label}
      title={collapsed ? label : undefined}
    >
      <span className="sidebar-link-icon">{icon}</span>
      {!collapsed && <span className="sidebar-link-label">{label}</span>}
    </Router.Link>
  );
};

/* ─── Layout Shell ─── */
const Layout: React.FC<LayoutProps> = ({ children }) => {
 const navigate = Router.useNavigate();
 const location = Router.useLocation();
 const { info } = useToast();
 const [token, setToken] = React.useState(typeof window !== 'undefined' ? localStorage.getItem('token') : null);
 const [progress, setProgress] = React.useState(0);
 const { enabled: fModeEnabled, toggle: toggleFMode } = useFMode();
 const skipLinkRef = React.useRef<HTMLAnchorElement>(null);
 const contentRef = React.useRef<HTMLDivElement>(null);

 const globalSearch = useGlobalSearch(navigate);
 const { user } = useAppContext();

 const [sidebarCollapsed, setSidebarCollapsed] = React.useState(false);
 const [mobileOpen, setMobileOpen] = React.useState(false);

 const [feedbackOpen, setFeedbackOpen] = React.useState(false);
 const [feedbackMessage, setFeedbackMessage] = React.useState('');
 const [feedbackSubmitting, setFeedbackSubmitting] = React.useState(false);
 const toast = useToast();

 const openFeedback = () => {
  setFeedbackMessage('');
  setFeedbackSubmitting(false);
  setFeedbackOpen(true);
 };

 const closeFeedback = () => {
  if (feedbackSubmitting) return;
  setFeedbackOpen(false);
  setFeedbackMessage('');
 };

 const submitFeedback = async () => {
  const trimmed = feedbackMessage.trim();
  if (trimmed.length < 10) return;
  setFeedbackSubmitting(true);
  try {
   await submitFeedbackApi(trimmed, token || '');
   setFeedbackOpen(false);
   setFeedbackMessage('');
   setFeedbackSubmitting(false);
   toast.success('Thank you! Your feedback has been submitted.');
  } catch (err: any) {
   let detail = 'Could not send feedback. Please try again.';
   try { if (err?.message) detail = err.message; } catch {}
   toast.error(detail);
  } finally {
   setFeedbackSubmitting(false);
  }
 };

 const handleToggle = (value: boolean) => {
 if (fModeEnabled === value) return;
 toggleFMode(value);
 info(value ? 'Switched to F-Mode (DeFi Enabled)' : 'Switched to Fiat Mode');
 };

 // Scroll progress from content div
 React.useEffect(() => {
  const el = contentRef.current;
  if (!el) return;
  const onScroll = () => {
   const scrolled = el.scrollTop / ((el.scrollHeight - el.clientHeight) || 1);
   setProgress(Math.max(0, Math.min(1, scrolled)) * 100);
  };
  onScroll();
  el.addEventListener('scroll', onScroll, { passive: true });
  return () => el.removeEventListener('scroll', onScroll);
 }, []);

 // Auth change + storage sync
 React.useEffect(() => {
  const off = onAuthChange((t) => setToken(t));
  const onStorage = (e: StorageEvent) => {
   if (e.key === 'token') setToken(localStorage.getItem('token'));
  };
  window.addEventListener('storage', onStorage);
  return () => {
   window.removeEventListener('storage', onStorage);
   off?.();
  };
 }, []);

 // Close mobile sidebar on route change
 React.useEffect(() => {
  setMobileOpen(false);
 }, [location.pathname]);

 const logout = async () => {
 try { await fetch('/users/logout', { method: 'POST', credentials: 'include' }); } catch {}
 clearAuth();
 navigate('/login', { replace: true });
 };

 const year = new Date().getFullYear();
 const currentPath = location.pathname;
 const breadcrumb = getBreadcrumb(currentPath);
 const userInitial = user?.email ? user.email.charAt(0).toUpperCase() : '?';
 const userRole = getOrganisationRole() || user?.role || '';
 const collapsedClass = sidebarCollapsed ? 'sidebar-collapsed' : '';

 return (
 <div className="shell">
   <a href="#main-content" className="skip-link" ref={skipLinkRef}>
     Skip to main content
   </a>
   <div className="progress-wrap" aria-hidden="true"><div className="progress-bar" style={{ width: `${progress}%` }} /></div>

   {mobileOpen && <div className="sidebar-backdrop" onClick={() => setMobileOpen(false)} />}

   <aside className={`shell-sidebar${sidebarCollapsed ? ' collapsed' : ''}${mobileOpen ? ' mobile-open' : ''}`}>
     <div className="sidebar-header">
       <Router.Link to={ROUTES.HOME} className="sidebar-logo" aria-label="Home">
         <img src="/money-security.svg" alt="FinTech logo" style={{ height: 28, width: 'auto', flexShrink: 0 }} />
         {!sidebarCollapsed && (
           <span className="sidebar-logo-block">
             <span className="sidebar-logo-text">FinTech Application</span>
             <span className="sidebar-logo-tagline">
               <span style={{ display: 'block' }}>Simple, fast and secure finance.</span>
               <span style={{ display: 'block' }}>Own your money flow.</span>
             </span>
           </span>
         )}
       </Router.Link>
     </div>

     <nav className="sidebar-nav" role="navigation" aria-label="Main">
       {token && (
         <div className="sidebar-group">
           {!sidebarCollapsed && <div className="sidebar-group-label">Banking</div>}
           <SidebarLink path={ROUTES.HOME} label="Dashboard" icon={<IconDashboard />} collapsed={sidebarCollapsed} currentPath={currentPath} />
           {!fModeEnabled && <SidebarLink path={ROUTES.ACCOUNTS} label="Accounts" icon={<IconAccounts />} collapsed={sidebarCollapsed} currentPath={currentPath} />}
           <SidebarLink path={ROUTES.TRANSACTIONS} label="Transactions" icon={<IconTransactions />} collapsed={sidebarCollapsed} currentPath={currentPath} />
           {!fModeEnabled && <SidebarLink path={ROUTES.CARDS} label="Cards" icon={<IconCards />} collapsed={sidebarCollapsed} currentPath={currentPath} />}
           {!fModeEnabled && <SidebarLink path={ROUTES.BUDGET} label="Budget" icon={<IconBudget />} collapsed={sidebarCollapsed} currentPath={currentPath} />}
         </div>
       )}

       {token && isCorporateUser() && (
         <div className="sidebar-group">
           {!sidebarCollapsed && <div className="sidebar-group-label">Corporate</div>}
           <SidebarLink path={ROUTES.CORPORATE_DASHBOARD} label="Corporate Dashboard" icon={<IconCorporate />} collapsed={sidebarCollapsed} currentPath={currentPath} />
           <SidebarLink path={ROUTES.CORPORATE_BATCHES} label="Batches" icon={<IconBatches />} collapsed={sidebarCollapsed} currentPath={currentPath} />
           <SidebarLink path={ROUTES.CORPORATE_APPROVALS} label="Approvals" icon={<IconApprovals />} collapsed={sidebarCollapsed} currentPath={currentPath} />
         </div>
       )}

       {token && (
         <div className="sidebar-group">
           {!sidebarCollapsed && <div className="sidebar-group-label">Compliance</div>}
           <SidebarLink path={ROUTES.COMPLIANCE} label="Compliance" icon={<IconCompliance />} collapsed={sidebarCollapsed} currentPath={currentPath} />
           {fModeEnabled && <SidebarLink path={ROUTES.SANCTIONS} label="Sanctions" icon={<IconSanctions />} collapsed={sidebarCollapsed} currentPath={currentPath} />}
           <SidebarLink path={ROUTES.ADMIN} label="Admin" icon={<IconAdmin />} collapsed={sidebarCollapsed} currentPath={currentPath} />
         </div>
       )}

       <div className="sidebar-group">
         {!sidebarCollapsed && <div className="sidebar-group-label">Account</div>}
         {token && <SidebarLink path={ROUTES.SETTINGS} label="Settings" icon={<IconSettings />} collapsed={sidebarCollapsed} currentPath={currentPath} />}
         <SidebarLink path={ROUTES.WHITEPAPER} label="Whitepaper" icon={<IconWhitepaper />} collapsed={sidebarCollapsed} currentPath={currentPath} />
       </div>
     </nav>

     {token && (
       <div className={`sidebar-fmode${sidebarCollapsed ? ' sidebar-fmode-collapsed' : ''}`}>
         {!sidebarCollapsed ? (
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
               <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: 4, verticalAlign: 'middle' }}>
                 <rect x="2" y="6" width="20" height="12" rx="2" />
                 <line x1="6" y1="12" x2="6" y2="12.01" />
                 <line x1="18" y1="12" x2="18" y2="12.01" />
               </svg>
               Fiat
             </button>
             <button
               className={`option ${fModeEnabled ? 'active' : ''}`}
               onClick={(e) => { e.stopPropagation(); handleToggle(true); }}
               aria-pressed={fModeEnabled}
               aria-label="Switch to F-Mode (DeFi)"
               type="button"
             >
               <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: 4, verticalAlign: 'middle' }}>
                 <polygon points="12 2 21.09 7 21.09 17 12 22 2.91 17 2.91 7 12 2" />
               </svg>
               F-Mode
             </button>
           </div>
         ) : (
           <div className="sidebar-fmode-icons" role="group" aria-label="Mode selection">
             <button
               className={`sidebar-fmode-icon-btn${!fModeEnabled ? ' active-fiat' : ''}`}
               onClick={(e) => { e.stopPropagation(); handleToggle(false); }}
               aria-pressed={!fModeEnabled}
               aria-label="Fiat Mode"
               title="Fiat"
               type="button"
             >
               <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                 <rect x="2" y="6" width="20" height="12" rx="2" />
                 <line x1="6" y1="12" x2="6" y2="12.01" />
                 <line x1="18" y1="12" x2="18" y2="12.01" />
               </svg>
             </button>
             <button
               className={`sidebar-fmode-icon-btn${fModeEnabled ? ' active-fmode' : ''}`}
               onClick={(e) => { e.stopPropagation(); handleToggle(true); }}
               aria-pressed={fModeEnabled}
               aria-label="F-Mode (DeFi)"
               title="F-Mode"
               type="button"
             >
               <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                 <polygon points="12 2 21.09 7 21.09 17 12 22 2.91 17 2.91 7 12 2" />
               </svg>
             </button>
           </div>
         )}
       </div>
     )}

     <button
       className="sidebar-toggle"
       onClick={() => setSidebarCollapsed(prev => !prev)}
       aria-label={sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
       type="button"
     >
       <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
         style={{ transform: sidebarCollapsed ? 'rotate(180deg)' : 'none', transition: 'transform 0.25s ease' }}>
         <polyline points="15 18 9 12 15 6" />
       </svg>
     </button>
   </aside>

   <header className={`shell-topbar ${collapsedClass}`} role="banner">
     <div className="topbar-left">
       <button className="topbar-hamburger" type="button" onClick={() => setMobileOpen(prev => !prev)} aria-label="Toggle menu">
         <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
           <line x1="3" y1="12" x2="21" y2="12" />
           <line x1="3" y1="6" x2="21" y2="6" />
           <line x1="3" y1="18" x2="21" y2="18" />
         </svg>
       </button>
     </div>

     <div className="topbar-center">
       {token && (
         <button type="button" className="gs-trigger" onClick={globalSearch.open} aria-label="Search (Cmd+K)">
           <span className="gs-trigger-icon"><svg width="14" height="14" viewBox="0 0 14 14" fill="none"><circle cx="5.8" cy="5.8" r="4.3" stroke="currentColor" strokeWidth="1.3"/><line x1="9" y1="9" x2="12.5" y2="12.5" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round"/></svg></span>
           <span className="gs-trigger-kbd">{navigator.platform?.indexOf('Mac') > -1 ? '⌘' : 'Ctrl+'}K</span>
         </button>
       )}
     </div>

     <div className="topbar-right">
       {token && <NotificationBell />}
       {token && (
         <AvatarDropdown
           userEmail={user?.email || ''}
           userInitial={userInitial}
           onLogout={logout}
         />
       )}
       {token && userRole && (
         <span className="topbar-role-chip">{userRole.toUpperCase()}</span>
       )}
       {!token && (
         <>
           <Router.Link to={ROUTES.LOGIN} className="topbar-auth-link" aria-label="Login">Login</Router.Link>
           <Router.Link to={ROUTES.REGISTER} className="topbar-auth-link" aria-label="Register">Register</Router.Link>
         </>
       )}
     </div>
   </header>

   <div className={`shell-breadcrumb ${collapsedClass}`}>
     {breadcrumb ? (
       breadcrumb.group && breadcrumb.label !== breadcrumb.group ? (
         <>
           <Router.Link to={breadcrumb.groupPath} className="breadcrumb-link">{breadcrumb.group}</Router.Link>
           <span className="breadcrumb-sep">/</span>
           <span className="breadcrumb-current" aria-current="page">{breadcrumb.label}</span>
         </>
         ) : (
           <span className="breadcrumb-current" aria-current="page">{breadcrumb.label}</span>
         )
       ) : (
         <span className="breadcrumb-current" aria-current="page">
           {currentPath.split('/').filter(Boolean).map(s => s.charAt(0).toUpperCase() + s.slice(1)).join(' / ') || 'Home'}
         </span>
     )}
   </div>

   <main className={`shell-content ${collapsedClass}`} role="main" id="main-content" ref={contentRef}>
     {token && <SessionExpiryBanner onRenew={refreshAccessToken} />}
     <div className="container">
       {children}
       <footer className="site-footer" role="contentinfo" aria-label="Footer">
        <div className="footer-inner">
           <div className="footer-links">
            <Router.Link to={ROUTES.PRIVACY}>Privacy &amp; Terms</Router.Link>
            {token && (
             <>
              <span className="footer-divider" aria-hidden="true" />
              <button type="button" className="feedback-trigger" onClick={openFeedback} aria-label="Send feedback about this application">
               Feedback
              </button>
             </>
            )}
           </div>
           <span className="footer-copyright">&copy; {year} Anubhav Sharma. All rights reserved.</span>
           <span className="footer-version" aria-label="Application version">v1.0.0</span>
           <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <span className="footer-brand">Open Finance</span>
            <span className="footer-badge" aria-label="Sandbox environment">
             <span className="badge-dot" aria-hidden="true" />
             Sandbox
            </span>
            <div className="footer-social">
             <a href="https://www.linkedin.com/in/anubhav-sharma-/" target="_blank" rel="noreferrer" aria-label="LinkedIn">
              in
             </a>
            </div>
           </div>
         </div>
       </footer>
     </div>
   </main>

   <FeedbackModal
     isOpen={feedbackOpen}
     onClose={closeFeedback}
     token={token}
     message={feedbackMessage}
     onMessageChange={setFeedbackMessage}
     submitting={feedbackSubmitting}
     onSubmit={submitFeedback}
   />
   <GlobalSearch search={globalSearch} />
 </div>
 );
};

export default Layout;
