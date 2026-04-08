# UI Walkthrough

## Authentication

The user lands on a split-screen login page with the platform brand on the left and a credential form on the right, with one-click demo profile selectors for Individual and Corporate access.
![Split-screen login page showing brand panel, demo profile toggle for Individual and Corporate, and email/password form with Log In button](screenshots/auth-login.png)

The user switches to the Corporate demo profile, which instantly prefills corporate admin credentials and highlights the active selector.
![Login page with Corporate profile selected, credentials prefilled, and the Corporate button visually active](screenshots/auth-login-corporate-demo.png)

The user navigates to the registration page where a form (currently disabled for security reasons) displays account type selection, first name, last name, and conditional company fields for Corporate accounts.
![Registration page showing a disabled form with Individual/Corporate toggle, personal details fields, and a notice that registration is currently closed](screenshots/auth-register.png)

## Dashboard

The user sees a personalised welcome banner with a mode badge, a prominent balance card showing total holdings in NZD with trend percentage and available/held breakdown, and a portfolio value area chart.
![Dashboard overview with welcome hero, balance card displaying total NZD balance with trend arrow and available/held split, and a 30-day portfolio area chart](screenshots/dashboard-overview.png)


The user clicks quick action buttons to navigate directly to Send Money, View Accounts, or Manage Budget from the dashboard.
![Dashboard quick actions section with three navigational buttons for common banking tasks](screenshots/dashboard-quick-actions.png)

Corporate users see an additional dark banner displaying their organisation name, role, and a button to open the Corporate Dashboard.
![Corporate access banner on the dashboard with organisation context and a prominent Open Corporate Dashboard button](screenshots/dashboard-corporate-banner.png)

## Accounts

The user views all internal accounts as individual cards, each displaying the account type, masked account number, formatted NZD balance, and a Fund Account button.
![Accounts page showing a list of account cards with type labels, account numbers, balances, and Fund Account action buttons](screenshots/accounts-list.png)

The user creates a new internal account by selecting Checking or Savings from a dropdown and clicking Add New Account.
![Create Internal Account form with account type dropdown set to Checking and an Add New Account submit button](screenshots/accounts-create-form.png)

The user connects an external bank by selecting a country, searching through branded bank tiles with official logos and colours, and authorising the connection.
![Connect Bank panel showing country selector, search input, and a grid of branded bank tiles for NZ, AU, and UK institutions](screenshots/accounts-connect-bank.png)


The user funds an internal account by selecting a linked external bank, entering a deposit amount with real-time validation, and confirming the transfer.
![Fund Account modal showing external account selector with available balances, amount input with validation, and deposit confirmation button](screenshots/accounts-fund-modal.png)


## Transactions

The user selects between three tab views - Send Money, Add Payee, and History - using segmented control buttons at the top of the Transactions page.
![Transactions page with three tab buttons: Send Money (active), Add Payee, and History](screenshots/transactions-tabs.png)

The user composes a payment by selecting a source account, choosing a registered payee, entering an amount, writing a description, and selecting a Conscious Spending Type.
![Send Money form with source account dropdown, payee selector, amount input, description field, and spending type category selector](screenshots/transactions-send-money.png)


The user registers a new payee by entering a name, account number, and optionally selecting from existing platform users.
![Add Payee form with name input, account number field, optional user selector dropdown, and an Add Payee submit button](screenshots/transactions-add-payee.png)

The user browses transaction history with search, type filter (all/credit/debit), spending category filter, date range picker, and column sort toggles for date and amount.
![Transaction history view with search bar, filter dropdowns for type and spending category, date range inputs, and a sortable transaction table](screenshots/transactions-history-filters.png)

Each transaction row displays the description, account number, formatted amount colour-coded by type, a status badge, spending type tag, and formatted date.
![Single transaction row showing description, masked account number, green credit or red debit amount, Completed/Pending status badge, and date](screenshots/transactions-history-row.png)

A 30-day sparkline chart appears above the history table, visualising daily transaction frequency as a compact area graph.
![Mini sparkline area chart showing transaction volume per day over the last 30 days](screenshots/transactions-sparkline.png)

The user exports filtered transactions via a dropdown menu offering CSV (Excel-compatible) and PDF (branded statement with summary totals) formats.
![Export dropdown menu expanded with two options: Download CSV and Download PDF](screenshots/transactions-export-menu.png)

Paginated navigation appears below the transaction list with page numbers, ellipsis for large sets, and a configurable page size selector.
![Pagination bar with numbered page buttons, previous/next arrows, ellipsis indicators, and a page size dropdown](screenshots/transactions-pagination.png)

## Budget

The user selects a financial goal from five strategy cards - Build Wealth, Balanced Living, Enjoy Life, Rapid Growth, and Recovery Mode - each showing target allocation percentages and risk labels.
![Budget goal selection step with five strategy cards displaying names, descriptions, allocation splits, and optional risk/recommended badges](screenshots/budget-goal-selection.png)

The user enters their monthly income and advances to see a visual breakdown of spending against their chosen goal, with a pie chart and bar chart powered by Recharts.
![Budget analysis view with a donut chart showing Fixed/Future/Fun allocation percentages and a bar chart comparing actual spend against target for each category](screenshots/budget-analysis-charts.png)

The user customises allocation sliders for Fixed, Future, and Fun percentages, with preferences persisted to localStorage across sessions.
![Budget customisation panel with three percentage sliders for Fixed, Future, and Fun, and a total validation indicator](screenshots/budget-customise-sliders.png)

## Virtual Cards

The user views issued virtual debit cards rendered as 3D-perspective card visuals with chip, masked number, expiry date, cardholder name, and tilt-on-hover interaction.
![Virtual card gallery showing styled card visuals with gradient backgrounds, chip graphics, masked card numbers, and cardholder details](screenshots/cards-gallery.png)

The user creates a new virtual card by entering a nickname, then sees a one-time reveal screen displaying the full card number and CVV that cannot be retrieved again.
![Card creation flow showing nickname input, followed by a one-time reveal panel displaying the full card number and CVV with a dismissal warning](screenshots/cards-create-reveal.png)

The user freezes, unfreezes, or permanently deletes a card using action buttons on each card, with frozen cards displaying a visual frost overlay and badge.
![Card management actions showing Freeze, Unfreeze, and Delete buttons, with one card displaying a Frozen badge and dimmed visual treatment](screenshots/cards-freeze-delete.png)

## Corporate Banking

The corporate user sees an organisation dashboard with a cash position headline figure, KPI tiles for members, pending approvals, and total batches, plus a 6-month cash flow bar chart.
![Corporate Dashboard with cash position card, three KPI tiles, and a grouped bar chart comparing monthly inflows and outflows](screenshots/corporate-dashboard.png)

The user creates a payment batch by adding multiple payee line items with name, account number, amount, and description, then selecting a currency.
![Payment Batch creation form with currency selector, multiple payee cards with input fields, Add Payee button, and Create Batch submit](screenshots/corporate-batch-create.png)

The user reviews all batches in a paginated list showing currency, item count, total amount, status badge, and a Submit for Approval action on draft batches.
![Payment Batches list with batch cards displaying status pills (Draft, PendingApproval, Approved), amounts, and action buttons](screenshots/corporate-batch-list.png)

An approver reviews pending payment batches on the Approvals page, with Approve and Reject action buttons visible only to Admin and Approver roles.
![Approvals page showing pending batch cards with amount, payment count, submission date, and Approve/Reject decision buttons](screenshots/corporate-approvals-pending.png)

The approver switches to the History tab to review previously decided batches with their final status badges and amounts.
![Approvals history tab showing a list of decided batches with Approved, Rejected, or Executed status badges](screenshots/corporate-approvals-history.png)

The user requests credit by entering an amount, selecting NZD or FTK currency, specifying a purpose, and optionally linking to an external project.
![Request Credit form with amount input, currency selector (NZD/FTK), purpose field, optional project context banner, and Submit button](screenshots/credit-request-form.png)


## Admin

The user views a read-only RBAC matrix displaying six role cards - Super Admin, Compliance Officer, Account Manager, Analyst, Customer, and Demo User - each listing granted and denied permissions.
![Admin page with a grid of role cards, each showing role name, context, description, and a permission checklist with green check and red minus icons](screenshots/admin-rbac-matrix.png)

## Settings

The user navigates a tabbed settings interface with Profile, Security, Notifications, and API Access sections.
![Settings page with a vertical tab list on the left (Profile, Security, Notifications, API Access) and the active tab content on the right](screenshots/settings-tabs.png)

The user configures their timezone via a searchable dropdown with UTC offset labels, plus a one-click Detect Browser Timezone button.
![Profile settings showing a searchable timezone dropdown with UTC offset prefixes, a Detect button, and a Save Changes action](screenshots/settings-profile-timezone.png)

The user manages notification preferences by toggling email and SMS delivery for each event type.
![Notification preferences panel with rows for each event type and toggle switches for Email and SMS channels](screenshots/settings-notifications-prefs.png)

## Notifications

The user opens the notification bell in the top bar to reveal a dropdown listing recent events - transactions, KYC updates, and security alerts - with read/unread indicators and a Mark All Read action.
![Notification bell dropdown showing a scrollable list of event messages with timestamps, unread dot indicators, and a Mark All Read button at the top](screenshots/notifications-bell-dropdown.png)

Critical events like suspicious activity flags or KYC rejections automatically surface as persistent toast banners, independent of the dropdown.


## Global Search

The user activates the global search bar in the top navigation and types a query that searches across accounts, transactions, and payees simultaneously. An empty state appears when no results match the query, displaying a friendly illustration and message.
![Global search overlay with a text input, loading skeleton during fetch, and categorised result groups for Accounts, Transactions, and Payees](screenshots/search-overlay-results.png)

Search results display highlighted matching text, masked account numbers for security, formatted currency amounts, and relative dates.
![Search results showing highlighted query matches, masked account numbers (••••1234), NZD amounts, and relative timestamps like "2 days ago"](screenshots/search-results-highlighted.png)



## F-Mode (Blockchain)

The user activates F-Mode using the segmented Fiat/F-Mode toggle in the sidebar, which switches the entire interface to DeFi context with a toast confirmation.
![Sidebar F-Mode toggle showing Fiat and F-Mode segments, with F-Mode selected and highlighted in accent colour](screenshots/fmode-toggle-active.png)

The user connects their MetaMask wallet via a Connect Wallet component, or enters Demo Mode to explore with simulated balances.
![Connect Wallet panel with a MetaMask connection button, a Demo Mode option, and wallet status indicators](screenshots/fmode-connect-wallet.png)

A network status indicator shows whether the wallet is connected to the correct Sepolia testnet, with a one-click Switch Network action if misconfigured.
![Network status badge showing current chain name, connection status, and a Switch to Sepolia button when on the wrong network](screenshots/fmode-network-status.png)


## Session & Network Awareness

A session expiry banner appears when the JWT token nears expiration - amber at 5 minutes, red at 60 seconds - with a countdown and option to renew.
![Session expiry warning banner in amber showing "Session expires in 4:32" with a Renew Session button](screenshots/session-expiry-banner.png)

Expired sessions trigger a critical toast and automatic redirect to the login page, preserving the intended destination for post-login navigation.
![Critical red toast notification stating "Your session has expired. Please log in again."](screenshots/session-expired-toast.png)

## Feedback

The user opens a feedback modal from the navigation, writes a message with a 2,000-character limit and minimum 10-character validation, and submits it directly to the platform.
![Feedback modal dialog with a textarea, character counter (0/2000), validation message for short input, and Cancel/Submit buttons](screenshots/feedback-modal.png)

## Whitepaper

The user reads the platform whitepaper in a document-style layout with a sticky sidebar table of contents, scroll-reveal section animations, and a Download PDF button.
![Whitepaper page with a sidebar table of contents highlighting the active section, a hero header with version badge, and scroll-animated content sections](screenshots/whitepaper.png)

## Privacy & Terms

The user switches between Privacy Policy and Terms of Service using tab controls, each with its own sidebar table of contents and scroll-tracked active section.
![Privacy page with Privacy/Terms tab toggle, sidebar navigation listing policy sections, and the active section content with legal prose](screenshots/privacy-terms.png)

## Error States

A 404 page displays a large error code, a clear message that the page was not found, and a button to return to the Dashboard.
![404 Not Found page with large "404" text, explanatory message, and a Go to Dashboard navigation button](screenshots/error-404.png)
