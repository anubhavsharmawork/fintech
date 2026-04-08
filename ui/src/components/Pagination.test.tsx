import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import Pagination from './Pagination';

const makePagination = (overrides = {}) => ({
  page: 1,
  pageSize: 10,
  totalCount: 50,
  totalPages: 5,
  from: 1,
  to: 10,
  setPage: jest.fn(),
  setPageSize: jest.fn(),
  setTotalCount: jest.fn(),
  resetToFirstPage: jest.fn(),
  ...overrides,
});

const renderPagination = (props = {}) =>
  render(
    <MemoryRouter>
      <Pagination pagination={makePagination(props)} />
    </MemoryRouter>,
  );

describe('Pagination', () => {
  describe('rendering', () => {
    it('renders nothing when totalCount is 0', () => {
      const { container } = render(
        <MemoryRouter>
          <Pagination pagination={makePagination({ totalCount: 0, from: 0, to: 0, totalPages: 1 })} />
        </MemoryRouter>,
      );
      expect(container.firstChild).toBeNull();
    });

    it('renders navigation landmark', () => {
      renderPagination();
      expect(screen.getByRole('navigation', { name: 'Pagination' })).toBeInTheDocument();
    });

    it('shows results summary', () => {
      renderPagination();
      expect(screen.getByText(/1/)).toBeInTheDocument();
      expect(screen.getByText(/50/)).toBeInTheDocument();
    });

    it('renders previous button', () => {
      renderPagination();
      expect(screen.getByLabelText('Previous page')).toBeInTheDocument();
    });

    it('renders next button', () => {
      renderPagination();
      expect(screen.getByLabelText('Next page')).toBeInTheDocument();
    });

    it('disables previous button on first page', () => {
      renderPagination({ page: 1 });
      expect(screen.getByLabelText('Previous page')).toBeDisabled();
    });

    it('disables next button on last page', () => {
      renderPagination({ page: 5, totalPages: 5, from: 41, to: 50 });
      expect(screen.getByLabelText('Next page')).toBeDisabled();
    });

    it('renders page size selector', () => {
      renderPagination();
      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('renders page numbers for small page count', () => {
      renderPagination({ totalPages: 5 });
      for (let i = 1; i <= 5; i++) {
        expect(screen.getByLabelText(`Page ${i}`)).toBeInTheDocument();
      }
    });
  });

  describe('navigation', () => {
    it('calls setPage with page+1 when next is clicked', () => {
      const setPage = jest.fn();
      render(
        <MemoryRouter>
          <Pagination pagination={makePagination({ page: 2, setPage })} />
        </MemoryRouter>,
      );
      fireEvent.click(screen.getByLabelText('Next page'));
      expect(setPage).toHaveBeenCalledWith(3);
    });

    it('calls setPage with page-1 when previous is clicked', () => {
      const setPage = jest.fn();
      render(
        <MemoryRouter>
          <Pagination pagination={makePagination({ page: 3, setPage })} />
        </MemoryRouter>,
      );
      fireEvent.click(screen.getByLabelText('Previous page'));
      expect(setPage).toHaveBeenCalledWith(2);
    });

    it('calls setPage when a page number is clicked', () => {
      const setPage = jest.fn();
      render(
        <MemoryRouter>
          <Pagination pagination={makePagination({ page: 1, totalPages: 3, setPage })} />
        </MemoryRouter>,
      );
      fireEvent.click(screen.getByLabelText('Page 3'));
      expect(setPage).toHaveBeenCalledWith(3);
    });

    it('marks current page button as aria-current', () => {
      renderPagination({ page: 2, totalPages: 5 });
      expect(screen.getByLabelText('Page 2')).toHaveAttribute('aria-current', 'page');
    });
  });

  describe('page size selector', () => {
    it('calls setPageSize on change', () => {
      const setPageSize = jest.fn();
      render(
        <MemoryRouter>
          <Pagination pagination={makePagination({ setPageSize })} />
        </MemoryRouter>,
      );
      fireEvent.change(screen.getByRole('combobox'), { target: { value: '25' } });
      expect(setPageSize).toHaveBeenCalledWith(25);
    });

    it('reflects current pageSize in selector', () => {
      renderPagination({ pageSize: 50 });
      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('50');
    });
  });

  describe('ellipsis for many pages', () => {
    it('shows ellipsis when there are many pages', () => {
      renderPagination({ page: 5, totalPages: 10, totalCount: 100, from: 41, to: 50 });
      const ellipses = screen.queryAllByText('…');
      expect(ellipses.length).toBeGreaterThan(0);
    });
  });

  describe('keyboard navigation', () => {
    it('Space key triggers page navigation', () => {
      const setPage = jest.fn();
      render(
        <MemoryRouter>
          <Pagination pagination={makePagination({ page: 1, totalPages: 3, setPage })} />
        </MemoryRouter>,
      );
      const page3btn = screen.getByLabelText('Page 3');
      fireEvent.keyDown(page3btn, { key: ' ' });
      expect(setPage).toHaveBeenCalledWith(3);
    });

    it('Enter key triggers page navigation', () => {
      const setPage = jest.fn();
      render(
        <MemoryRouter>
          <Pagination pagination={makePagination({ page: 1, totalPages: 3, setPage })} />
        </MemoryRouter>,
      );
      const page2btn = screen.getByLabelText('Page 2');
      fireEvent.keyDown(page2btn, { key: 'Enter' });
      expect(setPage).toHaveBeenCalledWith(2);
    });
  });
});
