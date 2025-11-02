import * as React from 'react';
import { useToast } from '../components/Toast';
import { authFetch } from '../auth';

interface Transaction {
 id: string;
 accountId: string;
 amount: number;
 currency: string;
 type: 'credit' | 'debit';
 description: string;
 createdAt: string;
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

 const { success, error: toastError, info } = useToast();

 // Payee + payment state
 const [payeeForm, setPayeeForm] = React.useState({ name: '', accountNumber: '', userId: '' });
 const [paymentForm, setPaymentForm] = React.useState({ accountId: '', amount: '', payeeId: '', description: '' });

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
 const payee = payees.find(p => p.id === paymentForm.payeeId);
 const payload = { accountId: paymentForm.accountId, amount: parseFloat(paymentForm.amount), payeeName: payee?.name, payeeAccountNumber: payee?.accountNumber, description: paymentForm.description };
 const res = await authFetch('/payments', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
 if (!res.ok) {
 setBusySend(false);
 setError('Failed to send payment');
 return toastError('Failed to send payment');
 }
 await Promise.all([fetchTransactions(), fetchAccounts()]);
 setPaymentForm(p => ({ ...p, amount: '', description: '' }));
 setActive('history');
 setBusySend(false);
 success('Payment sent');
 };

 const TabButton = ({ id, children }: { id: Tab, children: React.ReactNode }) => (
 <button className={"btn " + (active === id ? 'btn-primary' : 'btn-secondary')} style={{ marginRight:8 }} onClick={() => setActive(id)} type="button">{children}</button>
 );

 const accountNumberFromId = (id: string) => accounts.find(a => a.id === id)?.accountNumber ?? '?';

 return (
 <div>
 <h2>Transactions</h2>

 <div style={{ marginBottom:12 }}>
 <TabButton id="pay">Send Money</TabButton>
 <TabButton id="payee">Add Payee</TabButton>
 <TabButton id="history">History</TabButton>
 </div>

 {error && <p style={{ color: 'red' }}>{error}</p>}

 {active === 'pay' && (
 <div className="card">
 <h3>Send Money</h3>
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
 <label htmlFor="amount">Amount (NZD)</label>
 <input id="amount" type="number" step="0.01" min="0.01" value={paymentForm.amount} onChange={e => setPaymentForm({ ...paymentForm, amount: (e.target as HTMLInputElement).value })} required />
 </div>
 <div className="form-group">
 <label htmlFor="desc">Description</label>
 <input id="desc" type="text" value={paymentForm.description} onChange={e => setPaymentForm({ ...paymentForm, description: (e.target as HTMLInputElement).value })} placeholder="Optional description" />
 </div>
 <button className="btn btn-primary" type="submit" disabled={!accounts.length || !payees.length}>{busySend ? (<><span className="spinner" /> Sending...</>) : 'Send'}</button>
 </form>
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
 {loading && <p>Loading...</p>}
 {!loading && transactions.length ===0 && <p>No transactions yet.</p>}
 {transactions.map((transaction) => (
 <div key={transaction.id} className="card">
 <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
 <div>
 <h4>{transaction.description}</h4>
 <p><small>Transaction ID: {transaction.id}</small></p>
 <p>Account: {accountNumberFromId(transaction.accountId)}</p>
 <p>Date: {formatDate(transaction.createdAt)}</p>
 </div>
 <div style={{ textAlign: 'right' }}>
 <p style={{ color: transaction.type === 'credit' ? 'green' : 'red', fontSize: '1.2em', fontWeight: 'bold' }}>
 {transaction.type === 'credit' ? '+' : '-'}{nzd.format(transaction.amount)}
 </p>
 <span style={{ padding: '2px8px', borderRadius: '4px', backgroundColor: transaction.type === 'credit' ? '#d4edda' : '#f8d7da', color: transaction.type === 'credit' ? '#155724' : '#721c24' }}>
 {transaction.type.toUpperCase()}
 </span>
 </div>
 </div>
 </div>
 ))}
 </div>
 )}
 </div>
 );
};

export default Transactions;