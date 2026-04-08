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
      const { container } = render(<BalanceCard total={1000} status="excellent" />);
      const dot = container.querySelector('[aria-hidden="true"]');
      expect(dot).toHaveStyle({ backgroundColor: '#34d399' });
    });

    it('should apply cyan color for healthy status', () => {
      const { container } = render(<BalanceCard total={1000} status="healthy" />);
      const dot = container.querySelector('[aria-hidden="true"]');
      expect(dot).toHaveStyle({ backgroundColor: '#22d3ee' });
    });

    it('should apply yellow color for warning status', () => {
      const { container } = render(<BalanceCard total={1000} status="warning" />);
      const dot = container.querySelector('[aria-hidden="true"]');
      expect(dot).toHaveStyle({ backgroundColor: '#fbbf24' });
    });

    it('should apply red color for low status', () => {
      const { container } = render(<BalanceCard total={1000} status="low" />);
      const dot = container.querySelector('[aria-hidden="true"]');
      expect(dot).toHaveStyle({ backgroundColor: '#f87171' });
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
      expect(screen.getByText(/▲/)).toBeInTheDocument();
    });

    it('should show negative trend with down emoji', () => {
      render(<BalanceCard total={1000} showTrend={true} trendPercent={-5} />);
      expect(screen.getByText(/▼/)).toBeInTheDocument();
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
    it('should use card design system class', () => {
      render(<BalanceCard total={1000} />);
      const card = screen.getByRole('region');
      expect(card).toHaveClass('card');
    });

    it('should have dense KPI padding', () => {
      render(<BalanceCard total={1000} />);
      const card = screen.getByRole('region');
      expect(card).toHaveStyle({ padding: '16px 20px' });
    });

    it('should use tabular-nums for balance amount', () => {
      render(<BalanceCard total={1000} />);
      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveStyle({ fontVariantNumeric: 'tabular-nums' });
    });

    it('should have compact balance font size', () => {
      render(<BalanceCard total={1000} />);
      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveStyle({ fontSize: 'var(--font-size-2xl)' });
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

  describe('Available and Held Balance', () => {
    it('should display available balance when provided', () => {
      render(<BalanceCard total={1000} availableBalance={800} />);
      expect(screen.getByText('Available')).toBeInTheDocument();
      expect(screen.getByText(/\$800\.00/)).toBeInTheDocument();
    });

    it('should display held balance when provided', () => {
      render(<BalanceCard total={1000} heldBalance={200} />);
      expect(screen.getByText('Held')).toBeInTheDocument();
      expect(screen.getByText(/\$200\.00/)).toBeInTheDocument();
    });

    it('should display both available and held balance', () => {
      render(<BalanceCard total={1000} availableBalance={800} heldBalance={200} />);
      expect(screen.getByText('Available')).toBeInTheDocument();
      expect(screen.getByText('Held')).toBeInTheDocument();
    });

    it('should not display available/held section when neither is provided', () => {
      render(<BalanceCard total={1000} />);
      expect(screen.queryByText('Available')).not.toBeInTheDocument();
      expect(screen.queryByText('Held')).not.toBeInTheDocument();
    });

    it('should display only available when held is omitted', () => {
      render(<BalanceCard total={1000} availableBalance={950} />);
      expect(screen.getByText('Available')).toBeInTheDocument();
      expect(screen.queryByText('Held')).not.toBeInTheDocument();
    });

    it('should display only held when available is omitted', () => {
      render(<BalanceCard total={1000} heldBalance={50} />);
      expect(screen.queryByText('Available')).not.toBeInTheDocument();
      expect(screen.getByText('Held')).toBeInTheDocument();
    });

    it('should format available balance with currency', () => {
      render(<BalanceCard total={1000} availableBalance={800.5} currency="NZD" />);
      expect(screen.getByText(/\$800\.50/)).toBeInTheDocument();
    });

    it('should format held balance with currency', () => {
      render(<BalanceCard total={1000} heldBalance={199.99} />);
      expect(screen.getByText(/\$199\.99/)).toBeInTheDocument();
    });
  });

  describe('Sparkline', () => {
    it('should not render sparkline when sparklineData is not provided', () => {
      const { container } = render(<BalanceCard total={1000} />);
      expect(container.querySelector('.recharts-responsive-container')).not.toBeInTheDocument();
    });

    it('should not render sparkline when sparklineData has one point', () => {
      const { container } = render(<BalanceCard total={1000} sparklineData={[100]} />);
      expect(container.querySelector('.recharts-responsive-container')).not.toBeInTheDocument();
    });

    it('should render sparkline container when sparklineData has multiple points', () => {
      const { container } = render(<BalanceCard total={1000} sparklineData={[100, 150, 200, 175]} />);
      const sparkWrapper = container.querySelector('[aria-hidden="true"]');
      expect(sparkWrapper).toBeInTheDocument();
    });

    it('should render sparkline with aria-hidden to hide from screen readers', () => {
      const { container } = render(<BalanceCard total={1000} sparklineData={[100, 200, 300]} />);
      const hiddenEl = container.querySelector('[aria-hidden="true"]');
      expect(hiddenEl).toBeInTheDocument();
    });
  });
});
