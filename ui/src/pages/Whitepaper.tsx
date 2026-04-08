/* istanbul ignore file */
import * as React from 'react';
import {
    X,
    Menu,
    Zap,
    Landmark,
    Globe,
    Lock,
    Telescope,
    UserCog,
    User,
    CreditCard,
    RefreshCw,
    BarChart3,
    Link as LinkIcon,
    DoorOpen,
    Settings,
    Mail,
    Shield,
    Accessibility,
    Map,
    Rocket,
    Scale,
    Check,
    Bell,
    BookOpen,
    Database,
} from 'lucide-react';
import './Whitepaper.css';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

/* ─── Table of Contents data ─────────────────────── */
const TOC = [
    { id: 'abstract', label: 'Abstract' },
    { id: 'problem', label: 'The Problem' },
    { id: 'vision', label: 'The Inflection Point' },
    { id: 'architecture', label: 'Architecture' },
    { id: 'frontend', label: 'Frontend Systems' },
    { id: 'blockchain', label: 'The Blockchain Difference' },
    { id: 'security', label: 'Security & Compliance' },
    { id: 'accessibility', label: 'Accessibility' },
    { id: 'performance', label: 'Performance at Scale' },
    { id: 'roadmap', label: 'Scalability Roadmap' },
    { id: 'urgency', label: 'The World Is Moving' },
    { id: 'legal', label: 'Legal & Regulatory' },
    { id: 'manifesto', label: 'Closing Manifesto' },
];

const Whitepaper: React.FC = () => {
    const [activeId, setActiveId] = React.useState<string>('abstract');
    const [tocOpen, setTocOpen] = React.useState(false);

    /* ── Scroll-reveal observer ───────────────────── */
    React.useEffect(() => {
        const sections = document.querySelectorAll<HTMLElement>('.wp-section');
        const revealObserver = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting) {
                        entry.target.classList.add('wp-visible');
                    }
                });
            },
            { threshold: 0.1, rootMargin: '0px 0px -40px 0px' }
        );
        sections.forEach((s) => revealObserver.observe(s));
        return () => revealObserver.disconnect();
    }, []);

    /* ── Active TOC tracking ──────────────────────── */
    React.useEffect(() => {
        const headings = TOC.map((t) => document.getElementById(t.id)).filter(Boolean) as HTMLElement[];
        const tocObserver = new IntersectionObserver(
            (entries) => {
                const visible = entries.filter((e) => e.isIntersecting);
                if (visible.length > 0) {
                    setActiveId(visible[0].target.id);
                }
            },
            { rootMargin: '-80px 0px -60% 0px', threshold: 0 }
        );
        headings.forEach((h) => tocObserver.observe(h));
        return () => tocObserver.disconnect();
    }, []);

    const handlePrint = async () => {
      const root = document.querySelector('.wp') as HTMLElement | null;
      const main = document.querySelector('.wp-main') as HTMLElement | null;
      if (!root || !main) return;

      // Make all scroll-reveal sections visible before capture
      root.classList.add('wp-print-mode');

      // Wait for styles to apply
      await new Promise<void>((resolve) =>
        requestAnimationFrame(() => setTimeout(resolve, 400))
      );

      try {
        const canvas = await html2canvas(main, {
          scale: 2,
          useCORS: true,
          allowTaint: true,
          scrollY: 0,
          windowWidth: main.scrollWidth,
          windowHeight: main.scrollHeight,
          height: main.scrollHeight,
          width: main.scrollWidth,
        });

        const imgData = canvas.toDataURL('image/png');
        const pdf = new jsPDF('p', 'mm', 'a4');

        const pageWidth = pdf.internal.pageSize.getWidth();
        const pageHeight = pdf.internal.pageSize.getHeight();

        const imgWidth = pageWidth;
        const imgHeight = (canvas.height * pageWidth) / canvas.width;

        let heightLeft = imgHeight;
        let position = 0;

        pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
        heightLeft -= pageHeight;

        while (heightLeft > 0) {
          position -= pageHeight;
          pdf.addPage();
          pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
          heightLeft -= pageHeight;
        }

        pdf.save('open-finance-whitepaper.pdf');
      } finally {
        root.classList.remove('wp-print-mode');
      }
    };

    const scrollTo = (id: string) => {
        document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
        setTocOpen(false);
    };

    return (
        <div className="wp" role="article" aria-label="Open Finance Whitepaper">
            <div className="wp-layout">
                {/* ── Mobile TOC Toggle ─────────────────── */}
                <button
                    className="wp-toc-toggle"
                    onClick={() => setTocOpen((o) => !o)}
                    aria-expanded={tocOpen}
                    aria-controls="wp-toc-nav"
                >
                    {tocOpen ? <><X size={16} color="currentColor" /> Close Contents</> : <><Menu size={16} color="currentColor" /> Table of Contents</>}
                </button>

                {/* ── Sidebar TOC ──────────────────────── */}
                <nav
                    id="wp-toc-nav"
                    className={`wp-toc${tocOpen ? ' wp-toc-open' : ''}`}
                    aria-label="Whitepaper table of contents"
                >
                    <p className="wp-toc-title" aria-hidden="true">Contents</p>
                    <ul className="wp-toc-list" role="list">
                        {TOC.map((item) => (
                            <li key={item.id} className="wp-toc-item">
                                <a
                                    href={`#${item.id}`}
                                    className={`wp-toc-link${activeId === item.id ? ' active' : ''}`}
                                    onClick={(e) => { e.preventDefault(); scrollTo(item.id); }}
                                    aria-current={activeId === item.id ? 'location' : undefined}
                                >
                                    {item.label}
                                </a>
                            </li>
                        ))}
                    </ul>
                </nav>

                {/* ── Main Content ─────────────────────── */}
                <div className="wp-main">
                    {/* ── Hero / Abstract ─────────────────── */}
                    <header className="wp-hero" id="abstract">
                        <span className="wp-hero-label">Whitepaper v1.0</span>
                        <h1>FinTech Microservices<br />Open Finance Platform</h1>
                        <p className="wp-hero-subtitle">Open finance infrastructure built to institutional standards: distributed caching, end-to-end observability, double-entry accounting, and a frontend built for scale.</p>
                        <p className="wp-hero-abstract">
                            For most of human history, money has been controlled by institutions that profit from its opacity.
                            Ledgers are hidden. Fees are buried. Access is gated by geography, credit history, and privilege.
                            The result is a financial system that works brilliantly for the few and barely at all for the many.
                            This platform exists because we believe a different architecture is possible - one where every transaction is auditable,
                            every fee is transparent, every user owns their financial identity, and trust is enforced by code rather than contracts.
                            <strong> Finance should be open.</strong>
                        </p>
                        <div className="wp-hero-cta">
                            <button className="wp-download-btn" onClick={handlePrint} aria-label="Download whitepaper as PDF">
                                ↓ Download PDF
                            </button>
                            <span className="wp-version-badge">Version 1.0 • March 2026</span>
                        </div>
                    </header>

                    {/* ── 1. The Problem ──────────────────── */}
                    <section className="wp-section" id="problem" aria-labelledby="problem-title">
                        <div className="wp-section-icon" aria-hidden="true"><Zap size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 01</span>
                        <h2 className="wp-section-title" id="problem-title">The Problem With Finance Today</h2>
                        <div className="wp-body">
                            <p>
                                The global financial system was not designed for the people who use it. It was designed for the institutions that control it.
                                Three truths define the crisis:
                            </p>
                        </div>

                        <div className="wp-callout">
                            <span className="wp-callout-icon" aria-hidden="true"><Landmark size={20} color="currentColor" /></span>
                            <div className="wp-callout-text">
                                <strong>Opacity by design.</strong> The average consumer cannot trace how a bank calculates a fee, where their
                                deposits are invested, or why a wire transfer takes three days in an era of instant communication.
                                Legacy finance profits from confusion.
                            </div>
                        </div>

                        <div className="wp-callout">
                            <span className="wp-callout-icon" aria-hidden="true"><Globe size={20} color="currentColor" /></span>
                            <div className="wp-callout-text">
                                <strong>1.4 billion people remain unbanked.</strong> Not because banking is hard - because access is gatekept.
                                Documentation requirements, minimum balances, geographic limitations, and credit prerequisites exclude the populations
                                who need financial services most.
                            </div>
                        </div>

                        <div className="wp-callout">
                            <span className="wp-callout-icon" aria-hidden="true"><Lock size={20} color="currentColor" /></span>
                            <div className="wp-callout-text">
                                <strong>Financial exclusion is systemic.</strong> Cross-border remittances cost an average of 6.2% in fees.
                                Micro-entrepreneurs in emerging economies wait weeks for credit decisions that an algorithm could make in seconds.
                                The infrastructure exists to serve everyone. The incentives do not.
                            </div>
                        </div>

                        <aside className="wp-pullquote" role="note">
                            "The question is not whether finance will be disrupted. It is whether the disruption will serve everyone - or just create new gatekeepers."
                        </aside>
                    </section>

                    {/* ── 2. Vision ───────────────────────── */}
                    <section className="wp-section" id="vision" aria-labelledby="vision-title">
                        <div className="wp-section-icon" aria-hidden="true"><Telescope size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 02</span>
                        <h2 className="wp-section-title" id="vision-title">Architecture for Modern Multi-Tenant Banking Platform</h2>
                        <div className="wp-body">
                            <p>
                                In 1991, Tim Berners-Lee released the protocols of the World Wide Web into the public domain.
                                He did not patent them. He did not license them. He made a choice: information should be open.
                                That single decision created the conditions for Google, Wikipedia, open-source software, and the democratisation
                                of knowledge at a scale no civilisation had achieved before.
                            </p>
                            <p>
                                Finance awaits the same inflection point. Money - the movement of value between humans - is still locked
                                inside proprietary systems that charge rent for access. Open finance is the conviction that the protocols
                                of value transfer should be as transparent, composable, and universally accessible as the protocols of information transfer.
                            </p>
                            <p>
                                This platform is not a product. It is a proof of architecture. A demonstration that you can build financial
                                services that are simultaneously <strong>secure and transparent</strong>, <strong>compliant and composable</strong>,
                                <strong>institutional-grade and universally accessible</strong>. The technology is not theoretical. It is deployed.
                                It is auditable. It is open.
                            </p>
                        </div>

                        <aside className="wp-pullquote" role="note">
                            "The internet democratised information. Open finance democratises value. The architecture is the same. Only the payload has changed."
                        </aside>
                    </section>

                    {/* ── 3. Architecture ─────────────────── */}
                    <section className="wp-section" id="architecture" aria-labelledby="arch-title">
                        <div className="wp-section-icon" aria-hidden="true"><UserCog size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 03</span>
                        <h2 className="wp-section-title" id="arch-title">Platform Architecture</h2>
                        <div className="wp-body">
                            <p>
                                Every architectural decision serves a single principle: <strong>no single point of failure</strong>.
                                The platform is decomposed into six autonomous microservices backed by a distributed cache layer, each independently deployable, scalable,
                                and replaceable. This is not microservices for the sake of complexity - it is fault isolation by design.
                            </p>
                        </div>

                        <div className="wp-arch-grid">
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><User size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">User Service</h3>
                                <p className="wp-arch-card-desc">
                                    Authentication, authorisation, identity management. PBKDF2 password hashing (310K iterations), JWT token lifecycle, role-based access control.
                                    Isolated so a credential breach cannot cascade.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><CreditCard size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">Account Service</h3>
                                <p className="wp-arch-card-desc">
                                    Account creation with KYC-gated limit policies and bank connection count validation. Redis-backed distributed caching via Upstash
                                    eliminates redundant database queries. ETag concurrency headers and idempotency key enforcement ensure safe retries at scale.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><CreditCard size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">Card Issuance</h3>
                                <p className="wp-arch-card-desc">
                                    The platform supports virtual card issuance via a provider-agnostic architecture. The current implementation uses a simulated card engine.
                                    The system is designed for zero-friction migration to a regulated card issuing partner, without requiring changes to the application layer.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><RefreshCw size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">Transaction Service</h3>
                                <p className="wp-arch-card-desc">
                                    Transfer orchestration, validation, and settlement. Every money movement produces a pair of LedgerEntry records - a debit on the source, a credit on the destination - implementing
                                    formal double-entry bookkeeping with ISO 4217 currency codes. Redis caching and idempotency filters ensure exactly-once processing under network partition.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><BarChart3 size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">Budget Service</h3>
                                <p className="wp-arch-card-desc">
                                    Budget features are implemented as a frontend UI module for financial planning, categorisation, and spending intelligence.
                                    This module consumes transaction events and read models from backend services; it is not deployed as a separate microservice.
                                    Decoupling ensures analytics do not slow down transaction processing.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><UserCog size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">Corporate Banking Service</h3>
                                <p className="wp-arch-card-desc">
                                    Payment batch orchestration, multi-level approval workflows, and corporate account management.
                                    Role-aware authorisation enforces segregation of duties across submitters, approvers, and executors.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><Bell size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">Notification Service</h3>
                                <p className="wp-arch-card-desc">
                                    Event-driven notification dispatch across KYC status changes, suspicious activity alerts, and payment lifecycle events.
                                    Preference-aware delivery with real-time in-app surfacing and SMS channel support.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><LinkIcon size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">Blockchain Service</h3>
                                <p className="wp-arch-card-desc">
                                    Smart contract interaction, on-chain verification, wallet integration. The trust layer - where code becomes law.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><DoorOpen size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">API Gateway</h3>
                                <p className="wp-arch-card-desc">
                                    Unified entry point with rate limiting, request routing, OpenTelemetry trace propagation, and security headers. The single pane of glass that protects the mesh.
                                </p>
                            </div>
                            <div className="wp-arch-card">
                                <div className="wp-arch-card-icon" aria-hidden="true"><Database size={20} color="currentColor" /></div>
                                <h3 className="wp-arch-card-title">Distributed Cache Layer</h3>
                                <p className="wp-arch-card-desc">
                                    Redis via Upstash provides sub-millisecond distributed caching with service-namespaced key prefixes, configurable TTL, and automatic fallback
                                    to in-memory cache when Redis is unavailable - ensuring degradation is graceful, never catastrophic.
                                </p>
                            </div>
                        </div>

                        <div className="wp-callout">
                            <span className="wp-callout-icon" aria-hidden="true"><Settings size={20} color="currentColor" /></span>
                            <div className="wp-callout-text">
                                <strong>React frontend</strong> communicates exclusively through
                                the API gateway. All endpoint URLs are centralised in a single configuration module - no scattered string literals.
                                The frontend is a consumer, not a dependency - replaceable without touching a single line of backend code.
                            </div>
                        </div>

                        <div className="wp-callout">
                            <span className="wp-callout-icon" aria-hidden="true"><Mail size={20} color="currentColor" /></span>
                            <div className="wp-callout-text">
                                <strong>Event-driven messaging</strong> ensures
                                services react to events - not synchronous API calls. This eliminates temporal coupling and ensures the system
                                degrades gracefully under load rather than failing catastrophically.
                            </div>
                        </div>

                        <div className="wp-callout">
                            <span className="wp-callout-icon" aria-hidden="true"><BookOpen size={20} color="currentColor" /></span>
                            <div className="wp-callout-text">
                                <strong>Double-entry ledger model.</strong> Every financial movement is recorded as a balanced pair of LedgerEntry records - a debit
                                on the source account and a credit on the destination - with ISO 4217 currency codes and immutable timestamps. Account
                                balances are derived, never mutated directly. This is not an accounting approximation; it is the same journal structure
                                used by regulated institutions since Luca Pacioli codified the method in 1494.
                            </div>
                        </div>
                    </section>

                    {/* ── 3b. Frontend Systems ─────────────── */}
                    <section className="wp-section" id="frontend" aria-labelledby="frontend-title">
                        <div className="wp-section-icon" aria-hidden="true"><Settings size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 03b</span>
                        <h2 className="wp-section-title" id="frontend-title">Frontend Systems Architecture</h2>
                        <div className="wp-body">
                            <p>
                                A production-grade backend deserves a production-grade frontend. The client layer is not a prototype skin - it is an
                                institutional-quality application shell with the same operational rigour applied to the service mesh.
                            </p>
                        </div>

                        <ul className="wp-feature-list" role="list">
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Design system tokens</strong> - a single <code>tokens.css</code> file defines every colour, radius, shadow, z-index layer, and typographic scale as CSS custom properties. Light and dark modes are derived from the same token set via <code>prefers-color-scheme</code>. No magic numbers anywhere in the codebase.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Navigation shell</strong> - a collapsible sidebar with role-aware link visibility, breadcrumb context, mobile-responsive hamburger state, and skip-link accessibility. The shell owns route-level loading orchestration and global search activation.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Global state and loading architecture</strong> - a React context provider manages authenticated user identity (decoded from JWT), notification badge counts, and cross-tab session synchronisation via <code>StorageEvent</code> listeners. Page-level skeleton loaders provide perceived performance during data hydration.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Institutional toast system</strong> - five severity tiers (success, error, warning, info, critical) with configurable auto-dismiss durations, action callbacks, stacking limits, and ARIA live-region announcements. Critical alerts are persistent and visually distinct.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Session expiry banner</strong> - a real-time JWT expiry monitor with tiered amber/red/expired states, inline renewal capability, and automatic critical toast dispatch on session termination. Users are never silently logged out.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Global search</strong> - a keyboard-activated command palette (<code>Ctrl+K</code>) that queries accounts, transactions, and payees in parallel with debounced input, highlighted match segments, masked account numbers, and arrow-key navigation across result categories.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Pagination</strong> - URL-synchronised page state with configurable page sizes (10/25/50/100), ellipsis-compressed page selectors, and summary display. Filter changes automatically reset to page one.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>PDF and Excel export</strong> - client-side transaction statement generation with branded headers, summary statistics (total credits, debits, net position), auto-generated filenames, and a compliance disclaimer. PDF rendering uses <code>jsPDF</code> with <code>autoTable</code>; Excel export produces standards-compliant <code>.xlsx</code> via blob download.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>In-app notifications</strong> - a real-time notification bell with polling, severity-aware toast surfacing for high-priority events (suspicious activity, KYC rejection), batch mark-as-read, and unread badge count synchronised through global state.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Recharts visualisation layer</strong> - a shared chart theme defines colour palette, axis typography, grid styling, and tooltip formatting. A reusable <code>ChartShell</code> wrapper provides consistent card layout, subtitles, and period-toggle controls across all analytical views.</span>
                            </li>
                        </ul>

                        <aside className="wp-pullquote" role="note">
                            &quot;A financial platform is only as trustworthy as the interface through which its users experience it. Every pixel is a promise.&quot;
                        </aside>
                    </section>

                    {/* ── 4. Blockchain ───────────────────── */}
                    <section className="wp-section" id="blockchain" aria-labelledby="blockchain-title">
                        <div className="wp-section-icon" aria-hidden="true"><LinkIcon size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 04</span>
                        <h2 className="wp-section-title" id="blockchain-title">The Blockchain Difference (F-Mode)</h2>
                        <div className="wp-body">
                            <p>
                                Most fintech platforms that claim blockchain integration are running mocked endpoints against local simulations.
                                This platform does not mock. Smart contracts are compiled, deployed, and executed on the <strong>Ethereum blockchain</strong> (currently targeting the Sepolia testnet) -
                                a live, public, decentralised network where every transaction consumes real gas, produces a real hash,
                                and is verifiable by anyone on Etherscan.
                            </p>
                            <p>
                                When a user toggles F-Mode, they are not switching a UI theme. They are activating a fundamentally different
                                trust model. Transactions are signed by the user's own wallet - not by a server-side key.
                                Settlement is enforced by smart contract logic - not by a database write. Verification is on-chain and permanent - not
                                a log entry that can be edited or deleted.
                            </p>
                        </div>

                        <div className="wp-stats">
                            <div className="wp-stat">
                                <div className="wp-stat-value">Ethereum</div>
                                <div className="wp-stat-label">Live Blockchain</div>
                            </div>
                            <div className="wp-stat">
                                <div className="wp-stat-value">Web3</div>
                                <div className="wp-stat-label">Wallet-Signed</div>
                            </div>
                            <div className="wp-stat">
                                <div className="wp-stat-value">Etherscan</div>
                                <div className="wp-stat-label">Publicly Verifiable</div>
                            </div>
                            <div className="wp-stat">
                                <div className="wp-stat-value">Solidity</div>
                                <div className="wp-stat-label">Smart Contracts</div>
                            </div>
                        </div>

                        <aside className="wp-pullquote" role="note">
                            "Trust is not a feature you add. It is an architecture you choose. On-chain verification means no middleman can alter, delay, or deny a transaction after the fact."
                        </aside>
                    </section>

                    {/* ── 5. Security & Compliance ────────── */}
                    <section className="wp-section" id="security" aria-labelledby="security-title">
                        <div className="wp-section-icon" aria-hidden="true"><Shield size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 05</span>
                        <h2 className="wp-section-title" id="security-title">Security &amp; Compliance</h2>
                        <div className="wp-body">
                            <p>
                                Open does not mean insecure. Transparency and security are not opposites - they are prerequisites for each other.
                                A system you cannot inspect is a system you cannot trust. This platform is built to withstand scrutiny because
                                scrutiny is the point.
                            </p>
                        </div>

                        <ul className="wp-feature-list" role="list">
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>OWASP Top 10 compliance</strong> - injection prevention, broken access control mitigation, cryptographic hardening, security misconfiguration elimination, and SSRF protection across every endpoint.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Password security</strong> - PBKDF2 hashing (310K iterations) with per-user salts. Account lockout after failed attempts. No plaintext storage, ever.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Rate limiting and throttling</strong> - API gateway enforces request limits per client, preventing abuse and DDoS amplification.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>ETag and idempotency infrastructure</strong> - GET responses include SHA-256 ETag headers with HTTP 304 Not Modified support, eliminating redundant payload transfers. Write operations require an <code>Idempotency-Key</code> header; duplicate keys return the stored response, in-flight duplicates return HTTP 409. Keys expire after 24 hours. This is not optimistic locking - it is formal exactly-once write semantics.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Security headers</strong> - HSTS, Content-Security-Policy, X-Frame-Options, X-Content-Type-Options deployed across all responses.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>GDPR &amp; ISO 27001 alignment</strong> - data minimisation, consent-driven collection, encrypted storage, jurisdiction-aware data residency, and audit-ready logging patterns designed for global regulatory compliance.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>JWT lifecycle management</strong> - short-lived access tokens with refresh rotation. No algorithm-none acceptance. Signature verification on every request.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Account creation policy enforcement</strong> - KYC status verification, per-client-type account limits, and bank connection count validation are enforced server-side on every creation request. No client can bypass limits through direct API access.</span>
                            </li>
                        </ul>

                        <aside className="wp-pullquote" role="note">
                            "Security through obscurity is not security. This platform is hardened precisely because its architecture is visible."
                        </aside>
                    </section>

                    {/* ── 6. Accessibility ────────────────── */}
                    <section className="wp-section" id="accessibility" aria-labelledby="a11y-title">
                        <div className="wp-section-icon" aria-hidden="true"><Accessibility size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 06</span>
                        <h2 className="wp-section-title" id="a11y-title">Accessibility as a Human Right</h2>
                        <div className="wp-body">
                            <p>
                                One billion people globally live with a disability. In most financial applications, they are an afterthought -
                                a compliance checkbox ticked at the end of a sprint, if at all. This platform treats accessibility as a
                                foundational design constraint, not a remediation task.
                            </p>
                            <p>
                                Every interface meets <strong>WCAG 2.1 Level AA</strong> standards. Semantic HTML provides correct heading hierarchy.
                                ARIA roles and labels ensure screen readers can navigate every workflow. Colour contrast ratios exceed 4.5:1.
                                Keyboard navigation works end-to-end. Focus indicators are visible and consistent. Skip links bypass repetitive navigation.
                            </p>
                            <p>
                                Financial exclusion is not only economic. When a visually impaired user cannot check their balance, when a
                                motor-impaired user cannot complete a transfer, when a cognitively diverse user cannot parse a fee structure -
                                that is exclusion. And it is solvable.
                            </p>
                        </div>

                        <div className="wp-stats">
                            <div className="wp-stat">
                                <div className="wp-stat-value">AA</div>
                                <div className="wp-stat-label">WCAG 2.1 Level</div>
                            </div>
                            <div className="wp-stat">
                                <div className="wp-stat-value">4.5:1</div>
                                <div className="wp-stat-label">Min Contrast Ratio</div>
                            </div>
                            <div className="wp-stat">
                                <div className="wp-stat-value">100%</div>
                                <div className="wp-stat-label">Keyboard Navigable</div>
                            </div>
                        </div>

                        <aside className="wp-pullquote" role="note">
                            "If your financial platform does not work for everyone, it does not work."
                        </aside>
                    </section>

                    {/* ── 7. Performance ──────────────────── */}
                    <section className="wp-section" id="performance" aria-labelledby="perf-title">
                        <div className="wp-section-icon" aria-hidden="true"><Zap size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 07</span>
                        <h2 className="wp-section-title" id="perf-title">Performance at Scale</h2>
                        <div className="wp-body">
                            <p>
                                This platform is validated under load, instrumented for observability,
                                and tested with coverage thresholds that treat reliability as a deliverable, not an aspiration.
                            </p>
                        </div>

                        <div className="wp-stats">
                            <div className="wp-stat">
                                <div className="wp-stat-value">&lt;500ms</div>
                                <div className="wp-stat-label">P95 Latency</div>
                            </div>
                            <div className="wp-stat">
                                <div className="wp-stat-value">80%+</div>
                                <div className="wp-stat-label">Test Coverage</div>
                            </div>
                            <div className="wp-stat">
                                <div className="wp-stat-value">&lt; 5%</div>
                                <div className="wp-stat-label">Error Rate Under Load</div>
                            </div>
                            <div className="wp-stat">
                                <div className="wp-stat-value">CI/CD</div>
                                <div className="wp-stat-label">Automated Pipeline</div>
                            </div>
                        </div>

                        <div className="wp-body">
                            <p>
                                Structured logging through every service provides full request traceability. Load tests simulate concurrent
                                user flows across authentication, transactions, and blockchain interactions. Every deployment is validated
                                against performance baselines before reaching production. Regressions are caught in the pipeline, not by users.
                            </p>
                        </div>

                        <ul className="wp-feature-list" role="list">
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Structured observability</strong> - correlated request IDs across services, latency histograms, error rate dashboards.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Load-tested endpoints</strong> - validated under sustained concurrent traffic. No theoretical throughput claims.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Automated test suites</strong> - unit, integration, and end-to-end coverage enforced in CI. No deployment without passing gates.</span>
                            </li>
                        </ul>

                        <aside className="wp-pullquote" role="note">
                            "This platform does not guess. It measures."
                        </aside>
                    </section>

                    {/* ── 8. Roadmap ──────────────────────── */}
                    <section className="wp-section" id="roadmap" aria-labelledby="roadmap-title">
                        <div className="wp-section-icon" aria-hidden="true"><Map size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 08</span>
                        <h2 className="wp-section-title" id="roadmap-title">Global Scalability Roadmap</h2>
                        <div className="wp-body">
                            <p>
                                Architecture that cannot scale is a prototype. This platform is designed to grow from
                                100 users to 100 million without a rewrite - because the hard decisions were made at the foundation, not deferred to "later."
                            </p>
                        </div>

                        <ul className="wp-feature-list" role="list">
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Multi-region deployment readiness</strong> - stateless services with externalised configuration. Data residency boundaries respected by design.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Kubernetes orchestration path</strong> - containerised services with health checks, readiness probes, and horizontal pod autoscaling defined.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Event sourcing with double-entry ledger</strong> - immutable event logs as the system of record. Every transaction produces balanced LedgerEntry pairs (debit + credit) with ISO 4217 currency codes. Account balances are derived by summing ledger entries, never mutated directly. Full temporal queryability and audit-ready journal history.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>CQRS separation</strong> - read and write models optimised independently. Redis caching accelerates read paths; idempotency filters protect write paths. Query performance scales without compromising transactional integrity.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Service mesh readiness</strong> - mutual TLS, traffic shaping, and circuit breakers at the infrastructure layer rather than application code.</span>
                            </li>
                            <li className="wp-feature-item">
                                <span className="wp-feature-check" aria-hidden="true"><Check size={16} color="currentColor" /></span>
                                <span><strong>Multi-chain expansion</strong> - blockchain service abstracted to support Polygon, Arbitrum, and future L2 networks without core rewrites.</span>
                            </li>
                        </ul>

                        <aside className="wp-pullquote" role="note">
                            "Scale is not a feature you bolt on. It is a consequence of decisions you make on day one."
                        </aside>
                    </section>

                    {/* ── 9. Urgency Section ────────────────── */}
                    <section className="wp-section" id="urgency" aria-labelledby="urgency-title">
                        <div className="wp-section-icon" aria-hidden="true"><Rocket size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 09</span>
                        <h2 className="wp-section-title" id="urgency-title">The World Is Moving - Are You?</h2>
                        <div className="wp-body">
                            <p>
                                There are 2.5 billion people under the age of 18 alive today. They will never know a world without
                                programmable money. They will never accept a three-day settlement window. They will never understand
                                why sending value across a border costs more than sending a video across an ocean. The generation
                                inheriting the global economy does not need to be convinced that finance should be open - they will
                                simply refuse to use systems that are not.
                            </p>
                            <p>
                                Every month that passes, the gap between open financial infrastructure and legacy banking widens.
                                The institutions that adapt will define the next era. The ones that wait will be defined by it.
                                The architecture exists. The standards are published. The only variable left is timing - and timing,
                                once lost, cannot be recovered.
                            </p>
                        </div>

                        <div className="wp-compare">
                            <div className="wp-compare-col wp-compare-gain">
                                <h3 className="wp-compare-heading">What This Architecture Enables</h3>
                                <ul className="wp-compare-list" role="list">
                                    <li>→ Independent scaling of each service under traffic spikes</li>
                                    <li>→ Full transaction audit trail via double-entry ledger and immutable event sourcing</li>
                                    <li>→ Any service replaced without touching others</li>
                                    <li>→ On-chain wallet-signed settlement - no server-side key custody</li>
                                    <li>→ End-to-end distributed tracing via OpenTelemetry and New Relic</li>
                                    <li>→ Sub-millisecond cache hits via Redis; graceful fallback when unavailable</li>
                                    <li>→ Exactly-once write semantics via idempotency key enforcement</li>
                                </ul>
                            </div>
                            <div className="wp-compare-col wp-compare-lose">
                                <h3 className="wp-compare-heading">What It Prevents</h3>
                                <ul className="wp-compare-list" role="list">
                                    <li>→ Cascading failures from a single overloaded service</li>
                                    <li>→ Silent data mutations with no recoverable history</li>
                                    <li>→ Tight coupling that turns refactoring into a rewrite</li>
                                    <li>→ Duplicate financial operations from network retries</li>
                                    <li>→ Retrofitting audit trails under regulatory deadline pressure</li>
                                    <li>→ Observability blind spots across service boundaries</li>
                                </ul>
                            </div>
                        </div>

                        <aside className="wp-pullquote" role="note">
                            "Timing is not everything. But it is the one thing you cannot recover once lost."
                        </aside>
                    </section>

                    {/* ── 10. Legal ───────────────────────── */}
                    <section className="wp-section" id="legal" aria-labelledby="legal-title">
                        <div className="wp-section-icon" aria-hidden="true"><Scale size={20} color="currentColor" /></div>
                        <span className="wp-section-label">Section 10</span>
                        <h2 className="wp-section-title" id="legal-title">Legal &amp; Regulatory Transparency</h2>
                        <div className="wp-body">
                            <p>
                                Transparency extends to what this platform is - and how it operates within the law.
                            </p>
                            <p>
                                This platform is designed to be <strong>globally accessible to users aged 13 and above</strong>, in compliance
                                with international age-of-consent frameworks including GDPR (Article 8), COPPA-equivalent local statutes,
                                and the UN Convention on the Rights of the Child. Users under 18 may require parental or guardian consent
                                depending on their jurisdiction. The platform enforces age-appropriate safeguards and data minimisation
                                by default.
                            </p>
                            <p>
                                The architecture is production-grade. Security controls are real. Compliance patterns - OWASP Top 10,
                                GDPR, ISO 27001, and WCAG 2.1 Level AA - are implemented, not simulated. The codebase is structured
                                to pass a professional security audit. On-chain transactions are executed on the live Ethereum blockchain.
                                Smart contract interactions are wallet-signed, gas-consumed, and publicly verifiable.
                            </p>
                            <p>
                                This platform does not provide personalised financial advice. It does not constitute a securities offering in any jurisdiction. The product, its features, and its safeguards are structured for broad cross-border regulatory alignment so that people around the world can use the application in the ordinary course of life without needing to navigate local licensing, registration, or securities-law regimes. The application is positioned and operated as an informational and utility service rather than as a regulated investment, brokerage, or advisory platform, so that end users can interact with it as a compliant global product experience. To preserve this global operability, the application's functionality, terms, and controls may be refined over time in response to evolving legal and regulatory expectations in key jurisdictions.
                            </p>
                        </div>
                    </section>

                    {/* ── Manifesto ───────────────────── */}
                    <section className="wp-section" id="manifesto" aria-labelledby="manifesto-title">
                        <span className="wp-section-label">Closing</span>
                        <h2 className="wp-section-title" id="manifesto-title">Manifesto</h2>
                        <div className="wp-manifesto">
                            <p>
                                Money is too important to be opaque.<br />
                                Access is too fundamental to be gated.<br />
                                Trust is too critical to be assumed.<br />
                                <strong>Finance should be open.</strong>
                            </p>
                            <div className="wp-manifesto-sig">- Anubhav Sharma</div>
                        </div>
                    </section>

                    {/* ── Footer Badge ────────────────────── */}
                    <footer className="wp-footer" role="contentinfo" aria-label="Whitepaper footer">
                        <span className="wp-footer-badge">Version 1.0 • March 2026</span>
                        <p className="wp-footer-author">Anubhav Sharma · Open Finance Platform · 2026</p>
                    </footer>
                </div>
            </div>
        </div>
    );
};

export default Whitepaper;
