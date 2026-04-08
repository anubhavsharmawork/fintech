// @ts-nocheck
import * as React from 'react';

interface TableSkeletonProps {
  rows?: number;
  columns?: number;
}

/**
 * Skeleton loading placeholder for table rows.
 * Maintains visual stability during page loads.
 */
const TableSkeleton: React.FC<TableSkeletonProps> = ({ rows = 10, columns = 6 }) => {
  return (
    <div className="skeleton-table">
      {Array.from({ length: rows }).map((_, rowIdx) => (
        <div key={rowIdx} className="skeleton-row">
          {Array.from({ length: columns }).map((_, colIdx) => (
            <div
              key={colIdx}
              className="skeleton-cell"
              style={{ width: colIdx === 0 ? '15%' : colIdx === columns - 1 ? '12%' : undefined }}
            />
          ))}
        </div>
      ))}
    </div>
  );
};

export default TableSkeleton;
