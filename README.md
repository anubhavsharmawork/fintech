# FinTech Application – Microservices Platform 

Monorepo for a .NET microservices fintech platform deployed to Salesforce Heroku using Docker.

Includes:
- API Gateway (Ocelot) with JWT validation, rate limiting, and minimal fallback endpoints
- Microservices: User, Account, Transaction, Notification (MassTransit)
- React UI (TypeScript, accessible, minimal)
- Postgres (Postgres) via Npgsql/EF Core
- RabbitMQ (CloudAMQP) via MassTransit
- Serilog logging (console; suitable for Papertrail drains)

Highlights
- JWT auth: HS256 via a shared JWT_SIGNING_KEY or plug in your JWT_AUTHORITY (IdentityServer/OIDC).
- DB persistence: Gateway and EF Core migrates/ensures demo data is seeded.
- Demo bootstrap: “demo/demo” works out of the box; UI login is prefilled.
- Resilient configuration: DATABASE_URL, PG* env support. 
- Health endpoints: GET /health on each service.
- Rate limiting: Ocelot configured with per-minute limits.

UI features (production-friendly)
- Protected dashboard (visible only when logged in), with:
  - KPIs: total balance, account count, transaction count (animated bars)
  - Accounts summary
  - Quick actions (Send Money, Create Account)
- Transactions:
  - Manual Transaction removed
  - Send Money supports custom description
  - Transaction ID shown in history
  - Account number is shown instead of account ID
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

Security
- Set JWT_AUTHORITY to your IdentityServer/OIDC issuer to use asymmetric validation; otherwise set a strong JWT_SIGNING_KEY (HS256).
- IMPORTANT: If not using JWT_AUTHORITY, ensure the SAME JWT_SIGNING_KEY is configured for ApiGateway and all services; mismatches cause 401.



Heroku deployment (Container Registry)
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
