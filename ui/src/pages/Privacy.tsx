/* istanbul ignore file */
import * as React from 'react';
import {
  X, Menu, ClipboardList, Settings, Check, Scale, ShieldCheck, Handshake,
  Cookie, Baby, Archive, Globe, Link as LinkIcon, AlertTriangle, FileText,
  Landmark, Users, KeyRound, CheckCircle, Save, CreditCard, Copyright,
  Globe2, DoorOpen,
} from 'lucide-react';
import './Privacy.css';

/* ─── Table of Contents data ─────────────────────── */
const PRIVACY_TOC = [
  { id: 'promise', label: 'Our Promise' },
  { id: 'collect', label: 'Information We Collect' },
  { id: 'usage', label: 'How We Use It' },
  { id: 'storage', label: 'Data Storage & Security' },
  { id: 'sharing', label: 'Data Sharing' },
  { id: 'rights', label: 'Your Rights (NZ)' },
  { id: 'cookies', label: 'Cookies & Tracking' },
  { id: 'children', label: "Children's Privacy" },
  { id: 'retention', label: 'Data Retention' },
  { id: 'transfers', label: 'International Transfers' },
  { id: 'blockchain', label: 'Blockchain Data' },
  { id: 'changes', label: 'Policy Changes' },
];

const TERMS_TOC = [
  { id: 'tos-about', label: 'What This Platform Is' },
  { id: 'tos-eligibility', label: 'Who Can Use This' },
  { id: 'tos-account', label: 'Your Account' },
  { id: 'tos-use', label: 'Acceptable Use' },
  { id: 'tos-data', label: 'Your Data' },
  { id: 'tos-regulatory', label: 'Regulatory Status' },
  { id: 'tos-credit', label: 'Credit Facilities' },
  { id: 'tos-blockchain', label: 'Distributed Ledger' },
  { id: 'tos-ip', label: 'Intellectual Property' },
  { id: 'tos-availability', label: 'Service Availability' },
  { id: 'tos-liability', label: 'Limitation of Liability' },
  { id: 'tos-changes', label: 'Changes to Terms' },
  { id: 'tos-termination', label: 'Ending Agreement' },
  { id: 'tos-general', label: 'General Provisions' },
];

const Privacy: React.FC = () => {
  const [activeTab, setActiveTab] = React.useState<'privacy' | 'terms'>('privacy');
  const [activeId, setActiveId] = React.useState<string>('promise');
  const [tocOpen, setTocOpen] = React.useState(false);

  const currentTOC = activeTab === 'privacy' ? PRIVACY_TOC : TERMS_TOC;

  /* ── Scroll-reveal observer ───────────────────── */
  React.useEffect(() => {
    const sections = document.querySelectorAll<HTMLElement>('.pp-section');
    const revealObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('pp-visible');
          }
        });
      },
      { threshold: 0.1, rootMargin: '0px 0px -40px 0px' }
    );
    sections.forEach((s) => revealObserver.observe(s));
    return () => revealObserver.disconnect();
  }, [activeTab]);

  /* ── Active TOC tracking ──────────────────────── */
  React.useEffect(() => {
    const headings = currentTOC.map((t) => document.getElementById(t.id)).filter(Boolean) as HTMLElement[];
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
  }, [activeTab, currentTOC]);

  /* ── Reset active ID when switching tabs ──────── */
  React.useEffect(() => {
    setActiveId(activeTab === 'privacy' ? 'promise' : 'tos-about');
  }, [activeTab]);

  const scrollTo = (id: string) => {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
    setTocOpen(false);
  };

  return (
    <div className="pp" role="article" aria-label="Privacy Policy and Terms of Service">
      <div className="pp-layout">
        {/* ── Tab Navigation ─────────────────── */}
        <div className="pp-tabs" role="tablist">
          <button
            role="tab"
            aria-selected={activeTab === 'privacy'}
            className={`pp-tab${activeTab === 'privacy' ? ' active' : ''}`}
            onClick={() => setActiveTab('privacy')}
          >
            Privacy Policy
          </button>
          <button
            role="tab"
            aria-selected={activeTab === 'terms'}
            className={`pp-tab${activeTab === 'terms' ? ' active' : ''}`}
            onClick={() => setActiveTab('terms')}
          >
            Terms of Service
          </button>
        </div>
        <div className="pp-tab-divider" />

        {/* ── Mobile TOC Toggle ─────────────────── */}
        <button
          className="pp-toc-toggle"
          onClick={() => setTocOpen((o) => !o)}
          aria-expanded={tocOpen}
          aria-controls="pp-toc-nav"
        >
          {tocOpen ? <><X size={16} color="currentColor" /> Close Contents</> : <><Menu size={16} color="currentColor" /> Table of Contents</>}
        </button>

        <div className="pp-content-layout">
          {/* ── Sidebar TOC ──────────────────────── */}
          <nav
            id="pp-toc-nav"
            className={`pp-toc${tocOpen ? ' pp-toc-open' : ''}`}
            aria-label="Page table of contents"
          >
            <p className="pp-toc-title" aria-hidden="true">Contents</p>
            <ul className="pp-toc-list" role="list">
              {currentTOC.map((item) => (
                <li key={item.id} className="pp-toc-item">
                  <a
                    href={`#${item.id}`}
                    className={`pp-toc-link${activeId === item.id ? ' active' : ''}`}
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
          <div className="pp-main">
            {/* ════════════════════════════════════════════════════════════
                PRIVACY POLICY TAB
               ════════════════════════════════════════════════════════════ */}
            {activeTab === 'privacy' && (
              <>
                {/* Hero Header */}
                <header className="pp-hero" id="promise">
                  <span className="pp-hero-label">Version 1.0 · Effective March 2026</span>
                  <h1>Privacy Policy</h1>
                  <p className="pp-hero-subtitle">FinTech Platform — Open Finance Infrastructure</p>
                </header>

                {/* Our Promise */}
                <div className="pp-promise">
                  <p className="pp-promise-title">Our Promise</p>
                  <p>
                    Your data belongs to you. We collect only what's necessary, we <strong>never sell it</strong>, 
                    and we make it easy for you to access, export, or delete it. This Privacy Policy explains 
                    our practices in plain English.
                  </p>
                </div>

                {/* Section 1: Information We Collect */}
                <section className="pp-section" id="collect" aria-labelledby="collect-title">
                  <div className="pp-section-icon" aria-hidden="true"><ClipboardList size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 01</span>
                  <h2 className="pp-section-title" id="collect-title">Information We Collect</h2>

                  <p className="pp-sub-heading">Information you provide</p>
                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Account details:</strong> Name, email address, username, timezone preference</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Financial data:</strong> Accounts, transactions, budgets, credit requests you create</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Blockchain data:</strong> Wallet addresses and on-chain transaction hashes when using F-Mode</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Communications:</strong> Messages you send us (support requests, feedback)</span>
                    </li>
                  </ul>

                  <p className="pp-sub-heading">Information collected automatically</p>
                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Usage data:</strong> Features you use, pages you visit, actions you take within the app</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Server logs:</strong> IP addresses and request metadata logged by CDN for security (auto-deleted)</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Performance data:</strong> Error reports and service performance metrics</span>
                    </li>
                  </ul>

                  <p className="pp-sub-heading">Information we do NOT collect</p>
                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-cross"><X size={16} color="currentColor" /></span>
                      <span>We do not collect biometric data</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-cross"><X size={16} color="currentColor" /></span>
                      <span>We do not use third-party advertising trackers</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-cross"><X size={16} color="currentColor" /></span>
                      <span>We do not store your private keys or wallet seed phrases</span>
                    </li>
                  </ul>
                </section>

                {/* Section 2: How We Use Your Information */}
                <section className="pp-section" id="usage" aria-labelledby="usage-title">
                  <div className="pp-section-icon" aria-hidden="true"><Settings size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 02</span>
                  <h2 className="pp-section-title" id="usage-title">How We Use Your Information</h2>

                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Provide the service:</strong> Store your accounts, display your data, enable transactions</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Improve the experience:</strong> Understand usage patterns to make FinTech Platform better</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Security:</strong> Detect and prevent fraud, abuse, and unauthorized access</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Compliance:</strong> Sanctions screening and regulatory obligations</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Communications:</strong> Send essential notifications (security alerts, service updates). Marketing emails are opt-in only.</span>
                    </li>
                  </ul>

                  <div className="pp-callout">
                    <span className="pp-callout-icon" aria-hidden="true"><Scale size={20} color="currentColor" /></span>
                    <div className="pp-callout-text">
                      <strong>Legal basis:</strong> We process your data based on contract performance (providing the service you signed up for) 
                      and legitimate interest (security, service improvement, compliance obligations).
                    </div>
                  </div>
                </section>

                {/* Section 3: Data Storage & Security */}
                <section className="pp-section" id="storage" aria-labelledby="storage-title">
                  <div className="pp-section-icon" aria-hidden="true"><ShieldCheck size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 03</span>
                  <h2 className="pp-section-title" id="storage-title">Data Storage & Security</h2>

                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Encryption at rest and in transit:</strong> Your data is encrypted using industry-standard AES-256 encryption at rest and TLS 1.2+ in transit</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>OWASP Top 10 compliant:</strong> Security practices follow industry-standard vulnerability prevention</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Password hashing:</strong> PBKDF2 with 310,000 iterations — your password is never stored in plain text</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Access controls:</strong> Only authorized personnel can access production systems, with audit trails</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Dependencies scanned weekly:</strong> We conduct regular security assessments and dependency scans via SonarCloud</span>
                    </li>
                  </ul>

                  <aside className="pp-pullquote" role="note">
                    "Security is not a feature — it's the foundation. Every line of code is written with OWASP Top 10 compliance in mind."
                  </aside>
                </section>

                {/* Section 4: Data Sharing */}
                <section className="pp-section" id="sharing" aria-labelledby="sharing-title">
                  <div className="pp-section-icon" aria-hidden="true"><Handshake size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 04</span>
                  <h2 className="pp-section-title" id="sharing-title">Data Sharing</h2>

                  <div className="pp-highlight">
                    <p><strong>We never sell your data. Period.</strong></p>
                  </div>

                  <div className="pp-body">
                    <p>We may share data only in these limited circumstances:</p>
                  </div>

                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Service providers:</strong> Essential infrastructure providers (Heroku hosting, CloudAMQP messaging) who are contractually bound to protect your data</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Blockchain networks:</strong> When using F-Mode, transaction data is published to public blockchain networks by design</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Legal requirements:</strong> When required by law, court order, or regulatory authority</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>With your consent:</strong> Any other sharing only with your explicit permission</span>
                    </li>
                  </ul>
                </section>

                {/* Section 5: Your Rights */}
                <section className="pp-section" id="rights" aria-labelledby="rights-title">
                  <div className="pp-section-icon" aria-hidden="true">🇳🇿</div>
                  <span className="pp-section-label">Section 05</span>
                  <h2 className="pp-section-title" id="rights-title">Your Rights (NZ Privacy Act 2020)</h2>

                  <div className="pp-body">
                    <p>
                      This platform operates in compliance with the <strong>New Zealand Privacy Act 2020</strong>. 
                      Under this Act, you have the following rights in relation to your personal information:
                    </p>
                  </div>

                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Right of access (Principle 6)</strong> — you may request confirmation of whether we hold personal information about you, and access that information</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Right of correction (Principle 7)</strong> — you may request correction of any personal information we hold that is inaccurate, out of date, incomplete, or misleading</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Right to complain (s69)</strong> — you may make a complaint to us if you believe we have interfered with your privacy. We will respond within 20 working days</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Right to escalate</strong> — if you are not satisfied with our response, you may lodge a complaint with the <strong>Office of the Privacy Commissioner</strong> at <strong>privacy.org.nz</strong></span>
                    </li>
                  </ul>

                  <div className="pp-callout">
                    <span className="pp-callout-icon" aria-hidden="true"><ClipboardList size={20} color="currentColor" /></span>
                    <div className="pp-callout-text">
                      To exercise any of these rights, contact us at{' '}
                      <strong>anubhav.sharma.work@outlook.com</strong>. We will acknowledge your request promptly 
                      and respond within the timeframes required by the NZ Privacy Act 2020.
                    </div>
                  </div>
                </section>

                {/* Section 6: Cookies & Tracking */}
                <section className="pp-section" id="cookies" aria-labelledby="cookies-title">
                  <div className="pp-section-icon" aria-hidden="true"><Cookie size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 06</span>
                  <h2 className="pp-section-title" id="cookies-title">Cookies & Tracking</h2>

                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-check"><Check size={16} color="currentColor" /></span>
                      <span><strong>Essential cookies only by default</strong> — required for authentication and basic functionality</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Optional analytics:</strong> If we implement analytics, you'll be asked for consent first. You can opt out anytime</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-cross"><X size={16} color="currentColor" /></span>
                      <span><strong>No third-party advertising trackers.</strong> We don't display ads and don't allow ad networks to track you</span>
                    </li>
                  </ul>
                </section>

                {/* Section 7: Children's Privacy */}
                <section className="pp-section" id="children" aria-labelledby="children-title">
                  <div className="pp-section-icon" aria-hidden="true"><Baby size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 07</span>
                  <h2 className="pp-section-title" id="children-title">Children's Privacy</h2>

                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span>FinTech Platform is designed for users aged <strong>13 and older</strong> (COPPA compliant)</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span>We do not knowingly collect data from children under 13</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span>If you're a parent and believe your child has created an account, contact us and we'll promptly delete it</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span>In jurisdictions where a higher age of digital consent applies (e.g., 16 in some EU countries), that local age requirement applies</span>
                    </li>
                  </ul>
                </section>

                {/* Section 8: Data Retention */}
                <section className="pp-section" id="retention" aria-labelledby="retention-title">
                  <div className="pp-section-icon" aria-hidden="true"><Archive size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 08</span>
                  <h2 className="pp-section-title" id="retention-title">Data Retention</h2>

                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Active accounts:</strong> We keep your data for as long as your account is active</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Deleted accounts:</strong> Data is removed within 30 days of account deletion request</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Backups:</strong> Encrypted backups are rotated and destroyed on a regular schedule</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span><strong>Legal holds:</strong> We may retain data longer if required by law or ongoing legal proceedings</span>
                    </li>
                  </ul>
                </section>

                {/* Section 9: International Data Transfers */}
                <section className="pp-section" id="transfers" aria-labelledby="transfers-title">
                  <div className="pp-section-icon" aria-hidden="true"><Globe size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 09</span>
                  <h2 className="pp-section-title" id="transfers-title">International Data Transfers</h2>

                  <div className="pp-body">
                    <p>
                      Your data may be processed in the region where our infrastructure is hosted (Heroku cloud infrastructure). 
                      When data crosses borders, we use appropriate safeguards including standard contractual clauses, 
                      adequacy decisions, or equivalent protections.
                    </p>
                  </div>
                </section>

                {/* Section 10: Blockchain & Distributed Ledger Data */}
                <section className="pp-section" id="blockchain" aria-labelledby="blockchain-title">
                  <div className="pp-section-icon" aria-hidden="true"><LinkIcon size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 10</span>
                  <h2 className="pp-section-title" id="blockchain-title">Blockchain & Distributed Ledger Data</h2>

                  <div className="pp-callout">
                    <span className="pp-callout-icon" aria-hidden="true"><AlertTriangle size={20} color="currentColor" /></span>
                    <div className="pp-callout-text">
                      <strong>Important:</strong> When you use F-Mode (distributed ledger features), transactions are recorded on public 
                      blockchain networks (Ethereum Sepolia testnet). This data is <strong>immutable and publicly visible</strong> by design.
                    </div>
                  </div>

                  <ul className="pp-list">
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span>Wallet addresses and transaction hashes are visible on public block explorers (e.g., Etherscan)</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span>We do not store your private keys or seed phrases</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span>Blockchain transactions cannot be deleted or modified once confirmed</span>
                    </li>
                    <li className="pp-list-item">
                      <span className="pp-list-dot">•</span>
                      <span>You are solely responsible for securing your wallet credentials</span>
                    </li>
                  </ul>
                </section>

                {/* Section 11: Changes to This Policy */}
                <section className="pp-section" id="changes" aria-labelledby="changes-title">
                  <div className="pp-section-icon" aria-hidden="true"><FileText size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 11</span>
                  <h2 className="pp-section-title" id="changes-title">Changes to This Policy</h2>

                  <div className="pp-body">
                    <p>
                      We'll update this policy as our practices evolve. For material changes, we'll notify you through the app 
                      and may require re-acceptance. The version number and effective date at the top of this page will always 
                      reflect the current version.
                    </p>
                  </div>
                </section>

                {/* Contact Section */}
                <div className="pp-contact">
                  <p className="pp-contact-heading">Contact Us</p>
                  <p>Questions, concerns, or requests? We're here to help.</p>
                  <p>
                    <strong>Name:</strong> Anubhav Sharma<br />
                    <strong>Email:</strong> <a href="mailto:anubhav.sharma.work@outlook.com">anubhav.sharma.work@outlook.com</a>
                  </p>
                </div>

                {/* Footer */}
                <div className="pp-footer">
                  <span className="pp-footer-badge">Privacy First</span>
                  <p>
                    This policy is written to be understood, not to confuse. We believe privacy is a fundamental right — not a checkbox. 
                    Your trust is what keeps FinTech Platform running.
                  </p>
                </div>
              </>
            )}

            {/* ════════════════════════════════════════════════════════════
                TERMS OF SERVICE TAB
               ════════════════════════════════════════════════════════════ */}
            {activeTab === 'terms' && (
              <>
                {/* Hero Header */}
                <header className="pp-hero" id="tos-about">
                  <span className="pp-hero-label">Version 1.0 · Effective March 2026</span>
                  <h1>Terms of Service</h1>
                  <p className="pp-hero-subtitle">FinTech Platform — Open Finance Infrastructure</p>
                </header>

                {/* Welcome Promise */}
                <div className="pp-promise">
                  <p className="pp-promise-title">Welcome</p>
                  <p>
                    These Terms of Service ("Terms") constitute a binding agreement between you and FinTech Platform. 
                    We have written them in <strong>plain English</strong> because trust is built through transparency, not fine print.
                  </p>
                </div>

                {/* Section 1: What This Platform Is */}
                <section className="pp-section" aria-labelledby="tos-about-title">
                  <div className="pp-section-icon" aria-hidden="true"><Landmark size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 01</span>
                  <h2 className="pp-section-title" id="tos-about-title">What This Platform Is</h2>
                  <div className="pp-body">
                    <p>
                      FinTech Platform is a comprehensive financial management application providing multi-account management, 
                      transaction processing, budget tracking, credit facilities, compliance screening, distributed ledger 
                      integration, and audit capabilities. The application is positioned and operated as an informational and 
                      utility service rather than as a regulated investment, brokerage, or advisory platform, enabling users 
                      to interact with it as a compliant global product experience.
                    </p>
                  </div>
                </section>

                {/* Section 2: Who Can Use This Platform */}
                <section className="pp-section" id="tos-eligibility" aria-labelledby="tos-eligibility-title">
                  <div className="pp-section-icon" aria-hidden="true"><Users size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 02</span>
                  <h2 className="pp-section-title" id="tos-eligibility-title">Who Can Use This Platform</h2>
                  <ul className="pp-tos-list">
                    <li>You must be at least <strong>13 years old</strong> to create an account. Users under 18 may require parental or guardian consent depending on their jurisdiction, in compliance with GDPR (Article 8), COPPA, and local age-of-consent frameworks.</li>
                    <li>You may use the platform for personal financial management, business operations, or organizational purposes.</li>
                    <li>By creating an account, you confirm you have the legal capacity to agree to these Terms and are not prohibited from using financial services under applicable law.</li>
                  </ul>
                </section>

                {/* Section 3: Your Account */}
                <section className="pp-section" id="tos-account" aria-labelledby="tos-account-title">
                  <div className="pp-section-icon" aria-hidden="true"><KeyRound size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 03</span>
                  <h2 className="pp-section-title" id="tos-account-title">Your Account</h2>
                  <ul className="pp-tos-list">
                    <li><strong>Keep it secure.</strong> You are responsible for maintaining the security of your account credentials. Use a strong password and do not share your login.</li>
                    <li><strong>Keep it accurate.</strong> Provide truthful information when you register and keep it up to date.</li>
                    <li><strong>One person, one account.</strong> Each account is for a single individual, though you may manage multiple financial accounts within the platform.</li>
                  </ul>
                </section>

                {/* Section 4: Acceptable Use */}
                <section className="pp-section" id="tos-use" aria-labelledby="tos-use-title">
                  <div className="pp-section-icon" aria-hidden="true"><CheckCircle size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 04</span>
                  <h2 className="pp-section-title" id="tos-use-title">Acceptable Use</h2>
                  <div className="pp-body">
                    <p>Use this platform for legitimate financial management purposes. You agree not to:</p>
                  </div>
                  <ul className="pp-tos-list">
                    <li>Violate any applicable laws, regulations, or third-party rights</li>
                    <li>Upload malicious software, spam, or harmful content</li>
                    <li>Attempt to access other users' accounts or data without authorization</li>
                    <li>Overload, disrupt, or interfere with the service or its infrastructure</li>
                    <li>Circumvent compliance controls, sanctions screening, or security measures</li>
                    <li>Submit fraudulent, misleading, or materially inaccurate financial information</li>
                    <li>Use the platform for money laundering, terrorist financing, or other illicit activities</li>
                  </ul>
                  <div className="pp-body">
                    <p>
                      We reserve the right to suspend or terminate accounts that violate these guidelines, 
                      with notice whenever practical and permitted by law.
                    </p>
                  </div>
                </section>

                {/* Section 5: Your Data, Your Ownership */}
                <section className="pp-section" id="tos-data" aria-labelledby="tos-data-title">
                  <div className="pp-section-icon" aria-hidden="true"><Save size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 05</span>
                  <h2 className="pp-section-title" id="tos-data-title">Your Data, Your Ownership</h2>
                  <ul className="pp-tos-list">
                    <li><strong>You own your content.</strong> Everything you create — accounts, transactions, budgets, credit requests — belongs to you (or your organization).</li>
                    <li><strong>We do not sell your data.</strong> Ever. Your financial data is yours alone.</li>
                    <li><strong>You can export and delete.</strong> You have the right to export your data and delete your account at any time.</li>
                    <li><strong>Limited license to us.</strong> You grant us a limited license to store, process, and display your content solely to provide the service to you.</li>
                  </ul>
                </section>

                {/* Section 6: Service Classification and Regulatory Status */}
                <section className="pp-section" id="tos-regulatory" aria-labelledby="tos-regulatory-title">
                  <div className="pp-section-icon" aria-hidden="true"><Scale size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 06</span>
                  <h2 className="pp-section-title" id="tos-regulatory-title">Service Classification and Regulatory Status</h2>
                  <div className="pp-highlight">
                    <p>
                      <strong>Important:</strong> This application is positioned and operated as an informational and utility 
                      service rather than as a regulated investment, brokerage, or advisory platform. This classification 
                      enables users to interact with it as a compliant global product experience. To preserve this global 
                      operability, the application's functionality, terms, and controls may be refined over time in response 
                      to evolving legal and regulatory expectations in key jurisdictions.
                    </p>
                  </div>
                </section>

                {/* Section 7: Credit Facilities and Compliance Screening */}
                <section className="pp-section" id="tos-credit" aria-labelledby="tos-credit-title">
                  <div className="pp-section-icon" aria-hidden="true"><CreditCard size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 07</span>
                  <h2 className="pp-section-title" id="tos-credit-title">Credit Facilities and Compliance Screening</h2>
                  <div className="pp-body">
                    <p>
                      Credit request features enable users to submit funding applications subject to review and approval 
                      processes. All credit decisions are made at the sole discretion of the platform and may involve 
                      third-party verification. Sanctions and compliance screening is conducted using authoritative data 
                      sources to meet regulatory obligations. Users found on restricted lists or failing compliance checks 
                      may have their access limited or terminated.
                    </p>
                  </div>
                </section>

                {/* Section 8: Distributed Ledger Features (F-Mode) */}
                <section className="pp-section" id="tos-blockchain" aria-labelledby="tos-blockchain-title">
                  <div className="pp-section-icon" aria-hidden="true"><LinkIcon size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 08</span>
                  <h2 className="pp-section-title" id="tos-blockchain-title">Distributed Ledger Features (F-Mode)</h2>
                  <div className="pp-body">
                    <p>
                      When using F-Mode (distributed ledger features), you interact with blockchain networks for enhanced 
                      transaction transparency and verification. You are solely responsible for securing your wallet 
                      credentials and private keys. Blockchain transactions are immutable by design — we cannot recover 
                      lost private keys or reverse confirmed transactions. You acknowledge that digital asset values may 
                      fluctuate and that you bear all associated risks.
                    </p>
                  </div>
                </section>

                {/* Section 9: Intellectual Property */}
                <section className="pp-section" id="tos-ip" aria-labelledby="tos-ip-title">
                  <div className="pp-section-icon" aria-hidden="true"><Copyright size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 09</span>
                  <h2 className="pp-section-title" id="tos-ip-title">Intellectual Property</h2>
                  <ul className="pp-tos-list">
                    <li><strong>Platform ownership.</strong> The FinTech Platform software, design, branding, and proprietary technologies are our intellectual property, protected by applicable copyright, trademark, and other laws.</li>
                    <li><strong>Third-party components.</strong> This platform incorporates third-party and open-source technologies. Respective licenses govern those components.</li>
                    <li><strong>Continuous improvement.</strong> We may add, modify, or discontinue features to enhance the service, ensure compliance, or respond to market conditions. We will provide reasonable notice for material changes where practical.</li>
                  </ul>
                </section>

                {/* Section 10: Service Availability */}
                <section className="pp-section" id="tos-availability" aria-labelledby="tos-availability-title">
                  <div className="pp-section-icon" aria-hidden="true"><Globe2 size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 10</span>
                  <h2 className="pp-section-title" id="tos-availability-title">Service Availability</h2>
                  <div className="pp-body">
                    <p>
                      We aim for high availability but cannot guarantee 100% uptime. Maintenance, updates, and unforeseen 
                      issues may cause occasional interruptions. We will make reasonable efforts to notify you of planned 
                      downtime in advance.
                    </p>
                  </div>
                </section>

                {/* Section 11: Limitation of Liability */}
                <section className="pp-section" id="tos-liability" aria-labelledby="tos-liability-title">
                  <div className="pp-section-icon" aria-hidden="true"><AlertTriangle size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 11</span>
                  <h2 className="pp-section-title" id="tos-liability-title">Limitation of Liability</h2>
                  <ul className="pp-tos-list">
                    <li>This platform is provided "as is" and "as available" without warranties of any kind, express or implied, including but not limited to merchantability, fitness for a particular purpose, or non-infringement.</li>
                    <li>To the maximum extent permitted by law, we are not liable for any indirect, incidental, special, consequential, or punitive damages arising from your use of the service, including but not limited to loss of profits, data, or business opportunities.</li>
                    <li>Our total liability for any claim arising from these Terms shall not exceed the fees paid by you in the twelve months preceding the claim.</li>
                    <li>Nothing in these Terms excludes or limits liability that cannot be excluded or limited by applicable law, including liability for fraud, gross negligence, or willful misconduct.</li>
                  </ul>
                </section>

                {/* Section 12: Changes to These Terms */}
                <section className="pp-section" id="tos-changes" aria-labelledby="tos-changes-title">
                  <div className="pp-section-icon" aria-hidden="true"><FileText size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 12</span>
                  <h2 className="pp-section-title" id="tos-changes-title">Changes to These Terms</h2>
                  <div className="pp-body">
                    <p>
                      We may update these Terms from time to time. When we do, we will update the version number and effective date. 
                      For material changes, we will notify you through the app. Continued use after notification constitutes acceptance 
                      of the updated Terms.
                    </p>
                  </div>
                </section>

                {/* Section 13: Ending Our Agreement */}
                <section className="pp-section" id="tos-termination" aria-labelledby="tos-termination-title">
                  <div className="pp-section-icon" aria-hidden="true"><DoorOpen size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 13</span>
                  <h2 className="pp-section-title" id="tos-termination-title">Ending Our Agreement</h2>
                  <ul className="pp-tos-list">
                    <li><strong>You can leave anytime.</strong> Delete your account and your data will be removed according to our Privacy Policy.</li>
                    <li><strong>We can terminate for cause.</strong> If you seriously or repeatedly violate these Terms, we may suspend or terminate your account with notice.</li>
                    <li><strong>Data after termination.</strong> We will retain your data for a reasonable period to allow export, then delete it.</li>
                  </ul>
                </section>

                {/* Section 14: General Provisions */}
                <section className="pp-section" id="tos-general" aria-labelledby="tos-general-title">
                  <div className="pp-section-icon" aria-hidden="true"><ClipboardList size={20} color="currentColor" /></div>
                  <span className="pp-section-label">Section 14</span>
                  <h2 className="pp-section-title" id="tos-general-title">General Provisions</h2>
                  <ul className="pp-tos-list">
                    <li><strong>Severability.</strong> If any provision of these Terms is found unenforceable, the remaining provisions continue in full force and effect.</li>
                    <li><strong>Entire agreement.</strong> These Terms, together with our Privacy Policy, constitute the entire agreement between you and FinTech Platform regarding the service.</li>
                    <li><strong>No waiver.</strong> Our failure to enforce any right or provision does not constitute a waiver of that right or provision.</li>
                    <li><strong>Assignment.</strong> You may not assign or transfer these Terms without our prior written consent. We may assign our rights and obligations without restriction.</li>
                    <li><strong>Regulatory Adaptation.</strong> To preserve global operability and compliance, we may modify platform functionality, these Terms, or operational controls in response to evolving legal and regulatory expectations in key jurisdictions.</li>
                  </ul>
                </section>

                {/* Contact Section */}
                <div className="pp-contact">
                  <p className="pp-contact-heading">Contact Us</p>
                  <p>Questions about these Terms? We're here to help.</p>
                  <p>
                    <strong>Name:</strong> Anubhav Sharma<br />
                    <strong>Email:</strong> <a href="mailto:anubhav.sharma.work@outlook.com">anubhav.sharma.work@outlook.com</a>
                  </p>
                </div>

                {/* Footer */}
                <div className="pp-footer">
                  <span className="pp-footer-badge">Fair & Transparent</span>
                  <p>
                    These Terms are written in plain English to be understood by everyone, everywhere. 
                    They are designed to be fair, sustainable, and globally compliant — because great tools deserve great trust.
                  </p>
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Privacy;
