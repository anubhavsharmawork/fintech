import * as React from 'react';
import { LineChart, Line, ResponsiveContainer } from 'recharts';

interface BalanceCardProps {
  total: number;
  currency?: string;
  status?: 'healthy' | 'warning' | 'low' | 'excellent';
  showTrend?: boolean;
  trendPercent?: number;
  availableBalance?: number;
  heldBalance?: number;
  sparklineData?: number[];
}

const BalanceCard: React.FC<BalanceCardProps> = ({
  total,
  currency = 'NZD',
  status = 'healthy',
  showTrend = false,
  trendPercent = 0,
  availableBalance,
  heldBalance,
  sparklineData,
}) => {
  const formatter = new Intl.NumberFormat('en-NZ', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  });

  const getStatusColor = (s: string) => {
    switch (s) {
      case 'excellent':
        return '#34d399';
      case 'healthy':
        return '#22d3ee';
      case 'warning':
        return '#fbbf24';
      case 'low':
        return '#f87171';
      default:
        return '#94a3b8';
    }
  };

  const getStatusLabel = (s: string) => {
    switch (s) {
      case 'excellent':
        return 'Excellent Balance';
      case 'healthy':
        return 'Healthy Balance';
      case 'warning':
        return 'Balance Warning';
      case 'low':
        return 'Low Balance';
      default:
        return 'Balance';
    }
  };

  const trendIsUp = trendPercent > 0;
  const sparkChartData = sparklineData?.map(v => ({ v }));
  const sparkIsUp = sparklineData && sparklineData.length > 1
    ? sparklineData[sparklineData.length - 1] >= sparklineData[0]
    : true;

  return (
    <div
      className="balance-card card"
      role="region"
      aria-label="Total Balance Overview"
      style={{ padding: '16px 20px' }}
    >
      {/* Header: label + trend chip */}
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: 4,
      }}>
        <div
          style={{
            fontSize: 'var(--font-size-xs)',
            fontWeight: 600,
            letterSpacing: '0.5px',
            textTransform: 'uppercase',
            color: '#94a3b8',
          }}
        >
          Total Balance
        </div>
        {showTrend && trendPercent !== 0 && (
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 2,
              height: 20,
              padding: '0 6px',
              fontSize: 'var(--font-size-xs)',
              fontWeight: 600,
              borderRadius: 4,
              backgroundColor: trendIsUp ? 'rgba(52, 211, 153, 0.15)' : 'rgba(248, 113, 113, 0.15)',
              color: trendIsUp ? '#6ee7b7' : '#fca5a5',
              fontVariantNumeric: 'tabular-nums',
            }}
          >
            {trendIsUp ? '▲' : '▼'}
            {trendIsUp ? '+' : ''}
            {trendPercent}% this month
          </span>
        )}
      </div>

      {/* Balance + sparkline row */}
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-end',
      }}>
        <div
          role="heading"
          aria-level={2}
          aria-label={`Total balance ${formatter.format(total)}`}
          style={{
            fontSize: 'var(--font-size-2xl)',
            fontWeight: 700,
            lineHeight: 1.1,
            fontVariantNumeric: 'tabular-nums',
            letterSpacing: '-0.5px',
            color: '#ffffff',
          }}
        >
          {formatter.format(total)}
        </div>
        {sparkChartData && sparkChartData.length > 1 && (
          <div style={{ width: 80, height: 60, flexShrink: 0 }} aria-hidden="true">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={sparkChartData}>
                <Line
                  type="monotone"
                  dataKey="v"
                  stroke={sparkIsUp ? '#059669' : '#dc2626'}
                  strokeWidth={1.5}
                  dot={false}
                  isAnimationActive={false}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </div>

      {/* Available / Held row */}
      {(availableBalance !== undefined || heldBalance !== undefined) && (
        <div style={{
          display: 'flex',
          gap: 24,
          marginTop: 12,
          paddingTop: 12,
          borderTop: '1px solid rgba(255, 255, 255, 0.15)',
          fontSize: 'var(--font-size-xs)',
          color: '#94a3b8',
        }}>
          {availableBalance !== undefined && (
            <div style={{ display: 'flex', gap: 6 }}>
              <span>Available</span>
              <span style={{
                fontWeight: 600,
                color: '#ffffff',
                fontVariantNumeric: 'tabular-nums',
              }}>
                {formatter.format(availableBalance)}
              </span>
            </div>
          )}
          {heldBalance !== undefined && (
            <div style={{ display: 'flex', gap: 6 }}>
              <span>Held</span>
              <span style={{
                fontWeight: 600,
                color: '#ffffff',
                fontVariantNumeric: 'tabular-nums',
              }}>
                {formatter.format(heldBalance)}
              </span>
            </div>
          )}
        </div>
      )}

      {/* Status indicator */}
      <span
        role="status"
        aria-label={getStatusLabel(status)}
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: 6,
          marginTop: 12,
          fontSize: 'var(--font-size-xs)',
          fontWeight: 500,
          width: 'fit-content',
        }}
      >
        <span
          aria-hidden="true"
          style={{
            display: 'inline-block',
            width: 6,
            height: 6,
            borderRadius: '50%',
            backgroundColor: getStatusColor(status),
          }}
        />
        {getStatusLabel(status)}
      </span>
    </div>
  );
};

export default BalanceCard;
