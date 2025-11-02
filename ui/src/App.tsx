import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate, useLocation } from 'react-router-dom';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import Login from './pages/Login';
import Register from './pages/Register';
import Accounts from './pages/Accounts';
import Transactions from './pages/Transactions';
import { getToken, onAuthChange } from './auth';

function RequireAuth({ children }: { children: JSX.Element }) {
 const [checked, setChecked] = React.useState(false);
 const [hasToken, setHasToken] = React.useState<boolean>(!!(typeof window !== 'undefined' && localStorage.getItem('token')));
 const location = useLocation();

 React.useEffect(() => {
 let mounted = true;
 (async () => {
 const t = await getToken();
 if (!mounted) return;
 setHasToken(!!t);
 setChecked(true);
 })();
 const off = onAuthChange((t) => {
 setHasToken(!!t);
 setChecked(true);
 });
 return () => { mounted = false; off?.(); };
 }, []);

 if (!checked) return null;
 if (!hasToken) {
 return <Navigate to="/login" replace state={{ from: location }} />;
 }
 return children;
}

function App() {
 return (
 <Router>
 <Layout>
 <Routes>
 <Route path="/" element={<RequireAuth><Dashboard /></RequireAuth>} />
 <Route path="/login" element={<Login />} />
 <Route path="/register" element={<Register />} />
 <Route path="/accounts" element={<RequireAuth><Accounts /></RequireAuth>} />
 <Route path="/transactions" element={<RequireAuth><Transactions /></RequireAuth>} />
 </Routes>
 </Layout>
 </Router>
 );
}

export default App;