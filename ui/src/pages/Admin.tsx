import React from 'react';
import { Info, Check, Minus } from 'lucide-react';
import { decodeJwt } from '../auth';

interface Permission {
  label: string;
  granted: boolean;
}

interface RoleDefinition {
  name: string;
  context: string;
  description: string;
  permissions: Permission[];
}

const ROLES: RoleDefinition[] = [
  {
    name: 'Super Admin',
    context: 'Platform Owner / CTO',
    description: 'Full access to all modules, user management, audit logs, system config.',
    permissions: [
      { label: 'Full access to all modules', granted: true },
      { label: 'User management', granted: true },
      { label: 'Audit logs', granted: true },
      { label: 'System configuration', granted: true },
      { label: 'Approve transfers', granted: true },
      { label: 'Delete accounts', granted: true },
      { label: 'Manage sanctions list', granted: true },
      { label: 'Approve credit requests', granted: true },
    ],
  },
  {
    name: 'Compliance Officer',
    context: 'Regulator / Auditor (read-only)',
    description: 'Read transactions, accounts, audit logs, sanctions; cannot write or approve.',
    permissions: [
      { label: 'Read transactions', granted: true },
      { label: 'Read accounts', granted: true },
      { label: 'Read audit logs', granted: true },
      { label: 'View sanctions list', granted: true },
      { label: 'View credit requests', granted: true },
      { label: 'Write or edit records', granted: false },
      { label: 'Approve transfers', granted: false },
      { label: 'Approve credit requests', granted: false },
    ],
  },
  {
    name: 'Account Manager',
    context: 'Relationship Manager / Banker',
    description: 'View & edit accounts, view transactions, process credit requests.',
    permissions: [
      { label: 'View accounts', granted: true },
      { label: 'Edit accounts', granted: true },
      { label: 'View transactions', granted: true },
      { label: 'Submit credit requests', granted: true },
      { label: 'View sanctions list', granted: true },
      { label: 'Delete accounts', granted: false },
      { label: 'Approve transfers', granted: false },
      { label: 'Approve credit requests', granted: false },
    ],
  },
  {
    name: 'Analyst',
    context: 'Financial Analyst / Data Team',
    description: 'Read-only on transactions, budgets, credit data; can export reports.',
    permissions: [
      { label: 'Read transactions', granted: true },
      { label: 'Read budgets', granted: true },
      { label: 'Export reports', granted: true },
      { label: 'View credit requests', granted: true },
      { label: 'Edit accounts', granted: false },
      { label: 'Approve transfers', granted: false },
      { label: 'Approve credit requests', granted: false },
    ],
  },
  {
    name: 'Customer (Standard User)',
    context: 'Retail Banking Customer',
    description: 'View own accounts/transactions, create budgets, submit credit requests.',
    permissions: [
      { label: 'View own accounts', granted: true },
      { label: 'View own transactions', granted: true },
      { label: 'Create budget entries', granted: true },
      { label: 'Submit credit requests', granted: true },
      { label: 'View other users', granted: false },
      { label: 'View sanctions list', granted: false },
      { label: 'Admin access', granted: false },
    ],
  },
  {
    name: 'Demo User',
    context: 'Unauthenticated Demo / Prospective Client',
    description: 'Read-only access to all pages, all write operations disabled.',
    permissions: [
      { label: 'Read-only page access', granted: true },
      { label: 'View sample data', granted: true },
      { label: 'View sanctions list', granted: true },
      { label: 'Create or edit records', granted: false },
      { label: 'Submit credit requests', granted: false },
      { label: 'Approve transfers', granted: false },
      { label: 'Admin access', granted: false },
    ],
  },
];

function getCurrentRole(): string {
  try {
    const token = localStorage.getItem('token');
    if (!token) return 'Demo';
    const payload = decodeJwt(token);
    if (!payload) return 'Demo';
    const role = payload.role ?? payload.roles ?? null;
    if (Array.isArray(role)) return role[0] ?? 'Demo';
    return typeof role === 'string' && role.length > 0 ? role : 'Demo';
  } catch {
    return 'Demo';
  }
}

const Admin: React.FC = () => {
  const currentRole = getCurrentRole();
  const isDemo = currentRole === 'Demo' || currentRole === '';

  return (
    <section aria-labelledby="admin-heading" className="admin-section">
      <div className="admin-header-bar">
        <div>
          <h2 id="admin-heading" className="admin-heading">
            Role &amp; Permissions Matrix
          </h2>
          <p className="admin-header-sub">
            Fintech RBAC overview — read-only reference for all platform roles.
          </p>
        </div>
        <div
          className="admin-current-role"
          role="status"
          aria-live="polite"
          aria-label="Your current role"
        >
          <span className="admin-current-label">Your current role</span>
          <span className="badge" data-testid="current-role-badge">{currentRole}</span>
        </div>
      </div>

      {isDemo && (
        <div
          className="admin-demo-banner"
          role="alert"
          aria-live="polite"
          aria-label="Demo mode notice"
        >
          <span className="admin-demo-icon" aria-hidden="true"><Info size={18} color="currentColor" /></span>
          <span>
            <strong>Demo / Read-only mode</strong> — Write operations are disabled.
            Sign in with an elevated role to unlock full functionality.
          </span>
        </div>
      )}

      <div className="admin-roles-grid" role="list" aria-label="Role cards">
        {ROLES.map((r) => (
          <div
            key={r.name}
            className="admin-role-card"
            role="listitem"
            aria-label={`${r.name} role card`}
          >
            <div className="admin-role-header">
              <h3 className="admin-role-name">{r.name}</h3>
              <p className="admin-role-context">{r.context}</p>
            </div>
            <p className="admin-role-desc">{r.description}</p>
            <ul className="admin-perm-list" aria-label={`${r.name} permissions`}>
              {r.permissions.map((p) => (
                <li key={p.label} className="admin-perm-item">
                  <span
                    className={p.granted ? 'admin-perm-granted' : 'admin-perm-denied'}
                    aria-label={p.granted ? 'Granted' : 'Denied'}
                  >
                    {p.granted ? <Check size={14} color="currentColor" /> : <Minus size={14} color="currentColor" />}
                  </span>
                  <span className="admin-perm-label">{p.label}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </section>
  );
};

export default Admin;
