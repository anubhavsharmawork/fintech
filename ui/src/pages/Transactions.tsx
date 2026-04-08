import * as React from 'react';
import { Sparkles, Home, TrendingUp, Link as LinkIcon, Wallet, Check, ChevronDown } from 'lucide-react';
import { useToast } from '../components/Toast';
import { authFetch } from '../auth';
import { useFMode } from '../hooks/useFMode';
import { useAsync } from '../hooks/useAsync';
import { usePagination } from '../hooks/usePagination';
import Pagination from '../components/Pagination';
import PageLoader from '../components/PageLoader';
import ConnectWallet from '../components/ConnectWallet';
import CryptoAccountSwitcher from '../components/CryptoAccountSwitcher';
import CryptoModeBanner from '../components/CryptoModeBanner';
import CryptoTransactionHistory from '../components/CryptoTransactionHistory';
import TransactionStatus from '../components/TransactionStatus';
import ConfirmPaymentModal from '../components/ConfirmPaymentModal';
import { sendFTKTransfer, isValidAddress, estimateTransferGas, TransactionResult, GasEstimate } from '../services/crypto';
import { exportCSV, exportPDF, ExportableTransaction } from '../services/exportTransactions';
import { CHART_COLORS } from '../components/charts/chartTheme';
import { API } from '../config/constants';
import {
  AreaChart,
  Area,
  ResponsiveContainer,
} from 'recharts';

interface Transaction {
 id: string;
 accountId: string;
 amount: number;
 currency: string;
 type: 'credit' | 'debit';
 description: string;
 createdAt: string;
 spendingType?: string;
 status?: 'Completed' | 'Pending' | 'Failed' | 'Processing';
}

interface Account { id: string; accountNumber: string; accountType: string; balance?: number }
interface Payee { id: string; name: string; accountNumber: string }
interface UserLite { id: string; email: string; firstName: string; lastName: string }

type Tab = 'pay' | 'payee' | 'history';

const Transactions = () => {
 const [accounts, setAccounts] = React.useState<Account[]>([]);
 const [payees, setPayees] = React.useState<Payee[]>([]);
 const [users, setUsers] = React.useState<UserLite[]>([]);
 const [active, setActive] = React.useState<Tab>('pay');
 const [busySend, setBusySend] = React.useState(false);
 const [busyAdd, setBusyAdd] = React.useState(false);
 const [localError, setLocalError] = React.useState<string | null>(null);

 const { success, error: toastError } = useToast();
 const pagination = usePagination({ defaultPageSize: 25, syncToUrl: true });

 // Use useAsync for transactions loading
 const { data: transactions, loading, error: asyncError, refetch: refetchTransactions } = useAsync<Transaction[]>(
   async (signal) => {
     const res = await authFetch(API.TRANSACTIONS, { signal });
     if (!res.ok) throw new Error(`Failed to load transactions (${res.status})`);
     return res.json();
   },
   []
 );

  const txList = React.useMemo(() => transactions ?? [], [transactions]);
  const error = localError || asyncError;

  // Payee + payment state
  const [payeeForm, setPayeeForm] = React.useState({ name: '', accountNumber: '', userId: '' });
  const [paymentForm, setPaymentForm] = React.useState({ accountId: '', amount: '', payeeId: '', description: '' });
  const [spendingType, setSpendingType] = React.useState(() => localStorage.getItem('lastSpendingType') || '');
  const { enabled: fModeEnabled } = useFMode();
 const [walletAddress, setWalletAddress] = React.useState<string | null>(null);
 const [recipientAddress, setRecipientAddress] = React.useState('');
 const [walletSigner, setWalletSigner] = React.useState<any>(null);
 const [walletProvider, setWalletProvider] = React.useState<any>(null);
 const [pendingTx, setPendingTx] = React.useState<TransactionResult | null>(null);
 const [gasEstimate, setGasEstimate] = React.useState<GasEstimate | null>(null);
 const [estimatingGas, setEstimatingGas] = React.useState(false);
 const [txRefreshTrigger, setTxRefreshTrigger] = React.useState(0);
 const [recipientError, setRecipientError] = React.useState<string | null>(null);
 const [isDemoMode, setIsDemoMode] = React.useState(false);

 // Confirmation modal state
 const [pendingPayment, setPendingPayment] = React.useState<{ amount: string; payeeName: string; currency?: string } | null>(null);
 const [pendingPaymentResolve, setPendingPaymentResolve] = React.useState<((confirmed: boolean) => void) | null>(null);

 // History sort/filter state
 const [historySearch, setHistorySearch] = React.useState('');
 const [historyType, setHistoryType] = React.useState<'all' | 'credit' | 'debit'>('all');
 const [historySpending, setHistorySpending] = React.useState('');
 const [historySortField, setHistorySortField] = React.useState<'createdAt' | 'amount'>('createdAt');
 const [historySortDir, setHistorySortDir] = React.useState<'asc' | 'desc'>('desc');
 const [historyFromDate, setHistoryFromDate] = React.useState('');
 const [historyToDate, setHistoryToDate] = React.useState('');
 const [exportMenuOpen, setExportMenuOpen] = React.useState(false);
 const exportRef = React.useRef<HTMLDivElement>(null);

 // Reset pagination to page 1 when filters or sort change
 const prevFiltersRef = React.useRef({ historySearch, historyType, historySpending, historySortField, historySortDir, historyFromDate, historyToDate });
 React.useEffect(() => {
   const prev = prevFiltersRef.current;
   if (prev.historySearch !== historySearch || prev.historyType !== historyType || 
       prev.historySpending !== historySpending || prev.historySortField !== historySortField || 
       prev.historySortDir !== historySortDir || prev.historyFromDate !== historyFromDate ||
       prev.historyToDate !== historyToDate) {
     pagination.resetToFirstPage();
     prevFiltersRef.current = { historySearch, historyType, historySpending, historySortField, historySortDir, historyFromDate, historyToDate };
   }
 }, [historySearch, historyType, historySpending, historySortField, historySortDir, historyFromDate, historyToDate, pagination]);

 React.useEffect(() => {
   if (fModeEnabled && active === 'payee') {
     setActive('pay');
   }
 }, [fModeEnabled, active]);

 const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' });
 const formatDate = (dateString: string) => new Date(dateString).toLocaleDateString('en-NZ');

 const fetchAccounts = React.useCallback(async () => {
 const res = await authFetch(API.ACCOUNTS);
 if (res.ok) {
 const data = await res.json();
 setAccounts(data);
 if (data.length && !paymentForm.accountId) setPaymentForm(p => ({ ...p, accountId: data[0].id }));
 }
 }, [paymentForm.accountId]);

 const fetchPayees = React.useCallback(async () => {
 const res = await authFetch(API.PAYEES);
 if (res.ok) {
 const data: Payee[] = await res.json();
 setPayees(data);
 const defaultPayee = data.find(p => /demo/i.test(p.name)) || data[0];
 if (defaultPayee && !paymentForm.payeeId) setPaymentForm(p => ({ ...p, payeeId: defaultPayee.id }));
 }
 }, [paymentForm.payeeId]);

 const fetchUsers = React.useCallback(async () => {
 const res = await authFetch(API.USERS_ALL);
 if (res.ok) {
 const data: UserLite[] = await res.json();
 setUsers(data);
 }
 }, []);

 React.useEffect(() => {
 fetchAccounts();
 fetchPayees();
 fetchUsers();
 }, [fetchAccounts, fetchPayees, fetchUsers]);

 // Close export menu on outside click
 React.useEffect(() => {
   const handleClickOutside = (e: MouseEvent) => {
     if (exportRef.current && !exportRef.current.contains(e.target as Node)) {
       setExportMenuOpen(false);
     }
   };
   if (exportMenuOpen) document.addEventListener('mousedown', handleClickOutside);
   return () => document.removeEventListener('mousedown', handleClickOutside);
 }, [exportMenuOpen]);

 // Filtered + sorted fiat transactions (reusable for render and export)
 const allFilteredFiatTransactions = React.useMemo(() =>
   txList
     .filter(t => t.currency !== 'FTK')
     .filter(t => historyType === 'all' || t.type === historyType)
     .filter(t => !historySpending || t.spendingType === historySpending)
     .filter(t => !historySearch || t.description.toLowerCase().includes(historySearch.toLowerCase()))
     .filter(t => {
       if (!historyFromDate) return true;
       const txDate = new Date(t.createdAt);
       const fromDate = new Date(historyFromDate);
       fromDate.setHours(0, 0, 0, 0);
       return txDate >= fromDate;
     })
     .filter(t => {
       if (!historyToDate) return true;
       const txDate = new Date(t.createdAt);
       const toDate = new Date(historyToDate);
       toDate.setHours(23, 59, 59, 999);
       return txDate <= toDate;
     })
     .sort((a, b) => {
       const dir = historySortDir === 'asc' ? 1 : -1;
       if (historySortField === 'amount') return (a.amount - b.amount) * dir;
       return (new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()) * dir;
     }),
   [txList, historyType, historySpending, historySearch, historyFromDate, historyToDate, historySortField, historySortDir]
 );

 // Update pagination total count when filtered results change
 React.useEffect(() => {
   pagination.setTotalCount(allFilteredFiatTransactions.length);
 }, [allFilteredFiatTransactions.length, pagination]);

 // Paginated transactions for display
 const filteredFiatTransactions = React.useMemo(() => {
   const start = (pagination.page - 1) * pagination.pageSize;
   return allFilteredFiatTransactions.slice(start, start + pagination.pageSize);
 }, [allFilteredFiatTransactions, pagination.page, pagination.pageSize]);

 // Sparkline data: transaction count per day for the last 30 days
 const sparklineData = React.useMemo(() => {
   const now = new Date();
   const thirtyDaysAgo = new Date(now);
   thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);

   // Group transactions by date
   const countsByDate = new Map<string, number>();
   txList.forEach(t => {
     const date = new Date(t.createdAt);
     if (date >= thirtyDaysAgo) {
       const dateKey = date.toISOString().split('T')[0];
       countsByDate.set(dateKey, (countsByDate.get(dateKey) || 0) + 1);
     }
   });

   // Generate array for last 30 days
   const data: { day: string; count: number }[] = [];
   for (let i = 29; i >= 0; i--) {
     const date = new Date(now);
     date.setDate(date.getDate() - i);
     const dateKey = date.toISOString().split('T')[0];
     data.push({
       day: dateKey,
       count: countsByDate.get(dateKey) || 0,
     });
   }
   return data;
 }, [txList]);

 const handleExport = (format: 'csv' | 'pdf') => {
   setExportMenuOpen(false);
   // Export all filtered transactions, not just current page
   const exportable: ExportableTransaction[] = allFilteredFiatTransactions.map(t => ({
     ...t,
     accountNumber: accountNumberFromId(t.accountId),
   }));
   if (format === 'csv') exportCSV(exportable);
   else exportPDF(exportable);
 };

  const addPayee = async (e: React.FormEvent) => {
  e.preventDefault();
  setBusyAdd(true);
  let name = payeeForm.name;
  let accountNumber = payeeForm.accountNumber;
  if (payeeForm.userId) {
  const u = users.find(u => u.id === payeeForm.userId);
  if (u) {
  name = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.email;
  }
  }
  const res = await authFetch(API.PAYEES, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, accountNumber }) });
  if (!res.ok) {
  setBusyAdd(false);
  setLocalError('Failed to add payee');
  return toastError('Failed to add payee');
  }
  await res.json();
  setPayeeForm({ name: '', accountNumber: '', userId: '' });
  await fetchPayees();
  setBusyAdd(false);
  success('Payee added');
  };

  const sendPayment = async (e: React.FormEvent) => {
     e.preventDefault();
     setBusySend(true);
     setLocalError(null);

    try {
      const amountValue = parseFloat(paymentForm.amount);
      if (Number.isNaN(amountValue) || amountValue <= 0) {
        throw new Error('Enter a valid amount greater than 0');
      }

      if (!spendingType) {
        throw new Error('Please select a Conscious Spending Type');
      }

      const payee = payees.find(p => p.id === paymentForm.payeeId);
      if (!fModeEnabled && !payee) {
        throw new Error('Select a payee before sending');
      }

      // Show confirmation modal before proceeding
      const confirmed = await new Promise<boolean>((resolve) => {
        setPendingPayment({
          amount: paymentForm.amount,
          payeeName: fModeEnabled ? recipientAddress : (payee?.name ?? ''),
          currency: fModeEnabled ? 'FTK' : 'NZD',
        });
        setPendingPaymentResolve(() => resolve);
      });

      if (!confirmed) {
        setBusySend(false);
        return;
      }

      let txHash: string | undefined;
      let txResult: TransactionResult | undefined;
      if (fModeEnabled) {
        if (!walletSigner || !walletAddress) {
          throw new Error('Connect wallet to send in F-Mode');
        }
        if (!recipientAddress) {
          throw new Error('Recipient wallet address required');
        }
        if (!isValidAddress(recipientAddress)) {
          throw new Error('Invalid recipient wallet address');
        }

        try {
          txResult = await sendFTKTransfer(walletSigner, recipientAddress, paymentForm.amount);
          txHash = txResult.hash;
          setPendingTx(txResult);
        } catch (err: any) {
          throw new Error(err?.message || 'Crypto transfer failed');
        }
      }

      const payload = {
        accountId: paymentForm.accountId,
        amount: amountValue,
        payeeName: fModeEnabled ? recipientAddress : payee?.name,
        payeeAccountNumber: fModeEnabled ? undefined : payee?.accountNumber,
        description: paymentForm.description,
        spendingType,
        txHash,
        currency: fModeEnabled ? 'FTK' : undefined
      };

      const res = await authFetch(API.PAYMENTS, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
      if (!res.ok) {
        throw new Error('Failed to send payment');
      }

      await Promise.all([refetchTransactions(), fetchAccounts()]);
      setPaymentForm(p => ({ ...p, amount: '', description: '' }));
      setRecipientAddress('');
      setGasEstimate(null);
      if (fModeEnabled) {
        setTxRefreshTrigger(prev => prev + 1);
      }
      setActive('history');
      success(fModeEnabled ? 'Transaction submitted! View on Etherscan for confirmation.' : 'Payment sent');
    } catch (err: any) {
      const message = err?.message || 'Failed to send payment';
      setLocalError(message);
      toastError(message);
    } finally {
      setBusySend(false);
    }
  };

 const handleConfirmPayment = (confirmed: boolean) => {
   setPendingPayment(null);
   if (pendingPaymentResolve) {
     pendingPaymentResolve(confirmed);
     setPendingPaymentResolve(null);
   }
 };

 const TabButton = ({ id, children }: { id: Tab, children: React.ReactNode }) => (
 <button className={"btn " + (active === id ? 'btn-primary' : 'btn-secondary')} style={{ marginRight:8 }} onClick={() => setActive(id)} type="button">{children}</button>
 );

 const accountNumberFromId = (id: string) => accounts.find(a => a.id === id)?.accountNumber ?? '?';

 return (
 <div>
 <h2>Transactions</h2>

 {pendingPayment && (
   <ConfirmPaymentModal
     amount={pendingPayment.amount}
     payeeName={pendingPayment.payeeName}
     currency={pendingPayment.currency}
     onConfirm={() => handleConfirmPayment(true)}
     onCancel={() => handleConfirmPayment(false)}
   />
 )}

 <div style={{ marginBottom:12 }}>
 <TabButton id="pay">{fModeEnabled ? 'Crypto Transfer' : 'Send Money'}</TabButton>
 {!fModeEnabled && <TabButton id="payee">Add Payee</TabButton>}
 <TabButton id="history">History</TabButton>
 </div>

 {error && <p style={{ color: 'var(--text-error)' }}>{error}</p>}

 {active === 'pay' && (
 <div className="card">
 <h3>{fModeEnabled ? 'FTK Token Transfer' : 'Send Money'}</h3>
 {fModeEnabled && <CryptoModeBanner enabled />}
 {fModeEnabled ? (
   <>
     <ConnectWallet onConnected={(addr, signer, provider, isDemo) => { setWalletAddress(addr); setWalletSigner(signer); setWalletProvider(provider); setIsDemoMode(!!isDemo); }} onDisconnected={() => { setWalletAddress(null); setWalletSigner(null); setWalletProvider(null); setPendingTx(null); setIsDemoMode(false); }} />
     <CryptoAccountSwitcher address={walletAddress ?? undefined} demoAddress={walletAddress ?? undefined} isDemo={isDemoMode} />
     
     <form onSubmit={sendPayment}>
        <div className="form-group">
          <label htmlFor="recipient">Recipient Wallet Address</label>
          <input 
            id="recipient" 
            type="text" 
            value={recipientAddress} 
            onChange={e => {
              const value = (e.target as HTMLInputElement).value;
              setRecipientAddress(value);
              if (value && !isValidAddress(value)) {
                setRecipientError('Invalid Ethereum address format');
              } else {
                setRecipientError(null);
              }
            }} 
            placeholder="0x..." 
            required 
            style={{ fontFamily: 'monospace' }}
          />
          {recipientError && <small style={{ color: 'var(--text-error)' }}>{recipientError}</small>}
           {recipientAddress && isValidAddress(recipientAddress) && (
             <small style={{ color: 'var(--text-success)' }}><Check size={14} style={{ verticalAlign: 'middle', marginRight: 2 }} /> Valid address</small>
           )}
         </div>
        <div className="form-group">
          <label htmlFor="amount">Amount (FTK)</label>
          <input 
            id="amount" 
            type="number" 
            step="0.01" 
            min="0.01" 
            value={paymentForm.amount} 
            onChange={async e => {
              const value = (e.target as HTMLInputElement).value;
              setPaymentForm({ ...paymentForm, amount: value });
              // Estimate gas when amount changes
              if (walletSigner && recipientAddress && isValidAddress(recipientAddress) && parseFloat(value) > 0) {
                setEstimatingGas(true);
                try {
                  const estimate = await estimateTransferGas(walletSigner, recipientAddress, value);
                  setGasEstimate(estimate);
                } catch {
                  setGasEstimate(null);
                } finally {
                  setEstimatingGas(false);
                }
              }
            }} 
            required 
          />
          {estimatingGas && <small style={{ color: 'var(--text-hint)' }}>Estimating gas...</small>}
           {gasEstimate && !estimatingGas && (
             <small style={{ color: 'var(--text-hint-subtle)' }}>
              Estimated gas: ~{parseFloat(gasEstimate.estimatedCostEth).toFixed(6)} ETH
            </small>
          )}
        </div>
         <div className="form-group">
           <label htmlFor="spendingType" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
             Conscious Spending Type
             <span className="info-tooltip" style={{ position: 'relative', display: 'inline-flex', cursor: 'help' }}>
               <span style={{ fontSize: '0.85rem', color: 'var(--muted)', border: '1px solid var(--muted)', borderRadius: '50%', width: '16px', height: '16px', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontWeight: 600 }}>i</span>
               <span className="tooltip-popup" style={{ position: 'absolute', bottom: '120%', left: '50%', transform: 'translateX(-50%)', background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '10px 12px', fontSize: '0.75rem', whiteSpace: 'nowrap', boxShadow: '0 4px 12px rgba(0,0,0,0.15)', zIndex: 100, display: 'none' }}>
                 <strong style={{ display: 'block', marginBottom: '6px' }}>Categorize your spending:</strong>
                 <span style={{ display: 'block', color: 'var(--tooltip-fun)' }}><Sparkles size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} /> Fun: Discretionary spending</span>
                 <span style={{ display: 'block', color: 'var(--tooltip-fixed)' }}><Home size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} /> Fixed: Bills, recurring costs</span>
                 <span style={{ display: 'block', color: 'var(--tooltip-future)' }}><TrendingUp size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} /> Future: Savings & investments</span>
               </span>
             </span>
           </label>
           <select
             id="spendingType"
             value={spendingType}
             onChange={e => {
               const value = (e.target as HTMLSelectElement).value;
               setSpendingType(value);
               localStorage.setItem('lastSpendingType', value);
             }}
             required
           >
             <option value="" disabled>-- Select Type --</option>
             <option value="Fun">Fun</option>
             <option value="Fixed">Fixed</option>
             <option value="Future">Future</option>
           </select>
         </div>
         <button className="btn btn-primary" type="submit" disabled={isDemoMode || !walletSigner || busySend || !!recipientError}>{isDemoMode ? 'Demo Mode (View Only)' : busySend ? (<><span className="spinner" /> Processing...</>) : 'Transfer FTK'}</button>
      </form>

      {pendingTx && (
        <TransactionStatus 
          transaction={pendingTx} 
          provider={walletProvider}
          onConfirmed={() => {
            setTxRefreshTrigger(prev => prev + 1);
          }}
        />
      )}

      <CryptoTransactionHistory 
        address={walletAddress} 
        refreshTrigger={txRefreshTrigger}
      />
    </>
  ) : (
    <form onSubmit={sendPayment}>
     <div className="form-group">
     <label htmlFor="fromAccount">From Account</label>
     <select id="fromAccount" aria-label="Choose source account" value={paymentForm.accountId} onChange={e => setPaymentForm({ ...paymentForm, accountId: (e.target as HTMLSelectElement).value })} required>
     {accounts.map(a => (<option key={a.id} value={a.id}>{a.accountType} - {a.accountNumber}</option>))}
     </select>
     <small className="small">Choose the account you want to send from.</small>
     </div>
     <div className="form-group">
       <label htmlFor="toPayee">To Payee</label>
       <select id="toPayee" aria-label="Choose a payee" value={paymentForm.payeeId} onChange={e => setPaymentForm({ ...paymentForm, payeeId: (e.target as HTMLSelectElement).value })} required>
         {payees.map(p => (<option key={p.id} value={p.id}>{p.name} - {p.accountNumber}</option>))}
       </select>
       <small className="small">Select a saved payee.</small>
     </div>
     <div className="form-group">
  <label htmlFor="desc">Description</label>
  <input id="desc" type="text" value={paymentForm.description} onChange={e => setPaymentForm({ ...paymentForm, description: (e.target as HTMLInputElement).value })} placeholder="Optional description" />
   </div>
   <div className="form-group">
    <label htmlFor="spendingType" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
      Conscious Spending Type
      <span className="info-tooltip" style={{ position: 'relative', display: 'inline-flex', cursor: 'help' }}>
        <span style={{ fontSize: '0.85rem', color: 'var(--muted)', border: '1px solid var(--muted)', borderRadius: '50%', width: '16px', height: '16px', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontWeight: 600 }}>i</span>
            <span className="tooltip-popup" style={{ position: 'absolute', bottom: '120%', left: '50%', transform: 'translateX(-50%)', background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '10px 12px', fontSize: '0.75rem', whiteSpace: 'nowrap', boxShadow: '0 4px 12px rgba(0,0,0,0.15)', zIndex: 100, display: 'none' }}>
              <strong style={{ display: 'block', marginBottom: '6px' }}>Categorize your spending:</strong>
              <span style={{ display: 'block', color: 'var(--tooltip-fun)' }}><Sparkles size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} /> Fun: Discretionary spending</span>
              <span style={{ display: 'block', color: 'var(--tooltip-fixed)' }}><Home size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} /> Fixed: Bills, recurring costs</span>
              <span style={{ display: 'block', color: 'var(--tooltip-future)' }}><TrendingUp size={14} style={{ verticalAlign: 'middle', marginRight: 4 }} /> Future: Savings & investments</span>
            </span>
          </span>
        </label>
        <select
      id="spendingType"
      value={spendingType}
      onChange={e => {
        const value = (e.target as HTMLSelectElement).value;
        setSpendingType(value);
        localStorage.setItem('lastSpendingType', value);
      }}
      required
    >
              <option value="" disabled>-- Select Type --</option>
              <option value="Fun">Fun</option>
              <option value="Fixed">Fixed</option>
              <option value="Future">Future</option>
            </select>
            </div>
                 <button className="btn btn-primary" type="submit" disabled={!accounts.length || (!fModeEnabled && !payees.length) || busySend}>{busySend ? (<><span className="spinner" /> Processing...</>) : 'Send Payment'}</button>
         </form>
  )}
 </div>
 )}

 {active === 'payee' && (
 <div className="card">
 <h3>Add Payee</h3>
 <form onSubmit={addPayee}>
 <div className="form-group">
 <label htmlFor="selUser">Select Existing User</label>
 <select
 id="selUser"
 aria-label="Select existing user to prefill name"
 value={payeeForm.userId}
 onChange={e => {
 const userId = (e.target as HTMLSelectElement).value;
 setPayeeForm(p => {
 const updated = { ...p, userId };
 const u = users.find(u => u.id === userId);
 if (u) {
 const autoName = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.email;
 updated.name = autoName;
 }
 return updated;
 });
 }}
 >
 <option value="">-- Optional: choose a user --</option>
 {users.map(u => (
 <option key={u.id} value={u.id}>{u.firstName} {u.lastName} - {u.email}</option>
 ))}
 </select>
 </div>
 <div className="form-group">
 <label htmlFor="payeeName">Payee Name</label>
 <input id="payeeName" value={payeeForm.name} onChange={e => setPayeeForm({ ...payeeForm, name: (e.target as HTMLInputElement).value })} placeholder="Name (auto-filled if user selected)" />
 </div>
 <div className="form-group">
 <label htmlFor="payeeAcc">Account Number</label>
 <input id="payeeAcc" value={payeeForm.accountNumber} onChange={e => setPayeeForm({ ...payeeForm, accountNumber: (e.target as HTMLInputElement).value })} required placeholder="12-digit account number" />
 <small className="small">Enter exact12 digits.</small>
 </div>
 <button className="btn btn-primary" type="submit">{busyAdd ? (<><span className="spinner" /> Adding...</>) : 'Add Payee'}</button>
 </form>
 </div>
 )}

 {active === 'history' && (
 <div>
 {fModeEnabled ? (
   <>
     <div style={{ marginBottom: 16 }}>
       <h3 style={{ display: 'flex', alignItems: 'center', gap: 8, margin: 0 }}>
         <span><LinkIcon size={18} color="currentColor" /></span> On-Chain Transactions
       </h3>
       <p className="small" style={{ color: 'var(--text-hint-subtle)', marginTop: 4 }}>
         Real blockchain transactions from Sepolia testnet - click any to verify on Etherscan
       </p>
     </div>
     {walletAddress ? (
       <CryptoTransactionHistory address={walletAddress} refreshTrigger={txRefreshTrigger} />
     ) : (
       <div className="card" style={{ textAlign: 'center', padding: 24 }}>
         <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 12, color: 'var(--muted, #6b7280)' }}><Wallet size={36} color="currentColor" /></div>
         <p>Connect your wallet to view on-chain transaction history</p>
         <button className="btn btn-primary" onClick={() => setActive('pay')} style={{ marginTop: 12 }}>
           Go to Crypto Transfer
         </button>
       </div>
     )}
   </>
   ) : (
           <>
             {loading && <PageLoader />}
             {!loading && txList.length === 0 && <p>No transactions yet.</p>}

             {/* Row 1 — Action bar: sparkline left, export right, no wrapping */}
             {/* Row 2 — Filter bar: all filter controls, horizontal scroll on mobile */}
             {!loading && txList.filter(t => t.currency !== 'FTK').length > 0 && (
               <>
                 <div className="txh-toolbar-row1">
                   <div className="txh-sparkline">
                     <div className="txh-sparkline-label">Volume (30d)</div>
                     <ResponsiveContainer width="100%" height={56}>
                       <AreaChart data={sparklineData} margin={{ top: 0, right: 0, left: 0, bottom: 0 }}>
                         <defs>
                           <linearGradient id="sparklineGradient" x1="0" y1="0" x2="0" y2="1">
                             <stop offset="0%" stopColor={CHART_COLORS.primary} stopOpacity={0.1} />
                             <stop offset="100%" stopColor={CHART_COLORS.primary} stopOpacity={0} />
                           </linearGradient>
                         </defs>
                         <Area
                           type="monotone"
                           dataKey="count"
                           stroke={CHART_COLORS.primary}
                           strokeWidth={1.5}
                           fill="url(#sparklineGradient)"
                         />
                       </AreaChart>
                     </ResponsiveContainer>
                   </div>
                   <div ref={exportRef} className="txh-export-wrap">
                     <button
                       type="button"
                       onClick={() => setExportMenuOpen(o => !o)}
                       disabled={filteredFiatTransactions.length === 0}
                       className="txh-export-btn"
                       aria-haspopup="true"
                       aria-expanded={exportMenuOpen}
                     >
                       Export <ChevronDown size={14} />
                     </button>
                     {exportMenuOpen && (
                       <div className="txh-export-menu">
                         <button
                           type="button"
                           onClick={() => handleExport('csv')}
                           className="txh-export-item"
                         >
                           Download CSV
                         </button>
                         <button
                           type="button"
                           onClick={() => handleExport('pdf')}
                           className="txh-export-item"
                         >
                           Download PDF
                         </button>
                       </div>
                     )}
                   </div>
                 </div>
                 <div className="txh-toolbar-row2">
                   <input
                     type="text"
                     placeholder="Search description..."
                     value={historySearch}
                     onChange={e => setHistorySearch((e.target as HTMLInputElement).value)}
                     className="txh-input txh-input--search"
                   />
                   <div className="txh-date-range">
                     <input
                       type="date"
                       value={historyFromDate}
                       onChange={e => setHistoryFromDate((e.target as HTMLInputElement).value)}
                       className="txh-input txh-input--date"
                       title="From date"
                       aria-label="From date"
                     />
                     <span className="txh-date-sep">to</span>
                     <input
                       type="date"
                       value={historyToDate}
                       onChange={e => setHistoryToDate((e.target as HTMLInputElement).value)}
                       className="txh-input txh-input--date"
                       title="To date"
                       aria-label="To date"
                     />
                   </div>
                   <select
                     value={historyType}
                     onChange={e => setHistoryType((e.target as HTMLSelectElement).value as 'all' | 'credit' | 'debit')}
                     className="txh-select"
                   >
                     <option value="all">All Types</option>
                     <option value="credit">Credit</option>
                     <option value="debit">Debit</option>
                   </select>
                   <select
                     value={historySpending}
                     onChange={e => setHistorySpending((e.target as HTMLSelectElement).value)}
                     className="txh-select"
                   >
                     <option value="">All Spending Types</option>
                     <option value="Fun">Fun</option>
                     <option value="Fixed">Fixed</option>
                     <option value="Future">Future</option>
                   </select>
                   <select
                     value={`${historySortField}_${historySortDir}`}
                     onChange={e => {
                       const [field, dir] = (e.target as HTMLSelectElement).value.split('_');
                       setHistorySortField(field as 'createdAt' | 'amount');
                       setHistorySortDir(dir as 'asc' | 'desc');
                     }}
                     className="txh-select"
                   >
                     <option value="createdAt_desc">Date (newest first)</option>
                     <option value="createdAt_asc">Date (oldest first)</option>
                     <option value="amount_desc">Amount (high to low)</option>
                     <option value="amount_asc">Amount (low to high)</option>
                   </select>
                 </div>
               </>
             )}

         {/* Sortable/filterable table */}
                 {!loading && (() => {
                   if (filteredFiatTransactions.length === 0 && txList.filter(t => t.currency !== 'FTK').length > 0) {
                     return <p className="txh-empty">No transactions match the current filters.</p>;
                   }

           return (
             <div className="txh-table-wrap">
               <table className="txh-table">
                 <thead>
                   <tr>
                     <th>Date</th>
                     <th>Description</th>
                     <th>Account</th>
                     <th>Type</th>
                     <th>Status</th>
                     <th>Category</th>
                     <th className="txh-col-right">Amount</th>
                   </tr>
                 </thead>
                 <tbody>
                   {filteredFiatTransactions.map((t) => (
                     <tr key={t.id}>
                       <td className="txh-cell-date">{formatDate(t.createdAt)}</td>
                       <td>{t.description || '—'}</td>
                       <td className="txh-cell-mono">{accountNumberFromId(t.accountId)}</td>
                       <td>
                         <span className={`txh-badge ${t.type === 'credit' ? 'txh-badge--credit' : 'txh-badge--debit'}`}>
                           {t.type.toUpperCase()}
                         </span>
                       </td>
                       <td>
                         {(() => {
                           const status = t.status ?? 'Completed';
                           return (
                             <span className={`txh-pill txh-pill--${status.toLowerCase()}`}>
                               {status}
                             </span>
                           );
                         })()}
                       </td>
                       <td>
                         {t.spendingType ? (
                           <span className={`txh-cat txh-cat--${t.spendingType.toLowerCase()}`}>
                             {t.spendingType}
                           </span>
                         ) : '—'}
                       </td>
                       <td className={`txh-col-right txh-amount ${t.type === 'credit' ? 'txh-amount--credit' : 'txh-amount--debit'}`}>
                         {t.type === 'credit' ? '+' : '-'}{nzd.format(t.amount)}
                       </td>
                     </tr>
                   ))}
                 </tbody>
               </table>
             </div>
           );
         })()}
         <Pagination pagination={pagination} />
     </>
   )}
   </div>
   )}
   </div>
   );
 };

 export default Transactions;



