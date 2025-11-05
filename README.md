# FinTech Application – Microservices Platform 

> [!WARNING]  
> **This is a portfolio project and not a real financial application.** Do not use real personal or financial information. All data is for demonstration purposes only.

Monorepo for a .NET microservices fintech platform deployed to Salesforce Heroku using Docker.

Includes:
- API Gateway (Ocelot) with JWT validation, rate limiting, and minimal fallback endpoints
- Microservices: User, Account, Transaction, Notification (MassTransit)
- React UI (TypeScript, accessible, minimal)
- Postgres (Postgres) via Npgsql/EF Core
- RabbitMQ (CloudAMQP) via MassTransit
- Serilog logging (console; suitable for Papertrail drains)

## Production Readiness

This application has been reviewed and is production-ready with the following strengths:
-  Comprehensive security (JWT, PBKDF2, security headers, rate limiting)
-  Robust error handling with fallback mechanisms
-  SSL/TLS for all database connections
-  Input validation and XSS protection
-  Structured logging with correlation IDs
-  Docker containerization with non-root users
-  Health check endpoints for monitoring

**Pre-Launch Requirements:**
1. Change `JWT_SIGNING_KEY` to a strong random value (32+ bytes)
2. Configure database backups
3. Set up monitoring and alerting
4. Review .NET 10 preview package compatibility


## Highlights
- JWT auth: HS256 via a shared JWT_SIGNING_KEY or plug in your JWT_AUTHORITY (IdentityServer/OIDC).
- DB persistence: Gateway and EF Core migrates/ensures data is persitent.
- Demo bootstrap: "demo/Demo@2026" works out of the box; UI login is prefilled.
- Resilient configuration: DATABASE_URL, PG* env support. 
- Health endpoints: GET /health on each service.
- Rate limiting: Auth (10/min), Transactions (30/min), Accounts (20/min)

UI features (production-friendly)
- Protected dashboard (visible only when logged in), with:
  - KPIs: total balance, account count, transaction count (animated bars)
  - Accounts summary
  - Quick actions (Send Money, Create Account)
- Transactions:
  - Manual Transaction 
  - Send Money supports custom description
  - Transaction ID shown in history
- Payees:
  - Add Payee includes dropdown of existing users (GET /users/all)
- Accessibility and polish:
  - High-contrast palette, focus indicators, smooth scrolling
  - Scroll progress indicator
  - Animated hover states, subtle card shadows
  - Consistent spacing, minimal look
  - My LinkedIn link (Anubhav Sharma)


Repository layout
- ApiGateway/
- UserService/
- AccountService/
- TransactionService/
- NotificationService/
- Contracts/
- ui/
- .github/workflows/ci-cd.yml
- Procfile, app.json, .env.template

Prerequisites
- GitHub repository  
- Add-ons: postgresql, cloudamqp, papertrail (optional)
- Docker installed locally
- .NET 8+ (or latest LTS)
- Node.js 18+ for UI

Local development
1) Copy .env.template to .env and fill values.
2) Start Postgres and RabbitMQ locally 
3) Launch services (choose one of the flows):
   - Microservices mode:
     - For each service (User/Account/Transaction/Notification):
       - dotnet restore
       - dotnet ef database update (or run once to auto-migrate)
       - dotnet run
   - Gateway-first demo mode:
     - dotnet run --project ApiGateway (auto ensures DB+seed if DATABASE_URL/PG* present; otherwise in-memory in case of failures)
4) Run UI:
   - cd ui && npm install && npm start

Endpoints (through API Gateway)
- Auth: POST /users/register, POST /users/login
- Users: GET /users/all (for Payee dropdown; requires auth)
- Accounts: GET /accounts, POST /accounts
- Transactions: GET /transactions, POST /transactions
- Payments: POST /payments
- Payees: GET /payees, POST /payees
- Health: GET /health

## Security

### Authentication
- Set JWT_AUTHORITY to your IdentityServer/OIDC issuer to use asymmetric validation
- Otherwise set a strong JWT_SIGNING_KEY (HS256) - MUST be 32+ bytes
- **IMPORTANT**: If not using JWT_AUTHORITY, ensure the SAME JWT_SIGNING_KEY is configured for ApiGateway and all services

### Password Requirements
- Minimum 8 characters
- Must contain: uppercase, lowercase, digit, special character
- PBKDF2 with 310,000 iterations (OWASP recommended)
- Exception: "demo" user bypasses complexity for development convenience

### Security Headers
All services include:
- HSTS (production)
- X-Content-Type-Options, X-Frame-Options, Referrer-Policy
- Strict Content-Security-Policy
- Permissions-Policy

### Rate Limiting
- Authentication endpoints: 10 requests/minute
- Financial operations: 30 requests/minute
- Account operations: 20 requests/minute
- Returns 429 with clear error message when exceeded






## Heroku deployment (Container Registry)
- Create a Heroku app for each:
  - ApiGateway (web)
  - UserService (web)
  - AccountService (web)
  - TransactionService (web)
  - NotificationService (worker)
  - UI (web) optional
- Add-ons:
  - heroku-postgresql:hobby-dev
  - cloudamqp:lemur (typically on Transaction/Notification)
  - papertrail
- Config vars:
  - JWT_AUTHORITY, JWT_AUDIENCE, JWT_SIGNING_KEY
  - DATABASE_URL
  - CLOUDAMQP_URL
  - PAPERTRAIL_ADDRESS, PAPERTRAIL_PORT

CI/CD (GitHub Actions)
- The workflow builds, tests, packages docker images, pushes to Heroku registry, and releases.


Runtime architecture
- Ocelot API Gateway forwards requests to downstream services and enforces JWT + rate limits.
- Services expose REST APIs, use EF Core with Postgres, and log with Serilog.
- TransactionService publishes domain events via MassTransit (CloudAMQP); NotificationService consumes.
- Health checks at /health.

Notes
- Demo seed creates a default user and accounts/transactions when DB is available.
