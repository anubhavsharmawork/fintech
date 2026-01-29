import * as React from 'react';
import { useToast } from '../components/Toast';
import { authFetch } from '../auth';
import { useFMode } from '../hooks/useFMode';
import ConnectWallet from '../components/ConnectWallet';
import CryptoAccountSwitcher from '../components/CryptoAccountSwitcher';
import CryptoModeBanner from '../components/CryptoModeBanner';
import CryptoTransactionHistory from '../components/CryptoTransactionHistory';
import TransactionStatus from '../components/TransactionStatus';
import { sendFTKTransfer, isValidAddress, estimateTransferGas, TransactionResult, GasEstimate } from '../services/crypto';

interface Transaction {
 id: string;
 accountId: string;
 amount: number;
 currency: string;
 type: 'credit' | 'debit';
 description: string;
 createdAt: string;
 spendingType?: string;
}

interface Account { id: string; accountNumber: string; accountType: string; balance?: number }
interface Payee { id: string; name: string; accountNumber: string }
interface UserLite { id: string; email: string; firstName: string; lastName: string }

type Tab = 'pay' | 'payee' | 'history';

const Transactions = () => {
 const [transactions, setTransactions] = React.useState<Transaction[]>([]);
 const [accounts, setAccounts] = React.useState<Account[]>([]);
 const [payees, setPayees] = React.useState<Payee[]>([]);
 const [users, setUsers] = React.useState<UserLite[]>([]);
 const [active, setActive] = React.useState<Tab>('pay');
 const [loading, setLoading] = React.useState(true);
 const [error, setError] = React.useState<string | null>(null);
 const [busySend, setBusySend] = React.useState(false);
 const [busyAdd, setBusyAdd] = React.useState(false);

 const { success, error: toastError } = useToast();

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

 React.useEffect(() => {
   if (fModeEnabled && active === 'payee') {
     setActive('pay');
   }
 }, [fModeEnabled, active]);

 const nzd = new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' });
 const formatDate = (dateString: string) => new Date(dateString).toLocaleDateString('en-NZ');

 const fetchTransactions = React.useCallback(async () => {
 setError(null);
 setLoading(true);
 try {
 const res = await authFetch('/transactions');
 if (!res.ok) throw new Error(`Failed to load transactions (${res.status})`);
 const data = await res.json();
 setTransactions(data);
 } catch (err: any) {
 setError(err.message || 'Failed to load transactions');
 toastError(err.message || 'Failed to load transactions');
 } finally {
 setLoading(false);
 }
 }, [toastError]);

 const fetchAccounts = React.useCallback(async () => {
 const res = await authFetch('/accounts');
 if (res.ok) {
 const data = await res.json();
 setAccounts(data);
 if (data.length && !paymentForm.accountId) setPaymentForm(p => ({ ...p, accountId: data[0].id }));
 }
 }, [paymentForm.accountId]);

 const fetchPayees = React.useCallback(async () => {
 const res = await authFetch('/payees');
 if (res.ok) {
 const data: Payee[] = await res.json();
 setPayees(data);
 const defaultPayee = data.find(p => /demo/i.test(p.name)) || data[0];
 if (defaultPayee && !paymentForm.payeeId) setPaymentForm(p => ({ ...p, payeeId: defaultPayee.id }));
 }
 }, [paymentForm.payeeId]);

 const fetchUsers = React.useCallback(async () => {
 const res = await authFetch('/users/all');
 if (res.ok) {
 const data: UserLite[] = await res.json();
 setUsers(data);
 }
 }, []);

 React.useEffect(() => {
 fetchAccounts();
 fetchPayees();
 fetchTransactions();
 fetchUsers();
 }, [fetchTransactions, fetchAccounts, fetchPayees, fetchUsers]);

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
 const res = await authFetch('/payees', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, accountNumber }) });
 if (!res.ok) {
 setBusyAdd(false);
 setError('Failed to add payee');
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
    setError(null);

    try {
      const amountValue = parseFloat(paymentForm.amount);
      if (Number.isNaN(amountValue) || amountValue <= 0) {
        throw new Error('Enter a valid amount greater than 0');
      }

      if (!spendingType) {
        throw new Error('Please select a Conscious Spending Type™');
      }

      const payee = payees.find(p => p.id === paymentForm.payeeId);
      if (!fModeEnabled && !payee) {
        throw new Error('Select a payee before sending');
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

      const res = await authFetch('/payments', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
      if (!res.ok) {
        throw new Error('Failed to send payment');
      }

      await Promise.all([fetchTransactions(), fetchAccounts()]);
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
      setError(message);
      toastError(message);
    } finally {
      setBusySend(false);
    }
  };

 const TabButton = ({ id, children }: { id: Tab, children: React.ReactNode }) => (
 <button className={"btn " + (active === id ? 'btn-primary' : 'btn-secondary')} style={{ marginRight:8 }} onClick={() => setActive(id)} type="button">{children}</button>
 );

 const accountNumberFromId = (id: string) => accounts.find(a => a.id === id)?.accountNumber ?? '?';

 return (
 <div>
 <h2>Transactions</h2>

 <div style={{ marginBottom:12 }}>
 <TabButton id="pay">{fModeEnabled ? 'Crypto Transfer' : 'Send Money'}</TabButton>
 {!fModeEnabled && <TabButton id="payee">Add Payee</TabButton>}
 <TabButton id="history">History</TabButton>
 </div>

 {error && <p style={{ color: 'red' }}>{error}</p>}

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
          {recipientError && <small style={{ color: '#ef4444' }}>{recipientError}</small>}
          {recipientAddress && isValidAddress(recipientAddress) && (
            <small style={{ color: '#22c55e' }}>✓ Valid address</small>
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
          {estimatingGas && <small style={{ color: '#888' }}>Estimating gas...</small>}
          {gasEstimate && !estimatingGas && (
            <small style={{ color: '#666' }}>
              Estimated gas: ~{parseFloat(gasEstimate.estimatedCostEth).toFixed(6)} ETH
            </small>
          )}
        </div>
        <div className="form-group">
          <label htmlFor="spendingType" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            Conscious Spending Type™
            <span className="info-tooltip" style={{ position: 'relative', display: 'inline-flex', cursor: 'help' }}>
              <span style={{ fontSize: '0.85rem', color: 'var(--muted)', border: '1px solid var(--muted)', borderRadius: '50%', width: '16px', height: '16px', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontWeight: 600 }}>i</span>
              <span className="tooltip-popup" style={{ position: 'absolute', bottom: '120%', left: '50%', transform: 'translateX(-50%)', background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '10px 12px', fontSize: '0.75rem', whiteSpace: 'nowrap', boxShadow: '0 4px 12px rgba(0,0,0,0.15)', zIndex: 100, display: 'none' }}>
                <strong style={{ display: 'block', marginBottom: '6px' }}>Categorize your spending:</strong>
                <span style={{ display: 'block', color: '#3b82f6' }}>✨ Fun: Discretionary spending</span>
                <span style={{ display: 'block', color: '#f97316' }}>🏠 Fixed: Bills, recurring costs</span>
                <span style={{ display: 'block', color: '#22c55e' }}>📈 Future: Savings & investments</span>
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
            <option value="Fun">✨ Fun</option>
            <option value="Fixed">🏠 Fixed</option>
            <option value="Future">📈 Future</option>
          </select>
        </div>
        <button className="btn btn-primary" type="submit" disabled={isDemoMode || !walletSigner || busySend || !!recipientError}>{isDemoMode ? '👁️ Demo Mode (View Only)' : busySend ? (<><span className="spinner" /> Processing...</>) : 'Transfer FTK'}</button>
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
     Conscious Spending Type™
     <span className="info-tooltip" style={{ position: 'relative', display: 'inline-flex', cursor: 'help' }}>
       <span style={{ fontSize: '0.85rem', color: 'var(--muted)', border: '1px solid var(--muted)', borderRadius: '50%', width: '16px', height: '16px', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontWeight: 600 }}>i</span>
       <span className="tooltip-popup" style={{ position: 'absolute', bottom: '120%', left: '50%', transform: 'translateX(-50%)', background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '10px 12px', fontSize: '0.75rem', whiteSpace: 'nowrap', boxShadow: '0 4px 12px rgba(0,0,0,0.15)', zIndex: 100, display: 'none' }}>
         <strong style={{ display: 'block', marginBottom: '6px' }}>Categorize your spending:</strong>
         <span style={{ display: 'block', color: '#3b82f6' }}>✨ Fun: Discretionary spending</span>
         <span style={{ display: 'block', color: '#f97316' }}>🏠 Fixed: Bills, recurring costs</span>
         <span style={{ display: 'block', color: '#22c55e' }}>📈 Future: Savings & investments</span>
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
     <option value="Fun">✨ Fun</option>
     <option value="Fixed">🏠 Fixed</option>
     <option value="Future">📈 Future</option>
   </select>
   </div>
  <button className="btn btn-primary" type="submit" disabled={!accounts.length || (!fModeEnabled && !payees.length) || busySend}>{busySend ? (<><span className="spinner" /> Sending...</>) : 'Send'}</button>
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
         <span>⛓️</span> On-Chain Transactions
       </h3>
       <p className="small" style={{ color: '#666', marginTop: 4 }}>
         Real blockchain transactions from Sepolia testnet - click any to verify on Etherscan
       </p>
     </div>
     {walletAddress ? (
       <CryptoTransactionHistory address={walletAddress} refreshTrigger={txRefreshTrigger} />
     ) : (
       <div className="card" style={{ textAlign: 'center', padding: 24 }}>
         <div style={{ fontSize: '2rem', marginBottom: 12 }}>👛</div>
         <p>Connect your wallet to view on-chain transaction history</p>
         <button className="btn btn-primary" onClick={() => setActive('pay')} style={{ marginTop: 12 }}>
           Go to Crypto Transfer
         </button>
       </div>
     )}
   </>
 ) : (
   <>
     {loading && <p>Loading...</p>}
     {!loading && transactions.length === 0 && <p>No transactions yet.</p>}
     {!loading && transactions.filter(t => t.currency !== 'FTK').length === 0 && transactions.length > 0 && (
       <p style={{ fontStyle: 'italic', color: 'var(--muted)' }}>No Fiat transactions yet.</p>
     )}
     {transactions.filter(t => t.currency !== 'FTK').map((transaction) => (
       <div key={transaction.id} className="card">
         <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
           <div>
             <h4>{transaction.description}</h4>
             <p><small>Transaction ID: {transaction.id}</small></p>
             <p>Account: {accountNumberFromId(transaction.accountId)}</p>
             <p>Date: {formatDate(transaction.createdAt)}</p>
             {transaction.spendingType && (
               <p><span style={{ padding: '2px 8px', borderRadius: '4px', backgroundColor: transaction.spendingType === 'Fun' ? '#e0f2fe' : transaction.spendingType === 'Fixed' ? '#fef3c7' : '#d1fae5', color: transaction.spendingType === 'Fun' ? '#0369a1' : transaction.spendingType === 'Fixed' ? '#b45309' : '#047857', fontWeight: 500, fontSize: '0.85em' }}>
                 {transaction.spendingType}
               </span></p>
             )}
           </div>
           <div style={{ textAlign: 'right' }}>
             <p style={{ color: transaction.type === 'credit' ? 'green' : 'red', fontSize: '1.2em', fontWeight: 'bold' }}>
               {transaction.type === 'credit' ? '+' : '-'}{nzd.format(transaction.amount)}
             </p>
             <span style={{ padding: '2px 8px', borderRadius: '4px', backgroundColor: transaction.type === 'credit' ? '#d4edda' : '#f8d7da', color: transaction.type === 'credit' ? '#155724' : '#721c24' }}>
               {transaction.type.toUpperCase()}
             </span>
           </div>
         </div>
       </div>
     ))}
   </>
 )}
 </div>
 )}
 </div>
 );
};

export default Transactions;