import React from 'react';

/**
 * Full-width skeleton loader that renders 3 .skeleton-card rows
 * using the existing shimmer classes from index.css.
 */
const PageLoader: React.FC = () => {
  return (
    <div role="status" aria-label="Loading content" aria-busy="true">
      {[1, 2, 3].map((i) => (
        <div key={i} className="skeleton-card">
          <div className="skeleton-row">
            <div className="skeleton-cell" style={{ flex: 2 }} />
            <div className="skeleton-cell" style={{ flex: 1 }} />
            <div className="skeleton-cell" style={{ flex: 1 }} />
          </div>
          <div className="skeleton-row">
            <div className="skeleton-cell" style={{ flex: 3 }} />
            <div className="skeleton-cell" style={{ flex: 1 }} />
          </div>
        </div>
      ))}
      <span className="sr-only">Loading...</span>
    </div>
  );
};

export default PageLoader;
