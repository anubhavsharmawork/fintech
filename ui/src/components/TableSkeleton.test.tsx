import React from 'react';
import { render } from '@testing-library/react';
import TableSkeleton from './TableSkeleton';

describe('TableSkeleton Component', () => {
  describe('Default Props', () => {
    it('should render with default 10 rows', () => {
      const { container } = render(<TableSkeleton />);

      const rows = container.querySelectorAll('.skeleton-row');
      expect(rows).toHaveLength(10);
    });

    it('should render with default 6 columns per row', () => {
      const { container } = render(<TableSkeleton />);

      const firstRow = container.querySelector('.skeleton-row');
      const cells = firstRow?.querySelectorAll('.skeleton-cell');
      expect(cells).toHaveLength(6);
    });
  });

  describe('Custom Props', () => {
    it('should render specified number of rows', () => {
      const { container } = render(<TableSkeleton rows={5} />);

      const rows = container.querySelectorAll('.skeleton-row');
      expect(rows).toHaveLength(5);
    });

    it('should render specified number of columns', () => {
      const { container } = render(<TableSkeleton columns={4} />);

      const firstRow = container.querySelector('.skeleton-row');
      const cells = firstRow?.querySelectorAll('.skeleton-cell');
      expect(cells).toHaveLength(4);
    });

    it('should render with both custom rows and columns', () => {
      const { container } = render(<TableSkeleton rows={3} columns={8} />);

      const rows = container.querySelectorAll('.skeleton-row');
      expect(rows).toHaveLength(3);

      const firstRow = container.querySelector('.skeleton-row');
      const cells = firstRow?.querySelectorAll('.skeleton-cell');
      expect(cells).toHaveLength(8);
    });
  });

  describe('Styling', () => {
    it('should have skeleton-table class on container', () => {
      const { container } = render(<TableSkeleton />);

      expect(container.querySelector('.skeleton-table')).toBeInTheDocument();
    });

    it('should have skeleton-row class on each row', () => {
      const { container } = render(<TableSkeleton rows={3} />);

      const rows = container.querySelectorAll('.skeleton-row');
      rows.forEach(row => {
        expect(row).toHaveClass('skeleton-row');
      });
    });

    it('should have skeleton-cell class on each cell', () => {
      const { container } = render(<TableSkeleton />);

      const cells = container.querySelectorAll('.skeleton-cell');
      cells.forEach(cell => {
        expect(cell).toHaveClass('skeleton-cell');
      });
    });

    it('should set first column width to 15%', () => {
      const { container } = render(<TableSkeleton />);

      const firstRow = container.querySelector('.skeleton-row');
      const cells = firstRow?.querySelectorAll('.skeleton-cell');
      expect(cells?.[0]).toHaveStyle({ width: '15%' });
    });

    it('should set last column width to 12%', () => {
      const { container } = render(<TableSkeleton columns={6} />);

      const firstRow = container.querySelector('.skeleton-row');
      const cells = firstRow?.querySelectorAll('.skeleton-cell');
      expect(cells?.[5]).toHaveStyle({ width: '12%' });
    });
  });

  describe('Edge Cases', () => {
    it('should render single row', () => {
      const { container } = render(<TableSkeleton rows={1} />);

      const rows = container.querySelectorAll('.skeleton-row');
      expect(rows).toHaveLength(1);
    });

    it('should render single column', () => {
      const { container } = render(<TableSkeleton columns={1} />);

      const firstRow = container.querySelector('.skeleton-row');
      const cells = firstRow?.querySelectorAll('.skeleton-cell');
      expect(cells).toHaveLength(1);
    });

    it('should render many rows', () => {
      const { container } = render(<TableSkeleton rows={50} />);

      const rows = container.querySelectorAll('.skeleton-row');
      expect(rows).toHaveLength(50);
    });
  });
});
