import * as React from 'react';

interface BalanceCardProps {
  total: number;
  currency?: string;
  status?: 'healthy' | 'warning' | 'low' | 'excellent';
  showTrend?: boolean;
  trendPercent?: number;
}

const BalanceCard: React.FC<BalanceCardProps> = ({
  total,
  currency = 'NZD',
  status = 'healthy',
  showTrend = false,
  trendPercent = 0,
}) => {
  const formatter = new Intl.NumberFormat('en-NZ', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  });

  const getStatusColor = (s: string) => {
    switch (s) {
      case 'excellent':
        return '#059669';
      case 'healthy':
        return '#0891b2';
      case 'warning':
        return '#f59e0b';
      case 'low':
        return '#dc2626';
      default:
        return '#6b7280';
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

  return (
    <div
      className="balance-card"
      role="region"
      aria-label="Total Balance Overview"
      style={{
        background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
        color: '#fff',
        padding: '32px 28px',
        borderRadius: '12px',
        marginBottom: '24px',
        boxShadow: '0 10px 30px rgba(102, 126, 234, 0.3)',
        position: 'relative',
        overflow: 'hidden',
      }}
    >
      {/* Decorative background circle */}
      <div
        style={{
          position: 'absolute',
          top: '-50px',
          right: '-50px',
          width: '150px',
          height: '150px',
          background: 'rgba(255, 255, 255, 0.1)',
          borderRadius: '50%',
        }}
        aria-hidden="true"
      />

      <div style={{ position: 'relative', zIndex: 1 }}>
        {/* Label */}
        <div
          style={{
            fontSize: '14px',
            fontWeight: 600,
            letterSpacing: '0.5px',
            textTransform: 'uppercase',
            opacity: 0.9,
            marginBottom: '8px',
          }}
        >
          Total Balance
        </div>

        {/* Main Balance Amount */}
        <div
          style={{
            fontSize: '48px',
            fontWeight: 700,
            lineHeight: 1,
            marginBottom: '16px',
            letterSpacing: '-1px',
          }}
          role="heading"
          aria-level={2}
          aria-label={`Total balance ${formatter.format(total)}`}
        >
          {formatter.format(total)}
        </div>

        {/* Trend */}
        {showTrend && trendPercent !== 0 && (
          <div
            style={{
              fontSize: '14px',
              marginBottom: '12px',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            <span>{trendPercent > 0 ? '📈' : '📉'}</span>
            <span>
              {trendPercent > 0 ? '+' : ''}
              {trendPercent}% this month
            </span>
          </div>
        )}

        {/* Status Badge */}
        <div
          style={{
            display: 'inline-block',
            background: getStatusColor(status),
            padding: '8px 16px',
            borderRadius: '24px',
            fontSize: '13px',
            fontWeight: 600,
            letterSpacing: '0.3px',
          }}
          role="status"
          aria-label={getStatusLabel(status)}
        >
          {getStatusLabel(status)}
        </div>
      </div>
    </div>
  );
};

export default BalanceCard;
