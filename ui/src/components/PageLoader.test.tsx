import React from 'react';
import { render, screen } from '@testing-library/react';
import PageLoader from './PageLoader';

describe('PageLoader Component', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      render(<PageLoader />);

      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('should have status role for accessibility', () => {
      render(<PageLoader />);

      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('should have aria-label for screen readers', () => {
      render(<PageLoader />);

      expect(screen.getByRole('status')).toHaveAttribute('aria-label', 'Loading content');
    });

    it('should have aria-busy attribute', () => {
      render(<PageLoader />);

      expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
    });

    it('should display Loading text for screen readers', () => {
      render(<PageLoader />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('should have sr-only class on loading text', () => {
      render(<PageLoader />);

      expect(screen.getByText('Loading...')).toHaveClass('sr-only');
    });
  });

  describe('Skeleton Cards', () => {
    it('should render 3 skeleton cards', () => {
      const { container } = render(<PageLoader />);

      const skeletonCards = container.querySelectorAll('.skeleton-card');
      expect(skeletonCards).toHaveLength(3);
    });

    it('should render skeleton rows inside each card', () => {
      const { container } = render(<PageLoader />);

      const skeletonRows = container.querySelectorAll('.skeleton-row');
      expect(skeletonRows.length).toBeGreaterThan(0);
    });

    it('should render skeleton cells inside rows', () => {
      const { container } = render(<PageLoader />);

      const skeletonCells = container.querySelectorAll('.skeleton-cell');
      expect(skeletonCells.length).toBeGreaterThan(0);
    });
  });

  describe('Layout', () => {
    it('should have first row with 3 cells', () => {
      const { container } = render(<PageLoader />);

      const cards = container.querySelectorAll('.skeleton-card');
      const firstCardRows = cards[0].querySelectorAll('.skeleton-row');
      const firstRowCells = firstCardRows[0].querySelectorAll('.skeleton-cell');

      expect(firstRowCells).toHaveLength(3);
    });

    it('should have second row with 2 cells', () => {
      const { container } = render(<PageLoader />);

      const cards = container.querySelectorAll('.skeleton-card');
      const firstCardRows = cards[0].querySelectorAll('.skeleton-row');
      const secondRowCells = firstCardRows[1].querySelectorAll('.skeleton-cell');

      expect(secondRowCells).toHaveLength(2);
    });
  });
});
