import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
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

  describe('Page Header and Navigation', () => {
    it('should render Privacy Policy heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: 'Privacy Policy' })).toBeInTheDocument();
    });

    it('should render Privacy Policy tab as active by default', () => {
      renderPrivacy();
      const privacyTab = screen.getByRole('tab', { name: 'Privacy Policy' });
      expect(privacyTab).toHaveAttribute('aria-selected', 'true');
    });

    it('should render Terms of Service tab', () => {
      renderPrivacy();
      expect(screen.getByRole('tab', { name: 'Terms of Service' })).toBeInTheDocument();
    });

    it('should display version and effective date', () => {
      renderPrivacy();
      expect(screen.getByText(/Version 1\.0/i)).toBeInTheDocument();
      expect(screen.getByText(/March 2026/i)).toBeInTheDocument();
    });
  });

  describe('Our Promise Section', () => {
    it('should render Our Promise section', () => {
      renderPrivacy();
      const promiseTexts = screen.getAllByText(/Our Promise/i);
      expect(promiseTexts.length).toBeGreaterThan(0);
    });

    it('should display never sell data message', () => {
      renderPrivacy();
      expect(screen.getByText(/never sell it/i)).toBeInTheDocument();
    });
  });

  describe('Information We Collect Section', () => {
    it('should render Information We Collect heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: /Information We Collect/i })).toBeInTheDocument();
    });

    it('should mention account details', () => {
      renderPrivacy();
      expect(screen.getByText(/Account details:/i)).toBeInTheDocument();
    });

    it('should mention what we do not collect', () => {
      renderPrivacy();
      expect(screen.getByText(/We do not collect biometric data/i)).toBeInTheDocument();
    });
  });

  describe('Data Storage & Security Section', () => {
    it('should render Data Storage & Security heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: /Data Storage & Security/i })).toBeInTheDocument();
    });

    it('should mention encryption and TLS', () => {
      renderPrivacy();
      expect(screen.getByText(/TLS 1\.2\+/i)).toBeInTheDocument();
    });

    it('should mention OWASP compliance', () => {
      renderPrivacy();
      expect(screen.getByText(/OWASP Top 10 compliant/i)).toBeInTheDocument();
    });

    it('should mention dependency scanning', () => {
      renderPrivacy();
      expect(screen.getByText(/Dependencies scanned weekly/i)).toBeInTheDocument();
    });
  });

  describe('Data Sharing Section', () => {
    it('should render Data Sharing heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: /Data Sharing/i })).toBeInTheDocument();
    });

    it('should mention Heroku hosting', () => {
      renderPrivacy();
      const herokus = screen.getAllByText(/Heroku/i);
      expect(herokus.length).toBeGreaterThan(0);
    });
  });

  describe('Your Rights Section', () => {
    it('should render Your Rights heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: /Your Rights \(NZ Privacy Act 2020\)/i })).toBeInTheDocument();
    });

    it('should mention NZ Privacy Act 2020', () => {
      renderPrivacy();
      const matches = screen.getAllByText(/NZ Privacy Act 2020/i);
      expect(matches.length).toBeGreaterThan(0);
    });

    it('should mention data export rights', () => {
      renderPrivacy();
      expect(screen.getByText(/Right of access \(Principle 6\)/i)).toBeInTheDocument();
    });
  });

  describe('Blockchain Data Section', () => {
    it('should render Blockchain Data heading', () => {
      renderPrivacy();
      expect(screen.getByRole('heading', { name: /Blockchain & Distributed Ledger Data/i })).toBeInTheDocument();
    });

    it('should mention immutable transactions', () => {
      renderPrivacy();
      expect(screen.getByText(/immutable and publicly visible/i)).toBeInTheDocument();
    });
  });

  describe('Contact Section', () => {
    it('should display contact name', () => {
      renderPrivacy();
      expect(screen.getByText(/Anubhav Sharma/i)).toBeInTheDocument();
    });

    it('should have email link', () => {
      renderPrivacy();
      const emailLinks = screen.getAllByRole('link', { name: /anubhav\.sharma\.work@outlook\.com/i });
      expect(emailLinks.length).toBeGreaterThan(0);
      expect(emailLinks[0]).toHaveAttribute('href', 'mailto:anubhav.sharma.work@outlook.com');
    });
  });

  describe('Terms of Service Tab', () => {
    it('should switch to Terms of Service when tab is clicked', () => {
      renderPrivacy();
      const termsTab = screen.getByRole('tab', { name: 'Terms of Service' });
      fireEvent.click(termsTab);
      expect(screen.getByRole('heading', { name: 'Terms of Service' })).toBeInTheDocument();
    });

    it('should display What This Platform Is section in Terms', () => {
      renderPrivacy();
      const termsTab = screen.getByRole('tab', { name: 'Terms of Service' });
      fireEvent.click(termsTab);
      expect(screen.getByRole('heading', { name: /What This Platform Is/i })).toBeInTheDocument();
    });

    it('should display Acceptable Use section in Terms', () => {
      renderPrivacy();
      const termsTab = screen.getByRole('tab', { name: 'Terms of Service' });
      fireEvent.click(termsTab);
      expect(screen.getByRole('heading', { name: /Acceptable Use/i })).toBeInTheDocument();
    });

    it('should display Distributed Ledger section in Terms', () => {
      renderPrivacy();
      const termsTab = screen.getByRole('tab', { name: 'Terms of Service' });
      fireEvent.click(termsTab);
      expect(screen.getByRole('heading', { name: /Distributed Ledger Features/i })).toBeInTheDocument();
    });
  });

  describe('Table of Contents', () => {
    it('should render table of contents navigation', () => {
      renderPrivacy();
      expect(screen.getByRole('navigation', { name: /table of contents/i })).toBeInTheDocument();
    });

    it('should have TOC links for privacy sections', () => {
      renderPrivacy();
      expect(screen.getByRole('link', { name: 'Our Promise' })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Information We Collect' })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Your Rights (NZ)' })).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have proper article role', () => {
      renderPrivacy();
      expect(screen.getByRole('article', { name: /Privacy Policy and Terms of Service/i })).toBeInTheDocument();
    });

    it('should have proper tablist role for navigation', () => {
      renderPrivacy();
      expect(screen.getByRole('tablist')).toBeInTheDocument();
    });
  });
});
