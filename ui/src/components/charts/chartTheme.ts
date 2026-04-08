/* istanbul ignore file */
/**
 * Chart Theme Constants
 * Shared colour palette and defaults for all Recharts components.
 */

export const CHART_COLORS = {
  primary: '#2563eb',
  accent: '#10b981',
  amber: '#f59e0b',
  red: '#ef4444',
  muted: '#6b7280',
  gridLine: '#f3f4f6',
} as const;

export const CHART_DEFAULTS = {
  axisTickFontSize: 11,
  axisTickColor: CHART_COLORS.muted,
  gridStrokeColor: CHART_COLORS.gridLine,
  gridStrokeDashArray: '3 3',
  tooltipBackground: '#ffffff',
  tooltipBorderColor: '#e5e7eb',
  tooltipFontSize: 12,
} as const;

/**
 * Formats a number as a compact dollar amount.
 * e.g. 1200000 -> "$1.2M", 450000 -> "$450K", 12300 -> "$12.3K"
 */
export function currencyFormatter(value: number): string {
  const formatter = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    notation: 'compact',
    maximumFractionDigits: 1,
  });
  return formatter.format(value);
}

/**
 * Formats a number 0–100 as a percentage string.
 * e.g. 72.4 -> "72.4%"
 */
export function percentFormatter(value: number): string {
  return `${value.toFixed(1)}%`;
}
