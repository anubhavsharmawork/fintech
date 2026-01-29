import React from 'react';
import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Privacy from './Privacy';

describe('Privacy Page', () => {
  const renderPrivacy = () => {
    return render(
      <BrowserRouter>
        <Privacy />
      </BrowserRouter>
    );
  };

  describe('Page Header', () => {
    it('should render Privacy Policy heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: 'Privacy Policy' })).toBeInTheDocument();
    });
  });

  describe('Data Collection Section', () => {
    it('should render Data Collection heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: 'Data Collection' })).toBeInTheDocument();
    });

    it('should display no personal data collection message', () => {
      renderPrivacy();
      expect(screen.getByText(/no personal data/i)).toBeInTheDocument();
    });
  });

  describe('Server Logs Section', () => {
    it('should render Server Logs heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: 'Server Logs' })).toBeInTheDocument();
    });

    it('should mention IP addresses logging', () => {
      renderPrivacy();
      expect(screen.getByText(/IP addresses/i)).toBeInTheDocument();
    });
  });

  describe('Privacy Rights Section', () => {
    it('should render Your Rights heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: /Your Rights/i })).toBeInTheDocument();
    });

    it('should mention NZ Privacy Act', () => {
      renderPrivacy();
      expect(screen.getByText(/NZ Privacy Act 2020/i)).toBeInTheDocument();
    });
  });

  describe('Security Section', () => {
    it('should render Security heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: 'Security' })).toBeInTheDocument();
    });

    it('should mention HTTPS and TLS', () => {
      renderPrivacy();
      expect(screen.getByText(/HTTPS.*TLS 1\.2/i)).toBeInTheDocument();
    });

    it('should mention dependency scanning', () => {
      renderPrivacy();
      expect(screen.getByText(/dependencies scanned/i)).toBeInTheDocument();
    });
  });

  describe('Third-Party Services Section', () => {
    it('should render Third-Party Services heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: 'Third-Party Services' })).toBeInTheDocument();
    });

    it('should mention Heroku hosting', () => {
      renderPrivacy();
      expect(screen.getByText(/Heroku/i)).toBeInTheDocument();
    });
  });

  describe('Contact Section', () => {
    it('should render Contact heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: 'Contact' })).toBeInTheDocument();
    });

    it('should display contact name', () => {
      renderPrivacy();
      expect(screen.getByText(/Anubhav Sharma/i)).toBeInTheDocument();
    });

    it('should have email link', () => {
      renderPrivacy();
      const emailLink = screen.getByRole('link', { name: /anubhav\.sharma\.work@outlook\.com/i });
      expect(emailLink).toBeInTheDocument();
      expect(emailLink).toHaveAttribute('href', 'mailto:anubhav.sharma.work@outlook.com');
    });
  });

  describe('Footer Section', () => {
    it('should display last updated date', () => {
      renderPrivacy();
      expect(screen.getByText(/Last Updated/i)).toBeInTheDocument();
    });
  });
});
