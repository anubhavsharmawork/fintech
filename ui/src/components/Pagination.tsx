// @ts-nocheck
import * as React from 'react';
import { PaginationState } from '../hooks/usePagination';

interface PaginationProps {
  pagination: PaginationState;
}

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

/**
 * Institutional-grade pagination component with:
 * - Previous/Next buttons
 * - Numbered page buttons with ellipsis for many pages
 * - Results-per-page selector
 * - Summary showing "Showing X–Y of Z results"
 * - Full keyboard accessibility
 */
const Pagination: React.FC<PaginationProps> = ({ pagination }) => {
  const {
    page,
    pageSize,
    totalCount,
    totalPages,
    from,
    to,
    setPage,
    setPageSize,
  } = pagination;

  // Don't render if no data
  if (totalCount === 0) return null;

  const isFirstPage = page === 1;
  const isLastPage = page === totalPages;

  // Generate page numbers with ellipsis
  const getPageNumbers = (): (number | 'ellipsis')[] => {
    const pages: (number | 'ellipsis')[] = [];
    const delta = 1; // Pages to show on each side of current

    if (totalPages <= 7) {
      // Show all pages
      for (let i = 1; i <= totalPages; i++) pages.push(i);
    } else {
      // Always show first page
      pages.push(1);

      if (page > 3) {
        pages.push('ellipsis');
      }

      // Pages around current
      const rangeStart = Math.max(2, page - delta);
      const rangeEnd = Math.min(totalPages - 1, page + delta);

      for (let i = rangeStart; i <= rangeEnd; i++) {
        pages.push(i);
      }

      if (page < totalPages - 2) {
        pages.push('ellipsis');
      }

      // Always show last page
      if (totalPages > 1) {
        pages.push(totalPages);
      }
    }

    return pages;
  };

  const pageNumbers = getPageNumbers();

  const handleKeyDown = (e: React.KeyboardEvent, action: () => void) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      action();
    }
  };

  return (
    <div className="pg-container" role="navigation" aria-label="Pagination">
      {/* Results summary */}
      <div className="pg-summary">
        Showing <span className="pg-summary-range">{from.toLocaleString()}–{to.toLocaleString()}</span> of{' '}
        <span className="pg-summary-total">{totalCount.toLocaleString()}</span> results
      </div>

      {/* Page controls */}
      <div className="pg-controls">
        {/* Previous button */}
        <button
          type="button"
          className="pg-btn pg-btn--nav"
          onClick={() => setPage(page - 1)}
          disabled={isFirstPage}
          aria-label="Previous page"
          onKeyDown={(e) => !isFirstPage && handleKeyDown(e, () => setPage(page - 1))}
        >
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true">
            <path d="M9 3L5 7l4 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          <span className="pg-btn-text">Previous</span>
        </button>

        {/* Page number buttons */}
        <div className="pg-pages" role="group" aria-label="Page numbers">
          {pageNumbers.map((p, idx) =>
            p === 'ellipsis' ? (
              <span key={`ellipsis-${idx}`} className="pg-ellipsis" aria-hidden="true">…</span>
            ) : (
              <button
                key={p}
                type="button"
                className={`pg-btn pg-btn--page${p === page ? ' pg-btn--active' : ''}`}
                onClick={() => setPage(p)}
                aria-current={p === page ? 'page' : undefined}
                aria-label={`Page ${p}`}
                onKeyDown={(e) => handleKeyDown(e, () => setPage(p))}
              >
                {p}
              </button>
            )
          )}
        </div>

        {/* Next button */}
        <button
          type="button"
          className="pg-btn pg-btn--nav"
          onClick={() => setPage(page + 1)}
          disabled={isLastPage}
          aria-label="Next page"
          onKeyDown={(e) => !isLastPage && handleKeyDown(e, () => setPage(page + 1))}
        >
          <span className="pg-btn-text">Next</span>
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true">
            <path d="M5 3l4 4-4 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
        </button>
      </div>

      {/* Page size selector */}
      <div className="pg-size">
        <label htmlFor="pg-size-select" className="pg-size-label">Rows per page:</label>
        <select
          id="pg-size-select"
          className="pg-size-select"
          value={pageSize}
          onChange={(e) => setPageSize(parseInt(e.target.value, 10))}
          aria-label="Select number of rows per page"
        >
          {PAGE_SIZE_OPTIONS.map((size) => (
            <option key={size} value={size}>{size}</option>
          ))}
        </select>
      </div>
    </div>
  );
};

export default Pagination;
