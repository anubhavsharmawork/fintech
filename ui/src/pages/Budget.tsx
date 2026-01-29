import * as React from 'react';
import { authFetch } from '../auth';
import { useToast } from '../components/Toast';
import { useFMode } from '../hooks/useFMode';

interface BudgetAggregationDto {
  fun: number;
  fixed: number;
  future: number;
  total: number;
  period: { from: string; to: string };
}

interface Account { id: string; accountNumber: string; accountType: string }

interface BudgetGoal {
  id: string;
  name: string;
  description: string;
  fixed: number;
  future: number;
  fun: number;
  badge?: string;
  riskLabel?: string;
}

const BUDGET_GOALS: BudgetGoal[] = [
  {
    id: 'wealth',
    name: 'Build Wealth',
    description: 'Conservative approach focused on long-term growth',
    fixed: 50,
    future: 35,
    fun: 15,
    riskLabel: 'Low-Medium Risk',
  },
  {
    id: 'balanced',
    name: 'Balanced Living',
    description: 'A sustainable mix of saving and enjoying life',
    fixed: 55,
    future: 20,
    fun: 25,
    badge: 'Default',
  },
  {
    id: 'lifestyle',
    name: 'Enjoy Life',
    description: 'Lifestyle-focused with room for experiences',
    fixed: 55,
    future: 15,
    fun: 30,
  },
  {
    id: 'growth',
    name: 'Rapid Growth',
    description: 'Aggressive savings for accelerated wealth building',
    fixed: 50,
    future: 35,
    fun: 15,
    riskLabel: 'High Risk',
  },
  {
    id: 'recovery',
    name: 'Recovery Mode',
    description: 'Transition period to rebuild financial stability',
    fixed: 55,
    future: 25,
    fun: 20,
    badge: 'Recommended',
  },
];

type BudgetStep = 'goal' | 'income' | 'view';

const Budget = () => {
  const { error, success, info } = useToast();
  const { enabled: fModeEnabled } = useFMode();
  const [budget, setBudget] = React.useState<BudgetAggregationDto | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [from, setFrom] = React.useState(() => {
    const d = new Date();
    d.setDate(d.getDate() - 30);
    return d.toISOString().substring(0, 10);
  });
  const [to, setTo] = React.useState(() => new Date().toISOString().substring(0, 10));
  const [accounts, setAccounts] = React.useState<Account[]>([]);
  const [accountId, setAccountId] = React.useState<string>('');
  
  // Persist budget preferences in localStorage
  const [step, setStep] = React.useState<BudgetStep>(() => {
    const saved = localStorage.getItem('budgetStep');
    return (saved === 'goal' || saved === 'income' || saved === 'view') ? saved : 'goal';
  });
  
  const [selectedGoal, setSelectedGoal] = React.useState<string>(() => {
    return localStorage.getItem('budgetGoal') || 'balanced';
  });
  
  const [monthlyIncome, setMonthlyIncome] = React.useState<string>(() => {
    return localStorage.getItem('budgetIncome') || '';
  });
  
  const [showCustomize, setShowCustomize] = React.useState(false);
  
  const [customFixed, setCustomFixed] = React.useState(() => {
    const saved = localStorage.getItem('budgetCustomFixed');
    return saved ? parseInt(saved) : 55;
  });
  
  const [customFuture, setCustomFuture] = React.useState(() => {
    const saved = localStorage.getItem('budgetCustomFuture');
    return saved ? parseInt(saved) : 20;
  });
  
  const [customFun, setCustomFun] = React.useState(() => {
    const saved = localStorage.getItem('budgetCustomFun');
    return saved ? parseInt(saved) : 25;
  });

  // Save to localStorage whenever these values change
  React.useEffect(() => {
    localStorage.setItem('budgetStep', step);
  }, [step]);

  React.useEffect(() => {
    localStorage.setItem('budgetGoal', selectedGoal);
  }, [selectedGoal]);

  React.useEffect(() => {
    localStorage.setItem('budgetIncome', monthlyIncome);
  }, [monthlyIncome]);

  React.useEffect(() => {
    localStorage.setItem('budgetCustomFixed', customFixed.toString());
    localStorage.setItem('budgetCustomFuture', customFuture.toString());
    localStorage.setItem('budgetCustomFun', customFun.toString());
  }, [customFixed, customFuture, customFun]);

  const fetchAccounts = React.useCallback(async () => {
    const res = await authFetch('/accounts');
    if (!res.ok) return;
    const data: Account[] = await res.json();
    setAccounts(data);
    if (data.length && !accountId) setAccountId(data[0].id);
  }, [accountId]);

    const loadBudget = React.useCallback(async () => {
    if (!accountId) return;
    setLoading(true);
    try {
      const params = new URLSearchParams({
        accountId,
        from: new Date(from).toISOString(),
        to: new Date(to).toISOString()
      });
      const res = await authFetch(`/budget/budget?${params.toString()}`);
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Failed to load budget (${res.status})`);
      }
      const data = await res.json();
      setBudget(data);
    } catch (err: any) {
      error(err.message || 'Failed to load budget');
      setBudget(null);
    } finally {
      setLoading(false);
    }
  }, [accountId, from, to, error]);

  React.useEffect(() => {
    fetchAccounts();
  }, [fetchAccounts]);

  React.useEffect(() => {
    loadBudget();
  }, [loadBudget]);

  const currentGoal = BUDGET_GOALS.find(g => g.id === selectedGoal) || BUDGET_GOALS[1];
  const activeFixed = showCustomize ? customFixed : currentGoal.fixed;
  const activeFuture = showCustomize ? customFuture : currentGoal.future;
  const activeFun = showCustomize ? customFun : currentGoal.fun;

  const handleGoalSelect = (goalId: string) => {
    setSelectedGoal(goalId);
    const goal = BUDGET_GOALS.find(g => g.id === goalId);
    if (goal) {
      setCustomFixed(goal.fixed);
      setCustomFuture(goal.future);
      setCustomFun(goal.fun);
      info(`Selected "${goal.name}" budget goal`);
    }
  };

  const handleNextToIncome = () => {
    success(`Budget goal set to "${currentGoal.name}"`);
    setStep('income');
  };

  const handleIncomeSubmit = () => {
    const income = parseFloat(monthlyIncome);
    if (isNaN(income) || income <= 0) {
      error('Please enter a valid monthly income');
      return;
    }
    success(`Budget plan created! Fixed: $${(income * activeFixed / 100).toFixed(0)}, Future: $${(income * activeFuture / 100).toFixed(0)}, Fun: $${(income * activeFun / 100).toFixed(0)}`);
    setStep('view');
  };

  const handleCustomSliderChange = (type: 'fixed' | 'future' | 'fun', value: number) => {
    const remaining = 100 - value;
    if (type === 'fixed') {
      setCustomFixed(value);
      const ratio = customFuture + customFun > 0 ? customFuture / (customFuture + customFun) : 0.5;
      setCustomFuture(Math.round(remaining * ratio));
      setCustomFun(remaining - Math.round(remaining * ratio));
    } else if (type === 'future') {
      setCustomFuture(value);
      const ratio = customFixed + customFun > 0 ? customFixed / (customFixed + customFun) : 0.5;
      setCustomFixed(Math.round(remaining * ratio));
      setCustomFun(remaining - Math.round(remaining * ratio));
    } else {
      setCustomFun(value);
      const ratio = customFixed + customFuture > 0 ? customFixed / (customFixed + customFuture) : 0.5;
      setCustomFixed(Math.round(remaining * ratio));
      setCustomFuture(remaining - Math.round(remaining * ratio));
    }
  };

  const total = budget?.total ?? 0;
  const funPct = total > 0 ? (budget!.fun / total) * 100 : 0;
  const fixedPct = total > 0 ? (budget!.fixed / total) * 100 : 0;

  const gradient = `conic-gradient(#3b82f6 0 ${funPct}%, #f97316 ${funPct}% ${funPct + fixedPct}%, #22c55e ${funPct + fixedPct}% 100%)`;

  const renderGoalSelection = () => (
    <div className="card">
      <h2 style={{ marginBottom: '0.5rem' }}>Select Your Budget Goal</h2>
      <p className="small" style={{ marginBottom: '1.5rem' }}>
        Choose a spending strategy that fits your lifestyle and financial priorities.
      </p>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '1.5rem' }}>
        {BUDGET_GOALS.map((goal) => (
          <button
            key={goal.id}
            type="button"
            onClick={() => handleGoalSelect(goal.id)}
            style={{
              display: 'flex',
              alignItems: 'flex-start',
              gap: '12px',
              padding: '16px',
              border: selectedGoal === goal.id ? '2px solid var(--primary)' : '1px solid var(--border)',
              borderRadius: '12px',
              background: selectedGoal === goal.id ? 'rgba(37, 99, 235, 0.04)' : 'var(--card)',
              cursor: 'pointer',
              textAlign: 'left',
              transition: 'all 0.2s ease',
              boxShadow: selectedGoal === goal.id ? '0 4px 12px rgba(37, 99, 235, 0.15)' : 'none',
            }}
            aria-pressed={selectedGoal === goal.id}
          >
            <span
              style={{
                width: '20px',
                height: '20px',
                borderRadius: '50%',
                border: selectedGoal === goal.id ? '6px solid var(--primary)' : '2px solid var(--muted)',
                flexShrink: 0,
                marginTop: '2px',
                transition: 'all 0.2s ease',
              }}
              aria-hidden="true"
            />
            <div style={{ flex: 1 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                <span style={{ fontWeight: 600, fontSize: '1rem', color: 'var(--text)' }}>
                  {goal.name}
                </span>
                {goal.badge && (
                  <span
                    style={{
                      fontSize: '0.7rem',
                      padding: '2px 8px',
                      borderRadius: '999px',
                      background: goal.badge === 'Default' ? 'rgba(37, 99, 235, 0.1)' : 'rgba(16, 185, 129, 0.1)',
                      color: goal.badge === 'Default' ? 'var(--primary)' : '#059669',
                      fontWeight: 600,
                    }}
                  >
                    {goal.badge}
                  </span>
                )}
                {goal.riskLabel && (
                  <span
                    style={{
                      fontSize: '0.65rem',
                      padding: '2px 6px',
                      borderRadius: '999px',
                      background: goal.riskLabel === 'High Risk' ? 'rgba(239, 68, 68, 0.1)' : 'rgba(156, 163, 175, 0.15)',
                      color: goal.riskLabel === 'High Risk' ? '#dc2626' : '#6b7280',
                      fontWeight: 500,
                    }}
                  >
                    {goal.riskLabel}
                  </span>
                )}
              </div>
              <p style={{ margin: 0, fontSize: '0.85rem', color: 'var(--muted)', marginBottom: '8px' }}>
                {goal.description}
              </p>
              <div style={{ display: 'flex', gap: '16px', fontSize: '0.8rem', fontWeight: 500 }}>
                <span style={{ color: '#f97316' }}>🏠 {goal.fixed}% Fixed</span>
                <span style={{ color: '#22c55e' }}>📈 {goal.future}% Future</span>
                <span style={{ color: '#3b82f6' }}>✨ {goal.fun}% Fun</span>
              </div>
            </div>
          </button>
        ))}
      </div>

      {showCustomize && (
        <div style={{ padding: '16px', background: '#f8fafc', borderRadius: '12px', marginBottom: '1.5rem' }}>
          <h4 style={{ margin: '0 0 12px 0', fontSize: '0.95rem' }}>Customize Your Allocation</h4>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <div>
              <label style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', marginBottom: '4px' }}>
                <span>🏠 Fixed Expenses</span>
                <span style={{ fontWeight: 600 }}>{customFixed}%</span>
              </label>
              <input
                type="range"
                min="30"
                max="80"
                value={customFixed}
                onChange={(e) => handleCustomSliderChange('fixed', parseInt(e.target.value))}
                style={{ width: '100%', accentColor: '#f97316' }}
              />
            </div>
            <div>
              <label style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', marginBottom: '4px' }}>
                <span>📈 Future Savings</span>
                <span style={{ fontWeight: 600 }}>{customFuture}%</span>
              </label>
              <input
                type="range"
                min="5"
                max="50"
                value={customFuture}
                onChange={(e) => handleCustomSliderChange('future', parseInt(e.target.value))}
                style={{ width: '100%', accentColor: '#22c55e' }}
              />
            </div>
            <div>
              <label style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', marginBottom: '4px' }}>
                <span>✨ Fun Money</span>
                <span style={{ fontWeight: 600 }}>{customFun}%</span>
              </label>
              <input
                type="range"
                min="5"
                max="40"
                value={customFun}
                onChange={(e) => handleCustomSliderChange('fun', parseInt(e.target.value))}
                style={{ width: '100%', accentColor: '#3b82f6' }}
              />
            </div>
          </div>
          <p className="small" style={{ marginTop: '8px', marginBottom: 0 }}>
            Total: {customFixed + customFuture + customFun}% (must equal 100%)
          </p>
        </div>
      )}

      <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
        <button className="btn btn-primary" onClick={handleNextToIncome}>
          Next: Enter Income →
        </button>
        <button
          className="btn btn-secondary"
          onClick={() => setShowCustomize(!showCustomize)}
          style={{ background: showCustomize ? 'var(--primary)' : '#374151' }}
        >
          {showCustomize ? 'Hide Customize' : 'Customize (Advanced)'}
        </button>
      </div>
    </div>
  );

  const renderIncomeStep = () => {
    const income = parseFloat(monthlyIncome) || 0;
    return (
      <div className="card">
        <h2 style={{ marginBottom: '0.5rem' }}>Enter Your Monthly Income</h2>
        <p className="small" style={{ marginBottom: '1.5rem' }}>
          Based on your "{currentGoal.name}" goal, here's how your income will be allocated.
        </p>

        <div className="form-group" style={{ marginBottom: '1.5rem' }}>
          <label htmlFor="income">Monthly Take-Home Income ($)</label>
          <input
            id="income"
            type="number"
            placeholder="e.g., 5000"
            value={monthlyIncome}
            onChange={(e) => setMonthlyIncome(e.target.value)}
            min="0"
            step="100"
          />
        </div>

        {income > 0 && (
          <div style={{ padding: '16px', background: '#f8fafc', borderRadius: '12px', marginBottom: '1.5rem' }}>
            <h4 style={{ margin: '0 0 12px 0', fontSize: '0.95rem' }}>Your Budget Breakdown</h4>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 12px', background: 'rgba(249, 115, 22, 0.1)', borderRadius: '8px' }}>
                <span>🏠 Fixed Expenses ({activeFixed}%)</span>
                <span style={{ fontWeight: 600, color: '#f97316' }}>${(income * activeFixed / 100).toFixed(2)}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 12px', background: 'rgba(34, 197, 94, 0.1)', borderRadius: '8px' }}>
                <span>📈 Future Savings ({activeFuture}%)</span>
                <span style={{ fontWeight: 600, color: '#22c55e' }}>${(income * activeFuture / 100).toFixed(2)}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 12px', background: 'rgba(59, 130, 246, 0.1)', borderRadius: '8px' }}>
                <span>✨ Fun Money ({activeFun}%)</span>
                <span style={{ fontWeight: 600, color: '#3b82f6' }}>${(income * activeFun / 100).toFixed(2)}</span>
              </div>
            </div>
          </div>
        )}

        <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
          <button className="btn" style={{ background: '#e5e7eb', color: '#374151' }} onClick={() => setStep('goal')}>
            ← Back to Goals
          </button>
          <button className="btn btn-primary" onClick={handleIncomeSubmit} disabled={!monthlyIncome}>
            Create Budget Plan →
          </button>
        </div>
      </div>
    );
  };

  return (
    <>
      {step === 'goal' && renderGoalSelection()}
      {step === 'income' && renderIncomeStep()}
      {step === 'view' && (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '12px', marginBottom: '1rem' }}>
            <h2 style={{ margin: 0 }}>Budget</h2>
            <button
              className="btn"
              style={{ background: '#e5e7eb', color: '#374151', padding: '8px 16px', fontSize: '0.875rem' }}
              onClick={() => setStep('goal')}
            >
              ← Change Goal
            </button>
          </div>

          <div style={{ padding: '12px 16px', background: 'rgba(37, 99, 235, 0.05)', borderRadius: '10px', marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
            <span style={{ fontWeight: 600 }}>Current Goal: {currentGoal.name}</span>
            <span className="small">
              {activeFixed}% Fixed | {activeFuture}% Future | {activeFun}% Fun
            </span>
            {monthlyIncome && (
              <span className="small" style={{ marginLeft: 'auto' }}>
                Monthly Income: ${parseFloat(monthlyIncome).toLocaleString()}
              </span>
            )}
          </div>

          {fModeEnabled ? (
            <div style={{ textAlign: 'center', padding: '2rem 0' }}>
              <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>📈</div>
              <p>Budgeting is currently focused on Fiat accounts.</p>
              <p className="small">Switch to Fiat mode to view your conscious spending breakdown.</p>
            </div>
          ) : (
            <>
              <div className="form-row" style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                <div className="form-group">
                  <label htmlFor="account">Account</label>
                  <select
                    id="account"
                    value={accountId}
                    onChange={e => setAccountId((e.target as HTMLSelectElement).value)}
                  >
                    {accounts.map(a => (
                      <option key={a.id} value={a.id}>{a.accountType} - {a.accountNumber}</option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label htmlFor="from">From</label>
                  <input id="from" type="date" value={from} onChange={e => setFrom((e.target as HTMLInputElement).value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="to">To</label>
                  <input id="to" type="date" value={to} onChange={e => setTo((e.target as HTMLInputElement).value)} />
                </div>
                <div className="form-group" style={{ alignSelf: 'flex-end' }}>
                  <button className="btn btn-primary" type="button" onClick={loadBudget} disabled={loading || !accountId}>
                    {loading ? 'Loading...' : 'Refresh'}
                  </button>
                </div>
              </div>

              {budget && (
                <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', alignItems: 'center' }}>
                  <div style={{ width: 180, height: 180, borderRadius: '50%', background: gradient, position: 'relative', boxShadow: '0 0 0 8px #f7f7f7' }} aria-label="Budget breakdown pie chart">
                    {total === 0 && (
                      <div style={{ position: 'absolute', inset: 0, display: 'grid', placeItems: 'center', fontSize: 14 }}>
                        No data
                      </div>
                    )}
                  </div>
                  <div>
                    <h4>Totals</h4>
                    <ul>
                      <li><strong>Fun:</strong> {budget.fun.toFixed(2)}</li>
                      <li><strong>Fixed:</strong> {budget.fixed.toFixed(2)}</li>
                      <li><strong>Future:</strong> {budget.future.toFixed(2)}</li>
                      <li><strong>Total:</strong> {budget.total.toFixed(2)}</li>
                    </ul>
                    <div className="small">Period: {new Date(budget.period.from).toLocaleDateString()} - {new Date(budget.period.to).toLocaleDateString()}</div>
                  </div>
                </div>
              )}

              {!budget && !loading && (
                <p>No budget data.</p>
              )}
            </>
          )}
        </div>
      )}
    </>
  );
};

export default Budget;

