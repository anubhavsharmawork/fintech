// ─── API Endpoints ────────────────────────────────────────────────────────────

export const API = {
  // Auth / users
  LOGIN: '/users/login',
  REGISTER: '/users/register',
  LOGOUT: '/users/logout',
  REFRESH: '/users/refresh',
  PROFILE: '/users/profile',
  USERS_ALL: '/users/all',
  TIMEZONE: '/users/timezone',

  // Core banking
  ACCOUNTS: '/accounts',
  ACCOUNT: (id: string) => `/accounts/${encodeURIComponent(id)}`,
  ACCOUNT_DEPOSIT_EXTERNAL: (id: string) =>
    `/accounts/${encodeURIComponent(id)}/deposit-from-external`,
  TRANSACTIONS: '/transactions',
  PAYMENTS: '/payments',
  PAYEES: '/payees',

  // Budget
  BUDGET: '/budget/budget',

  // Bank connections
  BANK_CONNECTIONS: '/bankconnections',
  BANK_CONNECTIONS_AVAILABLE: '/bankconnections/available',
  BANK_CONNECTIONS_CONNECT: '/bankconnections/connect',
  BANK_CONNECTION: (id: string) => `/bankconnections/${encodeURIComponent(id)}`,
  BANK_CONNECTION_SYNC: (id: string) =>
    `/bankconnections/${encodeURIComponent(id)}/sync`,
  BANK_CONNECTIONS_ACCOUNTS: '/bankconnections/accounts',

  // Notifications
  NOTIFICATIONS: '/api/notifications',
  NOTIFICATIONS_READ_ALL: '/api/notifications/read-all',
  NOTIFICATIONS_PREFERENCES: '/api/notifications/preferences',

  // Cards
  CARDS: '/api/cards',
  CARD: (id: string) => `/api/cards/${encodeURIComponent(id)}`,
  CARD_FREEZE: (id: string) => `/api/cards/${encodeURIComponent(id)}/freeze`,
  CARD_UNFREEZE: (id: string) => `/api/cards/${encodeURIComponent(id)}/unfreeze`,

  // Compliance / KYC / SAR
  KYC_STATUS: '/api/kyc/status',
  SAR_REPORTS: '/api/sar',

  // Organisations (corporate)
  ORGANISATION: (orgId: string) => `/api/organisations/${encodeURIComponent(orgId)}`,
  ORGANISATION_MEMBERS: (orgId: string) =>
    `/api/organisations/${encodeURIComponent(orgId)}/members`,
  ORGANISATION_MEMBERS_INVITE: (orgId: string) =>
    `/api/organisations/${encodeURIComponent(orgId)}/members/invite`,

  // Payment batches
  PAYMENT_BATCHES: '/api/paymentbatches',
  PAYMENT_BATCH: (id: string) => `/api/paymentbatches/${encodeURIComponent(id)}`,
  PAYMENT_BATCH_SUBMIT: (id: string) =>
    `/api/paymentbatches/${encodeURIComponent(id)}/submit`,
  PAYMENT_BATCH_EXECUTE: (id: string) =>
    `/api/paymentbatches/${encodeURIComponent(id)}/execute`,

  // Approvals
  APPROVALS_PENDING: '/api/approvals/pending',
  APPROVAL_DETAIL: (id: string) => `/api/approvals/${encodeURIComponent(id)}`,
  APPROVAL_DECIDE: (id: string) =>
    `/api/paymentbatches/${encodeURIComponent(id)}/approve`,

  // Organisation accounts
  ORG_ACCOUNTS: (orgId: string) => `/api/accounts/organisation/${encodeURIComponent(orgId)}`,

  // FTK credit
  CREDIT_FACILITY: '/api/v1/ftk/credit/facility',
  CREDIT_DRAWDOWN: '/api/v1/ftk/credit/drawdown',
  CREDIT_REPAYMENT: '/api/v1/ftk/credit/repayment',
  CREDIT_REPAYMENTS: '/api/v1/ftk/credit/repayments',

  // Sanctions
  SANCTIONS: '/api/v1/ftk/sanctions',
  SANCTION: (id: string) => `/api/v1/ftk/sanctions/${encodeURIComponent(id)}`,
  SANCTION_DISBURSE: (id: string) =>
    `/api/v1/ftk/sanctions/${encodeURIComponent(id)}/disburse`,
  SANCTION_REJECT: (id: string) =>
    `/api/v1/ftk/sanctions/${encodeURIComponent(id)}/reject`,
  SANCTION_CANCEL: (id: string) =>
    `/api/v1/ftk/sanctions/${encodeURIComponent(id)}/cancel`,
  SANCTION_AUDIT: (id: string) =>
    `/api/v1/ftk/sanctions/${encodeURIComponent(id)}/audit`,

  // Feedback
  FEEDBACK: '/api/v1/feedback',

  // Search (pass-through endpoints)
  SEARCH_ACCOUNTS: '/accounts',
  SEARCH_TRANSACTIONS: '/transactions',
  SEARCH_PAYEES: '/payees',
} as const;

// ─── App Routes ───────────────────────────────────────────────────────────────

export const ROUTES = {
  HOME: '/',
  LOGIN: '/login',
  REGISTER: '/register',
  ACCOUNTS: '/accounts',
  TRANSACTIONS: '/transactions',
  BUDGET: '/budget',
  PRIVACY: '/privacy',
  WHITEPAPER: '/whitepaper',
  SANCTIONS: '/sanctions',
  SANCTION_DETAIL: (id: string) => `/sanctions/${encodeURIComponent(id)}`,
  ADMIN: '/admin',
  COMPLIANCE: '/compliance',
  CORPORATE_DASHBOARD: '/corporate/dashboard',
  CORPORATE_BATCHES: '/corporate/batches',
  CORPORATE_APPROVALS: '/corporate/approvals',
  CREDIT: '/credit',
  REQUEST_CREDIT: '/request-credit',
  SETTINGS: '/settings',
  CARDS: '/cards',
  NOT_FOUND: '*',
} as const;

// ─── Transaction / account status values ─────────────────────────────────────

export const TX_STATUS = {
  PENDING: 'pending',
  PROCESSING: 'processing',
  CLEARED: 'cleared',
  COMPLETED: 'completed',
  FLAGGED: 'flagged',
  REVERSED: 'reversed',
} as const;

export const SANCTION_STATUS = {
  PENDING: 'Pending',
  APPROVED: 'Approved',
  REJECTED: 'Rejected',
  CANCELLED: 'Cancelled',
  DISBURSED: 'Disbursed',
} as const;

export const CARD_STATUS = {
  ACTIVE: 'active',
  FROZEN: 'frozen',
} as const;

export const BALANCE_STATUS = {
  EXCELLENT: 'excellent',
  HEALTHY: 'healthy',
  WARNING: 'warning',
  LOW: 'low',
} as const;

export const CLIENT_TYPE = {
  INDIVIDUAL: 'Individual',
  CORPORATE: 'Corporate',
} as const;

export const ORG_ROLE = {
  ADMIN: 'Admin',
  MEMBER: 'Member',
  APPROVER: 'Approver',
} as const;

// ─── Notification event types ─────────────────────────────────────────────────

export const NOTIFICATION_EVENTS = {
  TRANSACTION_CREATED: 'TransactionCreated',
  PAYMENT_APPROVED: 'PaymentApproved',
  BATCH_SUBMITTED: 'PaymentBatchSubmittedForApproval',
  REPAYMENT_COMPLETED: 'RepaymentCompleted',
  KYC_STATUS_CHANGED: 'KycStatusChanged',
  SUSPICIOUS_ACTIVITY: 'SuspiciousActivityFlagged',
} as const;

// ─── Spending categories ──────────────────────────────────────────────────────

export const SPENDING_TYPES = ['Fixed', 'Future', 'Fun'] as const;

// ─── Misc config ─────────────────────────────────────────────────────────────

/** ISO-4217 default currency for NZ locale displays */
export const DEFAULT_CURRENCY = 'NZD';

/** Pagination defaults */
export const PAGE_SIZE_DEFAULT = 25;
export const PAGE_SIZE_ACCOUNTS = 10;

/** JWT near-expiry window (seconds) */
export const TOKEN_EXPIRY_BUFFER_SEC = 30;

/** Request timeout (ms) */
export const REQUEST_TIMEOUT_MS = 30_000;
