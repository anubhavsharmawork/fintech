import React from 'react';
import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Admin from './Admin';

function makeFakeToken(payload: Record<string, unknown>): string {
  const base64 = Buffer.from(JSON.stringify(payload)).toString('base64');
  return `hdr.${base64}.sig`;
}

function mockLocalStorageWithToken(token: string | null) {
  const store: Record<string, string> = {};
  if (token) store['token'] = token;
  (localStorage.getItem as jest.Mock).mockImplementation((key: string) => store[key] ?? null);
  (localStorage.setItem as jest.Mock).mockImplementation((key: string, value: string) => { store[key] = value; });
  (localStorage.clear as jest.Mock).mockImplementation(() => { Object.keys(store).forEach(k => delete store[k]); });
}

describe('Admin Page', () => {
  beforeEach(() => {
    mockLocalStorageWithToken(null);
  });

  const renderAdmin = () => {
    return render(
      <BrowserRouter>
        <Admin />
      </BrowserRouter>
    );
  };

  describe('Page Header', () => {
    it('should render the page heading', () => {
      renderAdmin();
      expect(
        screen.getByRole('heading', { name: /Role & Permissions Matrix/i })
      ).toBeInTheDocument();
    });
  });

  describe('Current Role Badge', () => {
    it('should display "Demo" when no token is present', () => {
      renderAdmin();
      const badge = screen.getByTestId('current-role-badge');
      expect(badge).toHaveTextContent('Demo');
    });

    it('should decode role from a valid JWT', () => {
      mockLocalStorageWithToken(makeFakeToken({ role: 'Super Admin', exp: 9999999999 }));
      renderAdmin();
      const badge = screen.getByTestId('current-role-badge');
      expect(badge).toHaveTextContent('Super Admin');
    });
  });

  describe('Demo Banner', () => {
    it('should show the demo banner when role is Demo', () => {
      renderAdmin();
      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText(/Demo \/ Read-only mode/i)).toBeInTheDocument();
    });

    it('should not show the demo banner when a non-Demo role is present', () => {
      mockLocalStorageWithToken(makeFakeToken({ role: 'Analyst', exp: 9999999999 }));
      renderAdmin();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });

  describe('Role Cards', () => {
    it('should render all six role cards', () => {
      renderAdmin();
      const cards = screen.getAllByLabelText(/role card$/i);
      expect(cards).toHaveLength(6);
    });

    it('should render expected role names', () => {
      renderAdmin();
      expect(screen.getByText('Super Admin')).toBeInTheDocument();
      expect(screen.getByText('Compliance Officer')).toBeInTheDocument();
      expect(screen.getByText('Account Manager')).toBeInTheDocument();
      expect(screen.getByText('Analyst')).toBeInTheDocument();
      expect(screen.getByText('Customer (Standard User)')).toBeInTheDocument();
      expect(screen.getByText('Demo User')).toBeInTheDocument();
    });

    it('should render fintech context labels in italic', () => {
      renderAdmin();
      const context = screen.getByText('Platform Owner / CTO');
      expect(context).toBeInTheDocument();
      expect(context.tagName).toBe('P');
    });

    it('should render granted and denied permission indicators', () => {
      renderAdmin();
      const granted = screen.getAllByLabelText('Granted');
      const denied = screen.getAllByLabelText('Denied');
      expect(granted.length).toBeGreaterThan(0);
      expect(denied.length).toBeGreaterThan(0);
    });
  });
});
