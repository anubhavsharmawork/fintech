import React from 'react';
import { render, screen } from '@testing-library/react';
import BalanceCard from './BalanceCard';

describe('BalanceCard Component', () => {
  describe('Rendering', () => {
    it('should render with required props', () => {
      render(<BalanceCard total={1000} />);
      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should display total balance amount', () => {
      render(<BalanceCard total={1000} currency="NZD" />);
      expect(screen.getByText(/\$1,000/)).toBeInTheDocument();
    });

    it('should have aria-label for balance overview', () => {
      render(<BalanceCard total={1000} />);
      const region = screen.getByRole('region');
      expect(region).toHaveAttribute('aria-label', 'Total Balance Overview');
    });

    it('should display "Total Balance" label', () => {
      render(<BalanceCard total={1000} />);
      expect(screen.getByText('Total Balance')).toBeInTheDocument();
    });
  });

  describe('Currency Formatting', () => {
    it('should use NZD currency by default', () => {
      render(<BalanceCard total={1000} />);
      expect(screen.getByText(/\$1,000/)).toBeInTheDocument();
    });

    it('should format with 2 decimal places', () => {
      render(<BalanceCard total={1000.5} />);
      expect(screen.getByText(/\$1,000\.50/)).toBeInTheDocument();
    });

    it('should support different currency codes', () => {
      render(<BalanceCard total={1000} currency="USD" />);
      // The formatter will use the currency symbol for USD
      expect(screen.getByText(/\$1,000/)).toBeInTheDocument();
    });

    it('should handle large numbers', () => {
      render(<BalanceCard total={1000000} />);
      expect(screen.getByText(/\$1,000,000\.00/)).toBeInTheDocument();
    });

    it('should handle zero balance', () => {
      render(<BalanceCard total={0} />);
      expect(screen.getByText(/\$0\.00/)).toBeInTheDocument();
    });

    it('should handle negative balance', () => {
      render(<BalanceCard total={-500} />);
      expect(screen.getByText(/-\$500\.00/)).toBeInTheDocument();
    });

    it('should handle decimal balances', () => {
      render(<BalanceCard total={1234.56} />);
      expect(screen.getByText(/\$1,234\.56/)).toBeInTheDocument();
    });
  });

  describe('Status Display', () => {
    it('should display healthy status by default', () => {
      render(<BalanceCard total={1000} />);
      expect(screen.getByText('Healthy Balance')).toBeInTheDocument();
    });

    it('should display excellent status', () => {
      render(<BalanceCard total={1000} status="excellent" />);
      expect(screen.getByText('Excellent Balance')).toBeInTheDocument();
    });

    it('should display warning status', () => {
      render(<BalanceCard total={100} status="warning" />);
      expect(screen.getByText('Balance Warning')).toBeInTheDocument();
    });

    it('should display low status', () => {
      render(<BalanceCard total={10} status="low" />);
      expect(screen.getByText('Low Balance')).toBeInTheDocument();
    });

    it('should have role status on status badge', () => {
      render(<BalanceCard total={1000} />);
      const statusBadge = screen.getByRole('status');
      expect(statusBadge).toBeInTheDocument();
    });

    it('should have aria-label on status badge', () => {
      render(<BalanceCard total={1000} status="excellent" />);
      const statusBadge = screen.getByRole('status');
      expect(statusBadge).toHaveAttribute('aria-label', 'Excellent Balance');
    });
  });

  describe('Status Colors', () => {
    it('should apply green color for excellent status', () => {
      render(<BalanceCard total={1000} status="excellent" />);
      const statusBadge = screen.getByRole('status');
      expect(statusBadge).toHaveStyle({ background: '#059669' });
    });

    it('should apply cyan color for healthy status', () => {
      render(<BalanceCard total={1000} status="healthy" />);
      const statusBadge = screen.getByRole('status');
      expect(statusBadge).toHaveStyle({ background: '#0891b2' });
    });

    it('should apply yellow color for warning status', () => {
      render(<BalanceCard total={1000} status="warning" />);
      const statusBadge = screen.getByRole('status');
      expect(statusBadge).toHaveStyle({ background: '#f59e0b' });
    });

    it('should apply red color for low status', () => {
      render(<BalanceCard total={1000} status="low" />);
      const statusBadge = screen.getByRole('status');
      expect(statusBadge).toHaveStyle({ background: '#dc2626' });
    });
  });

  describe('Trend Display', () => {
    it('should not show trend by default', () => {
      render(<BalanceCard total={1000} />);
      expect(screen.queryByText(/this month/)).not.toBeInTheDocument();
    });

    it('should show trend when showTrend is true', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={5} />);
      expect(screen.getByText(/5% this month/)).toBeInTheDocument();
    });

    it('should show positive trend with up emoji', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={5} />);
      expect(screen.getByText(/📈/)).toBeInTheDocument();
    });

    it('should show negative trend with down emoji', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={-5} />);
      expect(screen.getByText(/📉/)).toBeInTheDocument();
    });

    it('should format positive trend with plus sign', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={10} />);
      expect(screen.getByText(/\+10%/)).toBeInTheDocument();
    });

    it('should format negative trend without additional sign', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={-10} />);
      expect(screen.getByText(/-10%/)).toBeInTheDocument();
    });

    it('should not show trend when trendPercent is 0', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={0} />);
      expect(screen.queryByText(/this month/)).not.toBeInTheDocument();
    });

    it('should show trend with decimal percent', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={2.5} />);
      expect(screen.getByText(/\+2.5% this month/)).toBeInTheDocument();
    });

    it('should handle large trend values', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={100} />);
      expect(screen.getByText(/\+100% this month/)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have main balance with heading role', () => {
      render(<BalanceCard total={1000} />);
      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toBeInTheDocument();
    });

    it('should have aria-label on main balance amount', () => {
      render(<BalanceCard total={1000} currency="NZD" />);
      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveAttribute('aria-label', expect.stringContaining('Total balance'));
    });

    it('should hide decorative element from accessibility tree', () => {
      const { container } = render(<BalanceCard total={1000} />);
      const decorative = container.querySelector('[aria-hidden="true"]');
      expect(decorative).toBeInTheDocument();
    });

    it('should have proper semantic structure', () => {
      render(<BalanceCard total={1000} />);
      expect(screen.getByRole('region')).toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument();
      expect(screen.getByRole('status')).toBeInTheDocument();
    });
  });

  describe('Style Properties', () => {
    it('should have gradient background', () => {
      render(<BalanceCard total={1000} />);
      const card = screen.getByRole('region');
      expect(card).toHaveStyle({
        background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
      });
    });

    it('should have white text color', () => {
      render(<BalanceCard total={1000} />);
      const card = screen.getByRole('region');
      expect(card).toHaveStyle({ color: '#fff' });
    });

    it('should have padding', () => {
      render(<BalanceCard total={1000} />);
      const card = screen.getByRole('region');
      expect(card).toHaveStyle({
        padding: '32px 28px',
      });
    });

    it('should have border radius', () => {
      render(<BalanceCard total={1000} />);
      const card = screen.getByRole('region');
      expect(card).toHaveStyle({
        borderRadius: '12px',
      });
    });

    it('should have box shadow', () => {
      render(<BalanceCard total={1000} />);
      const card = screen.getByRole('region');
      expect(card).toHaveStyle({
        boxShadow: '0 10px 30px rgba(102, 126, 234, 0.3)',
      });
    });
  });

  describe('Props Combinations', () => {
    it('should handle all props together', () => {
      render(
        <BalanceCard
          total={5000.75}
          currency="NZD"
          status="excellent"
          showTrend={true}
          trendPercent={15.5}
        />
      );

      expect(screen.getByText(/\$5,000\.75/)).toBeInTheDocument();
      expect(screen.getByText('Excellent Balance')).toBeInTheDocument();
      expect(screen.getByText(/\+15.5% this month/)).toBeInTheDocument();
    });

    it('should handle minimal props', () => {
      const { container } = render(<BalanceCard total={100} />);
      expect(container.querySelector('[role="region"]')).toBeInTheDocument();
    });

    it('should update when total prop changes', () => {
      const { rerender } = render(<BalanceCard total={1000} />);
      expect(screen.getByText(/\$1,000\.00/)).toBeInTheDocument();

      rerender(<BalanceCard total={2000} />);
      expect(screen.getByText(/\$2,000\.00/)).toBeInTheDocument();
    });

    it('should update when status prop changes', () => {
      const { rerender } = render(<BalanceCard total={1000} status="healthy" />);
      expect(screen.getByText('Healthy Balance')).toBeInTheDocument();

      rerender(<BalanceCard total={1000} status="low" />);
      expect(screen.getByText('Low Balance')).toBeInTheDocument();
    });
  });
});
