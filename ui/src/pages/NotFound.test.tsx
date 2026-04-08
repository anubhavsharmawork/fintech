import React from 'react';
import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import NotFound from './NotFound';

describe('NotFound Page', () => {
  const renderNotFound = () => {
    return render(
      <BrowserRouter>
        <NotFound />
      </BrowserRouter>
    );
  };

  describe('Rendering', () => {
    it('should render 404 code', () => {
      renderNotFound();

      expect(screen.getByText('404')).toBeInTheDocument();
    });

    it('should render Page Not Found heading', () => {
      renderNotFound();

      expect(screen.getByText('Page Not Found')).toBeInTheDocument();
    });

    it('should render description message', () => {
      renderNotFound();

      expect(screen.getByText('The page you are looking for does not exist or has been moved.')).toBeInTheDocument();
    });

    it('should render link to dashboard', () => {
      renderNotFound();

      expect(screen.getByRole('link', { name: 'Go to Dashboard' })).toBeInTheDocument();
    });
  });

  describe('Navigation Link', () => {
    it('should have correct href to home', () => {
      renderNotFound();

      expect(screen.getByRole('link', { name: 'Go to Dashboard' })).toHaveAttribute('href', '/');
    });

    it('should have btn classes', () => {
      renderNotFound();

      expect(screen.getByRole('link', { name: 'Go to Dashboard' })).toHaveClass('btn', 'btn-primary');
    });
  });

  describe('Styling', () => {
    it('should have centered text alignment', () => {
      const { container } = renderNotFound();

      const wrapper = container.firstChild as HTMLElement;
      expect(wrapper).toHaveStyle({ textAlign: 'center' });
    });

    it('should display 404 in large font', () => {
      renderNotFound();

      const code = screen.getByText('404');
      expect(code).toHaveStyle({ fontSize: '4rem' });
    });
  });
});
