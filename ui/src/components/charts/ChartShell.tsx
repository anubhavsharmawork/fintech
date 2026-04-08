import React from 'react';

interface ChartShellProps {
  title: string;
  subtitle?: string;
  period?: string[];
  onPeriodChange?: (period: string) => void;
  children: React.ReactNode;
}

/**
 * ChartShell – Reusable wrapper for all charts.
 * Renders a card with title, optional subtitle, optional period toggle, and chart content.
 */
const ChartShell: React.FC<ChartShellProps> = ({
  title,
  subtitle,
  period,
  onPeriodChange,
  children,
}) => {
  const [activePeriod, setActivePeriod] = React.useState<string>(
    period && period.length > 0 ? period[0] : ''
  );

  const handlePeriodClick = (p: string) => {
    setActivePeriod(p);
    onPeriodChange?.(p);
  };

  return (
    <div style={styles.card}>
      <div style={styles.header}>
        <div style={styles.headerText}>
          <h3 style={styles.title}>{title}</h3>
          {subtitle && <p style={styles.subtitle}>{subtitle}</p>}
        </div>
        {period && period.length > 0 && (
          <div style={styles.periodToggle}>
            {period.map((p) => (
              <button
                key={p}
                type="button"
                onClick={() => handlePeriodClick(p)}
                style={{
                  ...styles.periodButton,
                  ...(activePeriod === p ? styles.periodButtonActive : styles.periodButtonInactive),
                }}
              >
                {p}
              </button>
            ))}
          </div>
        )}
      </div>
      <div style={styles.content}>{children}</div>
    </div>
  );
};

const styles: Record<string, React.CSSProperties> = {
  card: {
    background: '#ffffff',
    border: '1px solid var(--border, #e5e7eb)',
    borderRadius: 8,
    boxShadow: 'var(--shadow-sm, 0 1px 3px rgba(0,0,0,0.08))',
  },
  header: {
    display: 'flex',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    padding: '16px 16px 0 16px',
    gap: 16,
  },
  headerText: {
    display: 'flex',
    flexDirection: 'column',
    gap: 2,
  },
  title: {
    margin: 0,
    fontSize: 14,
    fontWeight: 600,
    color: 'var(--text, #111827)',
  },
  subtitle: {
    margin: 0,
    fontSize: 12,
    fontWeight: 400,
    color: 'var(--muted, #6b7280)',
  },
  periodToggle: {
    display: 'flex',
    gap: 4,
    flexShrink: 0,
  },
  periodButton: {
    padding: '4px 10px',
    fontSize: 11,
    fontWeight: 500,
    border: 'none',
    borderRadius: 4,
    cursor: 'pointer',
    transition: 'background 0.15s ease, color 0.15s ease',
  },
  periodButtonActive: {
    background: 'var(--primary, #2563eb)',
    color: '#ffffff',
  },
  periodButtonInactive: {
    background: 'var(--bg, #f9fafb)',
    color: 'var(--muted, #6b7280)',
  },
  content: {
    padding: 16,
  },
};

export default ChartShell;
