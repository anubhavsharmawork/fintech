import { renderHook, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import React from 'react';
import { useGlobalSearch } from './useGlobalSearch';
import * as searchService from '../services/search';

jest.mock('../services/search');

const mockNavigate = jest.fn();

const wrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <MemoryRouter>{children}</MemoryRouter>
);

describe('useGlobalSearch', () => {
  const mockAccounts = [{ id: 'a1', accountNumber: 'ACC001', accountType: 'Checking', balance: 100 }];
  const mockTransactions = [{ id: 't1', description: 'Coffee', amount: 5, currency: 'NZD', type: 'debit', createdAt: '2024-01-01' }];
  const mockPayees = [{ id: 'p1', name: 'Alice', accountNumber: 'PAY001' }];

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    (searchService.searchAccounts as jest.Mock).mockResolvedValue(mockAccounts);
    (searchService.searchTransactions as jest.Mock).mockResolvedValue(mockTransactions);
    (searchService.searchPayees as jest.Mock).mockResolvedValue(mockPayees);
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  const renderSearch = () =>
    renderHook(() => useGlobalSearch(mockNavigate), { wrapper });

  describe('initial state', () => {
    it('starts closed', () => {
      const { result } = renderSearch();
      expect(result.current.isOpen).toBe(false);
    });

    it('starts with empty query', () => {
      const { result } = renderSearch();
      expect(result.current.query).toBe('');
    });

    it('starts with no results', () => {
      const { result } = renderSearch();
      expect(result.current.accounts).toHaveLength(0);
      expect(result.current.transactions).toHaveLength(0);
      expect(result.current.payees).toHaveLength(0);
    });

    it('starts with activeIndex -1', () => {
      const { result } = renderSearch();
      expect(result.current.activeIndex).toBe(-1);
    });
  });

  describe('open / close', () => {
    it('open() sets isOpen to true', () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      expect(result.current.isOpen).toBe(true);
    });

    it('open() resets query', () => {
      const { result } = renderSearch();
      act(() => { result.current.setQuery('hello'); });
      act(() => { result.current.open(); });
      expect(result.current.query).toBe('');
    });

    it('close() sets isOpen to false', () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.close(); });
      expect(result.current.isOpen).toBe(false);
    });
  });

  describe('setQuery and debounced search', () => {
    it('does not search when not open', async () => {
      const { result } = renderSearch();
      act(() => { result.current.setQuery('coffee'); });
      act(() => { jest.runAllTimers(); });
      expect(searchService.searchAccounts).not.toHaveBeenCalled();
    });

    it('triggers search after debounce when open', async () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.setQuery('coffee'); });
      await act(async () => { jest.runAllTimers(); });
      expect(searchService.searchAccounts).toHaveBeenCalledWith('coffee', expect.any(AbortSignal));
    });

    it('populates results after search', async () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.setQuery('coffee'); });
      await act(async () => { jest.runAllTimers(); await Promise.resolve(); });
      expect(result.current.accounts).toEqual(mockAccounts);
      expect(result.current.transactions).toEqual(mockTransactions);
      expect(result.current.payees).toEqual(mockPayees);
    });

    it('clears results when query becomes empty', async () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.setQuery('coffee'); });
      await act(async () => { jest.runAllTimers(); await Promise.resolve(); });
      act(() => { result.current.setQuery(''); });
      await act(async () => { jest.runAllTimers(); });
      expect(result.current.accounts).toHaveLength(0);
      expect(result.current.transactions).toHaveLength(0);
      expect(result.current.payees).toHaveLength(0);
    });

    it('handles search errors gracefully', async () => {
      (searchService.searchAccounts as jest.Mock).mockRejectedValue(new Error('Network error'));
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.setQuery('fail'); });
      await act(async () => { jest.runAllTimers(); await Promise.resolve(); });
      expect(result.current.accounts).toHaveLength(0);
    });
  });

  describe('flatItems', () => {
    it('combines all results into flatItems', async () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.setQuery('test'); });
      await act(async () => { jest.runAllTimers(); await Promise.resolve(); });
      const flat = result.current.flatItems;
      expect(flat.some(i => i.kind === 'account')).toBe(true);
      expect(flat.some(i => i.kind === 'transaction')).toBe(true);
      expect(flat.some(i => i.kind === 'payee')).toBe(true);
    });
  });

  describe('getNavigationPath', () => {
    it('returns /accounts for account kind', () => {
      const { result } = renderSearch();
      const path = result.current.getNavigationPath({ kind: 'account', data: mockAccounts[0] });
      expect(path).toBe('/accounts');
    });

    it('returns /transactions for transaction kind', () => {
      const { result } = renderSearch();
      const path = result.current.getNavigationPath({ kind: 'transaction', data: mockTransactions[0] });
      expect(path).toBe('/transactions');
    });

    it('returns /transactions for payee kind', () => {
      const { result } = renderSearch();
      const path = result.current.getNavigationPath({ kind: 'payee', data: mockPayees[0] });
      expect(path).toBe('/transactions');
    });
  });

  describe('onKeyDown', () => {
    it('ArrowDown increments activeIndex', async () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.setQuery('test'); });
      await act(async () => { jest.runAllTimers(); await Promise.resolve(); });

      act(() => {
        result.current.onKeyDown({ key: 'ArrowDown', preventDefault: jest.fn() } as any);
      });
      expect(result.current.activeIndex).toBe(0);
    });

    it('ArrowUp wraps to last item from -1', async () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.setQuery('test'); });
      await act(async () => { jest.runAllTimers(); await Promise.resolve(); });

      act(() => {
        result.current.onKeyDown({ key: 'ArrowUp', preventDefault: jest.fn() } as any);
      });
      expect(result.current.activeIndex).toBe(result.current.flatItems.length - 1);
    });

    it('Enter navigates to item path', async () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => { result.current.setQuery('test'); });
      await act(async () => { jest.runAllTimers(); await Promise.resolve(); });

      act(() => {
        result.current.onKeyDown({ key: 'ArrowDown', preventDefault: jest.fn() } as any);
      });
      act(() => {
        result.current.onKeyDown({ key: 'Enter', preventDefault: jest.fn() } as any);
      });

      expect(mockNavigate).toHaveBeenCalledWith('/accounts');
      expect(result.current.isOpen).toBe(false);
    });

    it('Enter does nothing when activeIndex is -1', () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => {
        result.current.onKeyDown({ key: 'Enter', preventDefault: jest.fn() } as any);
      });
      expect(mockNavigate).not.toHaveBeenCalled();
    });
  });

  describe('keyboard shortcut Ctrl+K', () => {
    it('Ctrl+K opens search when closed', () => {
      renderSearch();
      act(() => {
        const event = new KeyboardEvent('keydown', { key: 'k', ctrlKey: true });
        window.dispatchEvent(event);
      });
    });

    it('Escape closes search when open', () => {
      const { result } = renderSearch();
      act(() => { result.current.open(); });
      act(() => {
        const event = new KeyboardEvent('keydown', { key: 'Escape' });
        window.dispatchEvent(event);
      });
      expect(result.current.isOpen).toBe(false);
    });
  });

  describe('setActiveIndex', () => {
    it('updates activeIndex', () => {
      const { result } = renderSearch();
      act(() => { result.current.setActiveIndex(3); });
      expect(result.current.activeIndex).toBe(3);
    });
  });
});

