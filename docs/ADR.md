 # Architecture Decision Records

 ## ADR-001: Microservices Decomposition by Business Domain

 **Status:** Accepted

 **Context:** A monolithic banking API would couple unrelated concerns—user identity, accounts, transactions, notifications, corporate banking—making independent scaling and deployment impossible.

 **Decision:** Decompose into six autonomous services (UserService, AccountService, TransactionService, NotificationService, CorporateBankingService, ApiGateway) each owning its own PostgreSQL schema, EF Core context, and deployment unit.

 **Consequences:** Services deploy and scale independently, a failure in notifications cannot cascade to payments, but operational complexity increases with per-service databases, migrations, and health monitoring.

 ---

 ## ADR-002: API Gateway with Ocelot and Centralized Cross-Cutting Concerns

 **Status:** Accepted

 **Context:** Clients should not discover or authenticate against six internal services directly, and cross-cutting policies (rate limiting, JWT validation, security headers, correlation IDs) must be enforced uniformly.

 **Decision:** Deploy an Ocelot-based API Gateway that routes all external traffic, enforces per-endpoint fixed-window rate limiting, attaches X-Correlation-ID headers, blocks suspicious paths (.git, .env, actuator), and applies security headers (CSP, HSTS, X-Frame-Options, Permissions-Policy).

 **Consequences:** A single ingress simplifies client integration and centralizes security enforcement, but the gateway becomes a throughput bottleneck that requires its own resilience (Polly retry with exponential backoff, circuit breaker at 50% failure ratio over 30s, 15s break duration).

 ---

 ## ADR-003: Event-Driven Messaging with MassTransit over RabbitMQ

 **Status:** Accepted

 **Context:** Services must react to domain events (transaction created, KYC status changed, suspicious activity flagged, payment approved) without tight runtime coupling or synchronous call chains.

 **Decision:** Use MassTransit as the messaging abstraction over RabbitMQ (CloudAMQP in production), publishing immutable record-based events from Contracts, with NotificationService hosting dedicated consumers that trigger FluentEmail SMTP dispatches; fall back to in-memory transport for local development.

 **Consequences:** Services communicate asynchronously with guaranteed delivery and retry semantics, new consumers can subscribe without modifying publishers, but message ordering is eventual and debugging requires distributed tracing.

 ---

 ## ADR-004: Dual-Mode JWT Authentication with OIDC-Ready Token Validation

 **Status:** Accepted

 **Context:** The platform needs stateless authentication that works today with self-issued JWTs (PBKDF2-SHA256 at 310,000 iterations for password hashing) and can migrate to an external OIDC provider without code changes.

 **Decision:** Implement dual-mode JWT validation in UserService—self-issued HMAC-SHA256 tokens with short expiry (15-30 min) plus refresh tokens, while ApiGateway validates tokens per-route via Ocelot AuthenticationOptions; structure claims (sub, org, role) to match standard OIDC claims.

 **Consequences:** Authentication is stateless and horizontally scalable, the OIDC migration path requires only configuration changes, but token revocation before expiry depends on short TTLs since there is no server-side session store.

 ---

 ## ADR-005: Double-Entry Bookkeeping Ledger for Financial Integrity

 **Status:** Accepted

 **Context:** A simple balance-update model cannot guarantee accounting correctness—partial failures can silently create money, and there is no audit trail for regulatory compliance.

 **Decision:** Record every financial movement as a pair of LedgerEntry rows (debit and credit) linked by TransactionId, with AccountId, EntryType, Amount, Currency, and CreatedAt; balances are derived by summing ledger entries rather than stored as mutable fields.

 **Consequences:** The system maintains a complete, immutable audit trail where debits always equal credits, enabling reconciliation and regulatory reporting, but balance queries require aggregation over the ledger which must be offset by caching or materialized views.

 ---

 ## ADR-006: Redis Cache-Aside with Graceful In-Memory Fallback

 **Status:** Accepted

 **Context:** Frequently read data (account lists, transaction histories) generates repetitive database load, but the caching layer must not become a single point of failure for a financial application.

 **Decision:** Implement cache-aside pattern via ICacheService wrapping IDistributedCache backed by StackExchange.Redis (Upstash in production) with namespaced keys (accounts:, transactions:); wrap all cache operations in try/catch so failures silently fall through to the database, and fall back to in-memory DistributedCache when Redis is unavailable at startup.

 **Consequences:** Read-heavy endpoints serve from cache with sub-millisecond latency and database load drops significantly, cache failures are invisible to users, but cache invalidation on writes must be handled explicitly to avoid stale data.

 ---

 ## ADR-007: Idempotency and ETag Infrastructure for Safe Retries and Conditional Responses

 **Status:** Accepted

 **Context:** Network failures and client retries in financial APIs can cause duplicate payments or transfers, and repeated full-payload GET responses waste bandwidth on unchanged resources.

 **Decision:** Enforce Idempotency-Key header on all POST/PUT/PATCH via IdempotencyFilterAttribute—store the key with a 24-hour TTL, return 409 Conflict for in-flight duplicates, and replay the stored response for completed duplicates; implement ETagFilterAttribute on GET endpoints using SHA-256 hash of the response body, returning 304 Not Modified when the client sends a matching If-None-Match header.

 **Consequences:** Clients can safely retry mutating operations without risk of duplicate side effects, GET responses are bandwidth-efficient, but idempotency records consume storage proportional to write volume and must be pruned after TTL expiry.

 ---

 ## ADR-008: Distributed Tracing with OpenTelemetry Exported via OTLP

 **Status:** Accepted

 **Context:** Debugging request flows across six services, RabbitMQ consumers, Redis calls, and PostgreSQL queries requires correlated traces, not just per-service logs.

 **Decision:** Instrument all services with OpenTelemetry SDK (ASP.NET Core, HttpClient, and EF Core instrumentation), export traces and metrics via OTLP HTTP/Protobuf to New Relic, gate the entire pipeline on the OTEL_EXPORTER_OTLP_ENDPOINT environment variable so local development runs without an exporter.

 **Consequences:** Every request carries a TraceId across service boundaries enabling end-to-end latency analysis and bottleneck identification, but the observability backend becomes an operational dependency for production debugging and adds per-span export overhead.


 ---

 ## ADR-009: React SPA with Design Token System, Global State, and Protected Route Architecture

 **Status:** Accepted

 **Context:** The frontend must deliver a consistent fintech-grade UI across 17 routes with authenticated access control, real-time session awareness, and reusable data visualization—without adopting a heavy framework like Next.js.

 **Decision:** Build a React 18 + TypeScript SPA using CSS custom properties (tokens.css) for design tokens (colors, radii, typography, transitions, z-index), AppContext for global user/notification state derived from JWT claims, RequireAuth guard with 30-second token expiry polling, Layout shell (navigation, GlobalSearch with masked account numbers, NotificationBell, SessionExpiryBanner), Recharts with ChartShell/chartTheme for dashboards, and jsPDF/jspdf-autotable for PDF statement export.

 **Consequences:** The UI is fast to develop with consistent theming via tokens, global state avoids prop drilling across deeply nested routes, and the guard prevents stale-session interactions, but the SPA model requires client-side routing vigilance and the custom state management may need migration to a dedicated store if complexity grows.

