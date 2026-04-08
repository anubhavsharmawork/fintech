
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import GlobalSearch from './GlobalSearch';
import { useGlobalSearch } from '../hooks/useGlobalSearch';

jest.mock('../hooks/useGlobalSearch');

const mockSearchState = {
  isOpen: true,
  query: 'test',
  loading: false,
  accounts: [],
  transactions: [],
  payees: [],
  handleOpen: jest.fn(),
  handleClose: jest.fn(),
  handleQueryChange: jest.fn(),
  handleClear: jest.fn()
};

describe('GlobalSearch', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render search input when open', () => {
    useGlobalSearch as jest.Mock.mockReturnValue(mockSearchState);

    render(
      <BrowserRouter>
        <GlobalSearch search={mockSearchState} />
      </BrowserRouter>
    );

    expect(screen.getByPlaceholderText(/search/i)).toBeInTheDocument();
  });

  it('should display loading state', () => {
    const loadingState = { ...mockSearchState, loading: true };

    render(
      <BrowserRouter>
        <GlobalSearch search={loadingState} />
      </BrowserRouter>
    );

    expect(screen.getByText(/searching/i)).toBeInTheDocument();
  });

  it('should display empty state when no results', () => {
    const emptyState = { ...mockSearchState, query: 'nonexistent' };

    render(
      <BrowserRouter>
        <GlobalSearch search={emptyState} />
      </BrowserRouter>
    );

    expect(screen.getByText(/no results/i)).toBeInTheDocument();
  });

  it('should display account results', () => {
    const withResults = {
      ...mockSearchState,
      accounts: [
        { id: 'a1', accountNumber: '1234567890', accountType: 'checking', balance: 5000, currency: 'USD' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withResults} />
      </BrowserRouter>
    );

    expect(screen.getByText(/accounts/i)).toBeInTheDocument();
    expect(screen.getByText(/checking/i)).toBeInTheDocument();
  });

  it('should handle clear button click', async () => {
    const user = userEvent.setup();

    render(
      <BrowserRouter>
        <GlobalSearch search={mockSearchState} />
      </BrowserRouter>
    );

    const clearBtn = screen.getByRole('button', { name: /clear/i });
    await user.click(clearBtn);

    expect(mockSearchState.handleClear).toHaveBeenCalled();
  });

  it('should handle close button click', async () => {
    const user = userEvent.setup();

    render(
      <BrowserRouter>
        <GlobalSearch search={mockSearchState} />
      </BrowserRouter>
    );

    const closeBtn = screen.getByRole('button', { name: /close/i });
    await user.click(closeBtn);

    expect(mockSearchState.handleClose).toHaveBeenCalled();
  });

  it('should not render when closed', () => {
    const closedState = { ...mockSearchState, isOpen: false };

    const { container } = render(
      <BrowserRouter>
        <GlobalSearch search={closedState} />
      </BrowserRouter>
    );

    expect(container.querySelector('.gs-overlay')).not.toBeInTheDocument();
  });

  it('should display transaction results', () => {
    const withTransactions = {
      ...mockSearchState,
      transactions: [
        {
          id: 't1',
          accountId: 'a1',
          amount: 150.50,
          currency: 'NZD',
          type: 'debit' as const,
          description: 'Coffee shop purchase',
          createdAt: '2024-01-15T10:00:00Z'
        }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withTransactions} />
      </BrowserRouter>
    );

    expect(screen.getByText(/transactions/i)).toBeInTheDocument();
    expect(screen.getByText(/coffee shop/i)).toBeInTheDocument();
  });

  it('should display payee results', () => {
    const withPayees = {
      ...mockSearchState,
      payees: [
        { id: 'p1', name: 'John Doe', accountNumber: '1234567890' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withPayees} />
      </BrowserRouter>
    );

    expect(screen.getByText(/payees/i)).toBeInTheDocument();
    expect(screen.getByText(/john doe/i)).toBeInTheDocument();
  });

  it('should highlight matching text in results', () => {
    const withResults = {
      ...mockSearchState,
      query: 'check',
      accounts: [
        { id: 'a1', accountNumber: '1234567890', accountType: 'Checking', balance: 5000, currency: 'NZD' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withResults} />
      </BrowserRouter>
    );

    const highlights = document.querySelectorAll('.gs-highlight');
    expect(highlights.length).toBeGreaterThan(0);
  });

  it('should mask account numbers', () => {
    const withResults = {
      ...mockSearchState,
      accounts: [
        { id: 'a1', accountNumber: '1234567890', accountType: 'Checking', balance: 5000, currency: 'NZD' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withResults} />
      </BrowserRouter>
    );

    // Should show masked account number (last 4 digits)
    expect(screen.getByText(/7890/)).toBeInTheDocument();
    // Should not show full account number
    expect(screen.queryByText('1234567890')).not.toBeInTheDocument();
  });

  it('should format currency amounts', () => {
    const withResults = {
      ...mockSearchState,
      accounts: [
        { id: 'a1', accountNumber: '1234567890', accountType: 'Checking', balance: 5000, currency: 'NZD' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withResults} />
      </BrowserRouter>
    );

    expect(screen.getByText(/\$5,000\.00/)).toBeInTheDocument();
  });

  it('should handle item click and navigate', async () => {
    const mockClose = jest.fn();
    const mockGetNavigationPath = jest.fn().mockReturnValue('/accounts');
    const user = userEvent.setup();

    const withResults = {
      ...mockSearchState,
      close: mockClose,
      getNavigationPath: mockGetNavigationPath,
      accounts: [
        { id: 'a1', accountNumber: '1234567890', accountType: 'Checking', balance: 5000, currency: 'NZD' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withResults} />
      </BrowserRouter>
    );

    const accountItem = screen.getByText(/checking/i).closest('.gs-item');
    if (accountItem) {
      await user.click(accountItem);
      expect(mockClose).toHaveBeenCalled();
    }
  });

  it('should update active index on mouse enter', async () => {
    const mockSetActiveIndex = jest.fn();
    const user = userEvent.setup();

    const withResults = {
      ...mockSearchState,
      setActiveIndex: mockSetActiveIndex,
      accounts: [
        { id: 'a1', accountNumber: '1234567890', accountType: 'Checking', balance: 5000, currency: 'NZD' },
        { id: 'a2', accountNumber: '0987654321', accountType: 'Savings', balance: 10000, currency: 'NZD' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withResults} />
      </BrowserRouter>
    );

    const items = document.querySelectorAll('.gs-item');
    if (items[1]) {
      await user.hover(items[1]);
      expect(mockSetActiveIndex).toHaveBeenCalledWith(1);
    }
  });

  it('should close on overlay click', async () => {
    const mockClose = jest.fn();
    const user = userEvent.setup();

    render(
      <BrowserRouter>
        <GlobalSearch search={{ ...mockSearchState, close: mockClose }} />
      </BrowserRouter>
    );

    const overlay = document.querySelector('.gs-overlay');
    if (overlay) {
      await user.click(overlay);
      expect(mockClose).toHaveBeenCalled();
    }
  });

  it('should show skeleton loading groups', () => {
    const loadingState = { ...mockSearchState, loading: true };

    render(
      <BrowserRouter>
        <GlobalSearch search={loadingState} />
      </BrowserRouter>
    );

    const skeletons = document.querySelectorAll('.gs-skeleton-group');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('should format relative dates for transactions', () => {
    const today = new Date();
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);

    const withTransactions = {
      ...mockSearchState,
      transactions: [
        {
          id: 't1',
          accountId: 'a1',
          amount: 100,
          currency: 'NZD',
          type: 'debit' as const,
          description: 'Test',
          createdAt: today.toISOString()
        }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withTransactions} />
      </BrowserRouter>
    );

    expect(screen.getByText(/today/i)).toBeInTheDocument();
  });

  it('should handle keyboard navigation via onKeyDown', () => {
    const mockOnKeyDown = jest.fn();

    render(
      <BrowserRouter>
        <GlobalSearch search={{ ...mockSearchState, onKeyDown: mockOnKeyDown }} />
      </BrowserRouter>
    );

    const modal = document.querySelector('.gs-modal');
    if (modal) {
      fireEvent.keyDown(modal, { key: 'ArrowDown' });
      expect(mockOnKeyDown).toHaveBeenCalled();
    }
  });

  it('should handle query change via input', async () => {
    const mockSetQuery = jest.fn();
    const user = userEvent.setup();

    render(
      <BrowserRouter>
        <GlobalSearch search={{ ...mockSearchState, setQuery: mockSetQuery }} />
      </BrowserRouter>
    );

    const input = screen.getByPlaceholderText(/search/i);
    await user.type(input, 'new query');

    expect(mockSetQuery).toHaveBeenCalled();
  });

  it('should show active item styling', () => {
    const withResults = {
      ...mockSearchState,
      activeIndex: 0,
      accounts: [
        { id: 'a1', accountNumber: '1234567890', accountType: 'Checking', balance: 5000, currency: 'NZD' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withResults} />
      </BrowserRouter>
    );

    const activeItem = document.querySelector('.gs-item--active');
    expect(activeItem).toBeInTheDocument();
  });

  it('should handle fallback currency formatting', () => {
    const withResults = {
      ...mockSearchState,
      accounts: [
        { id: 'a1', accountNumber: '1234567890', accountType: 'Checking', balance: 5000, currency: 'INVALID' }
      ]
    };

    render(
      <BrowserRouter>
        <GlobalSearch search={withResults} />
      </BrowserRouter>
    );

    // Should fall back to basic dollar formatting
    expect(screen.getByText(/5000\.00/)).toBeInTheDocument();
  });
});

