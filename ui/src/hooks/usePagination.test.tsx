import { renderHook, act } from '@testing-library/react';
import React from 'react';
import { MemoryRouter } from 'react-router-dom';
import { usePagination } from './usePagination';

const wrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <MemoryRouter>{children}</MemoryRouter>
);

describe('usePagination', () => {
  describe('defaults', () => {
    it('starts on page 1', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      expect(result.current.page).toBe(1);
    });

    it('uses defaultPageSize of 25', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      expect(result.current.pageSize).toBe(25);
    });

    it('accepts custom defaultPageSize', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 10 }), { wrapper });
      expect(result.current.pageSize).toBe(10);
    });

    it('totalCount starts at 0', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      expect(result.current.totalCount).toBe(0);
    });

    it('totalPages is 1 when totalCount is 0', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      expect(result.current.totalPages).toBe(1);
    });

    it('from is 0 when totalCount is 0', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      expect(result.current.from).toBe(0);
    });

    it('to is 0 when totalCount is 0', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      expect(result.current.to).toBe(0);
    });
  });

  describe('setTotalCount', () => {
    it('updates totalCount', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      act(() => { result.current.setTotalCount(100); });
      expect(result.current.totalCount).toBe(100);
    });

    it('calculates totalPages correctly', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 25 }), { wrapper });
      act(() => { result.current.setTotalCount(100); });
      expect(result.current.totalPages).toBe(4);
    });

    it('rounds up totalPages for partial last page', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 25 }), { wrapper });
      act(() => { result.current.setTotalCount(26); });
      expect(result.current.totalPages).toBe(2);
    });

    it('calculates from and to on page 1', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 10 }), { wrapper });
      act(() => { result.current.setTotalCount(50); });
      expect(result.current.from).toBe(1);
      expect(result.current.to).toBe(10);
    });

    it('calculates from and to on last page', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 10 }), { wrapper });
      act(() => { result.current.setTotalCount(25); });
      act(() => { result.current.setPage(3); });
      expect(result.current.from).toBe(21);
      expect(result.current.to).toBe(25);
    });
  });

  describe('setPage', () => {
    it('changes the current page', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      act(() => { result.current.setTotalCount(100); });
      act(() => { result.current.setPage(3); });
      expect(result.current.page).toBe(3);
    });

    it('clamps page to 1 when below minimum', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      act(() => { result.current.setTotalCount(100); });
      act(() => { result.current.setPage(0); });
      expect(result.current.page).toBe(1);
    });

    it('clamps page to totalPages when above maximum', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 25 }), { wrapper });
      act(() => { result.current.setTotalCount(50); });
      act(() => { result.current.setPage(10); });
      expect(result.current.page).toBe(2);
    });
  });

  describe('setPageSize', () => {
    it('changes the page size', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      act(() => { result.current.setPageSize(10); });
      expect(result.current.pageSize).toBe(10);
    });

    it('resets to page 1 when page size changes', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 10 }), { wrapper });
      act(() => { result.current.setTotalCount(100); });
      act(() => { result.current.setPage(5); });
      act(() => { result.current.setPageSize(25); });
      expect(result.current.page).toBe(1);
    });

    it('ignores invalid page sizes', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 25 }), { wrapper });
      act(() => { result.current.setPageSize(7 as any); });
      expect(result.current.pageSize).toBe(25);
    });

    it('accepts 10, 25, 50, 100', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      for (const size of [10, 25, 50, 100]) {
        act(() => { result.current.setPageSize(size); });
        expect(result.current.pageSize).toBe(size);
      }
    });
  });

  describe('resetToFirstPage', () => {
    it('resets page to 1', () => {
      const { result } = renderHook(() => usePagination(), { wrapper });
      act(() => { result.current.setTotalCount(100); });
      act(() => { result.current.setPage(4); });
      act(() => { result.current.resetToFirstPage(); });
      expect(result.current.page).toBe(1);
    });
  });

  describe('auto-clamp when totalPages decreases', () => {
    it('clamps page when totalCount drops below current page', () => {
      const { result } = renderHook(() => usePagination({ defaultPageSize: 10 }), { wrapper });
      act(() => { result.current.setTotalCount(100); });
      act(() => { result.current.setPage(10); });
      expect(result.current.page).toBe(10);
      act(() => { result.current.setTotalCount(15); });
      expect(result.current.page).toBeLessThanOrEqual(2);
    });
  });
});

