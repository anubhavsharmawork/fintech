import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate, useLocation } from 'react-router-dom';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import Login from './pages/Login';
import Register from './pages/Register';
import Accounts from './pages/Accounts';
import Transactions from './pages/Transactions';
import Budget from './pages/Budget';
import Privacy from './pages/Privacy';
import SanctionsList from './pages/SanctionsList';
import SanctionDetail from './pages/SanctionDetail';
import Admin from './pages/Admin';
import Compliance from './pages/Compliance';
import CorporateDashboard from './pages/CorporateDashboard';
import PaymentBatch from './pages/PaymentBatch';
import Approvals from './pages/Approvals';
import FtkCredit from './pages/FtkCredit';
import RequestCredit from './pages/RequestCredit';
import Settings from './pages/Settings';
import Cards from './pages/Cards';
import Whitepaper from './pages/Whitepaper';
import NotFound from './pages/NotFound';
import { getToken, onAuthChange, clearAuth, decodeJwt } from './auth';
import { ROUTES } from './config/constants';

function RequireAuth({ children }: { children: JSX.Element }) {
 const [checked, setChecked] = React.useState(false);
 const [hasToken, setHasToken] = React.useState<boolean>(!!(typeof window !== 'undefined' && localStorage.getItem('token')));
 const location = useLocation();

 React.useEffect(() => {
 let mounted = true;

 const checkExpiry = (token: string): boolean => {
   const payload = decodeJwt(token);
   if (!payload || typeof payload.exp !== 'number') return false;
   return Math.floor(Date.now() / 1000) >= payload.exp;
 };

 (async () => {
   const t = await getToken();
   if (!mounted) return;
   if (!t || checkExpiry(t)) {
     if (t) clearAuth();
     setHasToken(false);
     setChecked(true);
     return;
   }
   setHasToken(true);
   setChecked(true);
 })();

 const off = onAuthChange((t) => {
   if (t && checkExpiry(t)) {
     clearAuth();
     setHasToken(false);
   } else {
     setHasToken(!!t);
   }
   setChecked(true);
 });

 const interval = setInterval(async () => {
   const t = await getToken();
   if (t && checkExpiry(t)) {
     clearAuth();
     setHasToken(false);
   }
 }, 30000);

 return () => { mounted = false; off?.(); clearInterval(interval); };
 }, []);

 if (!checked) return null;
 if (!hasToken) {
 return <Navigate to={ROUTES.LOGIN} replace state={{ from: location }} />;
 }
 return children;
}

function AnimatedRoutes() {
  const location = useLocation();
  return (
    <div key={location.pathname} className="route-fade-in">
      <Routes location={location}>
        <Route path={ROUTES.HOME} element={<RequireAuth><Dashboard /></RequireAuth>} />
        <Route path={ROUTES.LOGIN} element={<Login />} />
        <Route path={ROUTES.REGISTER} element={<Register />} />
        <Route path={ROUTES.ACCOUNTS} element={<RequireAuth><Accounts /></RequireAuth>} />
        <Route path={ROUTES.TRANSACTIONS} element={<RequireAuth><Transactions /></RequireAuth>} />
        <Route path={ROUTES.BUDGET} element={<RequireAuth><Budget /></RequireAuth>} />
        <Route path={ROUTES.SANCTIONS} element={<RequireAuth><SanctionsList /></RequireAuth>} />
        <Route path="/sanctions/:id" element={<RequireAuth><SanctionDetail /></RequireAuth>} />
        <Route path={ROUTES.ADMIN} element={<RequireAuth><Admin /></RequireAuth>} />
        <Route path={ROUTES.COMPLIANCE} element={<RequireAuth><Compliance /></RequireAuth>} />
        <Route path={ROUTES.CORPORATE_DASHBOARD} element={<RequireAuth><CorporateDashboard /></RequireAuth>} />
        <Route path={ROUTES.CORPORATE_BATCHES} element={<RequireAuth><PaymentBatch /></RequireAuth>} />
        <Route path={ROUTES.CORPORATE_APPROVALS} element={<RequireAuth><Approvals /></RequireAuth>} />
        <Route path={ROUTES.CREDIT} element={<RequireAuth><FtkCredit /></RequireAuth>} />
        <Route path={ROUTES.REQUEST_CREDIT} element={<RequireAuth><RequestCredit /></RequireAuth>} />
        <Route path={ROUTES.SETTINGS} element={<RequireAuth><Settings /></RequireAuth>} />
        <Route path={ROUTES.CARDS} element={<RequireAuth><Cards /></RequireAuth>} />
        <Route path={ROUTES.PRIVACY} element={<Privacy />} />
        <Route path={ROUTES.WHITEPAPER} element={<Whitepaper />} />
        <Route path={ROUTES.NOT_FOUND} element={<NotFound />} />
      </Routes>
    </div>
  );
}

function App() {
  return (
  <Router>
  <Layout>
  <AnimatedRoutes />
  </Layout>
  </Router>
  );
}

export default App;
