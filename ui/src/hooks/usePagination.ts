// @ts-nocheck
import * as React from 'react';
import { useInRouterContext, useSearchParams } from 'react-router-dom';

export interface PaginationState {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  from: number;
  to: number;
  setPage: (page: number) => void;
  setPageSize: (size: number) => void;
  setTotalCount: (count: number) => void;
  resetToFirstPage: () => void;
}

export interface UsePaginationOptions {
  defaultPageSize?: number;
  syncToUrl?: boolean;
}

/**
 * Pagination hook for managing page state with optional URL sync.
 * Resets to page 1 when filters change (call resetToFirstPage).
 */
export function usePagination(options: UsePaginationOptions = {}): PaginationState {
  const { defaultPageSize = 25, syncToUrl = true } = options;
  const inRouter = useInRouterContext();
  const [searchParams, setSearchParams] = useSearchParams();

  // Initialize from URL or defaults
  const getInitialPage = (): number => {
    if (syncToUrl) {
      const urlPage = parseInt(searchParams.get('page') || '', 10);
      if (!isNaN(urlPage) && urlPage >= 1) return urlPage;
    }
    return 1;
  };

  const getInitialPageSize = (): number => {
    if (syncToUrl) {
      const urlSize = parseInt(searchParams.get('pageSize') || '', 10);
      if ([10, 25, 50, 100].includes(urlSize)) return urlSize;
    }
    return defaultPageSize;
  };

  const [page, setPageInternal] = React.useState(getInitialPage);
  const [pageSize, setPageSizeInternal] = React.useState(getInitialPageSize);
  const [totalCount, setTotalCount] = React.useState(0);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);

  // Sync to URL
  const updateUrl = React.useCallback((newPage: number, newSize: number) => {
    if (!syncToUrl || !inRouter) return;
    setSearchParams(prev => {
      const params = new URLSearchParams(prev);
      params.set('page', String(newPage));
      params.set('pageSize', String(newSize));
      return params;
    }, { replace: true });
  }, [syncToUrl, inRouter, setSearchParams]);

  const setPage = React.useCallback((newPage: number) => {
    const clampedPage = Math.max(1, Math.min(newPage, totalPages || 1));
    setPageInternal(clampedPage);
    updateUrl(clampedPage, pageSize);
  }, [totalPages, pageSize, updateUrl]);

  const setPageSize = React.useCallback((newSize: number) => {
    if (![10, 25, 50, 100].includes(newSize)) return;
    setPageSizeInternal(newSize);
    setPageInternal(1); // Reset to first page on page size change
    updateUrl(1, newSize);
  }, [updateUrl]);

  const resetToFirstPage = React.useCallback(() => {
    setPageInternal(1);
    updateUrl(1, pageSize);
  }, [pageSize, updateUrl]);

  // Clamp page if totalPages decreases
  React.useEffect(() => {
    if (page > totalPages && totalPages > 0) {
      setPage(totalPages);
    }
  }, [page, totalPages, setPage]);

  return {
    page,
    pageSize,
    totalCount,
    totalPages,
    from,
    to,
    setPage,
    setPageSize,
    setTotalCount,
    resetToFirstPage,
  };
}
