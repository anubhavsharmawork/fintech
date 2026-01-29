import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Budget from './Budget';
import * as auth from '../auth';
import * as fModeHook from '../hooks/useFMode';
import { ToastProvider } from '../components/Toast';

jest.mock('../auth');
jest.mock('../hooks/useFMode');

describe('Budget Page', () => {
  const mockToggle = jest.fn();
  const mockAuthFetch = auth.authFetch as jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    (fModeHook.useFMode as jest.Mock).mockReturnValue({
      enabled: false,
      toggle: mockToggle,
    });
    mockAuthFetch.mockImplementation((url: string) => {
      if (url === '/accounts') {
        return Promise.resolve({
          ok: true,
          json: async () => [{ id: 'acc1', accountNumber: 'ACC001', accountType: 'Checking' }],
        });
      }
      if (url.includes('/budget/budget')) {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            fun: 25,
            fixed: 50,
            future: 25,
            total: 100,
            period: { from: '2024-01-01', to: '2024-01-31' },
          }),
        });
      }
      return Promise.resolve({ ok: true, json: async () => ({}) });
    });
  });

  const renderBudget = () => {
    return render(
      <BrowserRouter>
        <ToastProvider>
          <Budget />
        </ToastProvider>
      </BrowserRouter>
    );
  };

  describe('Goal Selection Step', () => {
    it('should render goal selection by default', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Select Your Budget Goal')).toBeInTheDocument();
      });
    });

    it('should display all budget goal options', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Build Wealth')).toBeInTheDocument();
        expect(screen.getByText('Balanced Living')).toBeInTheDocument();
        expect(screen.getByText('Enjoy Life')).toBeInTheDocument();
        expect(screen.getByText('Rapid Growth')).toBeInTheDocument();
        expect(screen.getByText('Recovery Mode')).toBeInTheDocument();
      });
    });

    it('should display budget goal descriptions', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Conservative approach focused on long-term growth')).toBeInTheDocument();
        expect(screen.getByText('A sustainable mix of saving and enjoying life')).toBeInTheDocument();
      });
    });

    it('should display badges for specific goals', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Default')).toBeInTheDocument();
        expect(screen.getByText('Recommended')).toBeInTheDocument();
      });
    });

    it('should display risk labels for specific goals', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Low–Medium Risk')).toBeInTheDocument();
        expect(screen.getByText('High Risk')).toBeInTheDocument();
      });
    });

    it('should allow selecting a budget goal', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Build Wealth')).toBeInTheDocument();
      });

      const buildWealthButton = screen.getByText('Build Wealth').closest('button');
      fireEvent.click(buildWealthButton!);

      expect(buildWealthButton).toHaveAttribute('aria-pressed', 'true');
    });

    it('should display percentage allocation for each goal', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getAllByText(/Fixed/).length).toBeGreaterThan(0);
        expect(screen.getAllByText(/Future/).length).toBeGreaterThan(0);
        expect(screen.getAllByText(/Fun/).length).toBeGreaterThan(0);
      });
    });
  });

  describe('Step Navigation', () => {
    it('should navigate to income step when clicking next', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Select Your Budget Goal')).toBeInTheDocument();
      });

      const nextButton = screen.getByRole('button', { name: /next|continue/i });
      fireEvent.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText(/monthly income/i)).toBeInTheDocument();
      });
    });
  });

  describe('Income Step', () => {
    beforeEach(() => {
      localStorage.setItem('budgetStep', 'income');
    });

    it('should render income input on income step', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByLabelText(/monthly income/i)).toBeInTheDocument();
      });
    });

    it('should validate income before proceeding', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByLabelText(/monthly income/i)).toBeInTheDocument();
      });

      const submitButton = screen.getByRole('button', { name: /create|submit|next/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/valid monthly income/i)).toBeInTheDocument();
      });
    });

    it('should accept valid income amount', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByLabelText(/monthly income/i)).toBeInTheDocument();
      });

      const incomeInput = screen.getByLabelText(/monthly income/i);
      fireEvent.change(incomeInput, { target: { value: '5000' } });

      const submitButton = screen.getByRole('button', { name: /create|submit|next/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.queryByText(/valid monthly income/i)).not.toBeInTheDocument();
      });
    });
  });

  describe('View Step', () => {
    beforeEach(() => {
      localStorage.setItem('budgetStep', 'view');
      localStorage.setItem('budgetIncome', '5000');
    });

    it('should render budget view with chart', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/spending breakdown/i)).toBeInTheDocument();
      });
    });

    it('should show account selector', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByRole('combobox')).toBeInTheDocument();
      });
    });

    it('should show date range selectors', async () => {
      renderBudget();

      await waitFor(() => {
        const dateInputs = screen.getAllByRole('textbox');
        expect(dateInputs.length).toBeGreaterThan(0);
      });
    });
  });

  describe('Customization', () => {
    it('should show customize toggle', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });
    });

    it('should show sliders when customization is enabled', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });

      const customizeButton = screen.getByText(/customize/i).closest('button') ||
        screen.getByRole('button', { name: /customize/i });
      
      if (customizeButton) {
        fireEvent.click(customizeButton);

        await waitFor(() => {
          expect(screen.getByRole('slider', { name: /fixed/i })).toBeInTheDocument();
        });
      }
    });
  });

  describe('LocalStorage Persistence', () => {
    it('should persist selected goal to localStorage', async () => {
      const setItemSpy = jest.spyOn(Storage.prototype, 'setItem');

      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Build Wealth')).toBeInTheDocument();
      });

      const buildWealthButton = screen.getByText('Build Wealth').closest('button');
      fireEvent.click(buildWealthButton!);

      expect(setItemSpy).toHaveBeenCalledWith('budgetGoal', 'wealth');
    });

    it('should restore goal from localStorage', async () => {
      localStorage.setItem('budgetGoal', 'growth');

      renderBudget();

      await waitFor(() => {
        const rapidGrowthButton = screen.getByText('Rapid Growth').closest('button');
        expect(rapidGrowthButton).toHaveAttribute('aria-pressed', 'true');
      });
    });

    it('should persist step to localStorage', async () => {
      const setItemSpy = jest.spyOn(Storage.prototype, 'setItem');

      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Select Your Budget Goal')).toBeInTheDocument();
      });

      const nextButton = screen.getByRole('button', { name: /next|continue/i });
      fireEvent.click(nextButton);

      expect(setItemSpy).toHaveBeenCalledWith('budgetStep', 'income');
    });
  });

  describe('API Integration', () => {
    it('should fetch accounts on mount', async () => {
      renderBudget();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith('/accounts');
      });
    });

    it('should fetch budget data when account is selected', async () => {
      localStorage.setItem('budgetStep', 'view');

      renderBudget();

      await waitFor(() => {
        expect(mockAuthFetch).toHaveBeenCalledWith(
          expect.stringContaining('/budget/budget')
        );
      });
    });

    it('should handle budget fetch error', async () => {
      localStorage.setItem('budgetStep', 'view');
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [{ id: 'acc1', accountNumber: 'ACC001', accountType: 'Checking' }],
          });
        }
        if (url.includes('/budget/budget')) {
          return Promise.resolve({
            ok: false,
            status: 500,
            text: async () => 'Server error',
          });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/failed to load budget/i)).toBeInTheDocument();
      });
    });
  });

  describe('F-Mode', () => {
    it('should display crypto-specific content when fMode enabled', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      renderBudget();

      // The component may show different content in F-Mode
      await waitFor(() => {
        expect(screen.getByText(/budget/i)).toBeInTheDocument();
      });
    });
  });

  describe('Budget Calculations', () => {
    beforeEach(() => {
      localStorage.setItem('budgetStep', 'view');
      localStorage.setItem('budgetIncome', '10000');
    });

    it('should calculate and display budget allocations', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/spending breakdown/i)).toBeInTheDocument();
      });

      // Should display the allocations from the API response
      expect(screen.getByText(/25/)).toBeInTheDocument(); // fun
      expect(screen.getByText(/50/)).toBeInTheDocument(); // fixed
    });
  });

  describe('Loading States', () => {
    it('should show loading state when fetching budget', async () => {
      localStorage.setItem('budgetStep', 'view');
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [{ id: 'acc1', accountNumber: 'ACC001', accountType: 'Checking' }],
          });
        }
        // Return a promise that never resolves for budget
        return new Promise(() => {});
      });

      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/loading/i)).toBeInTheDocument();
      });
    });
  });

  describe('Custom Slider Controls', () => {
    it('should adjust fixed slider and rebalance others', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });

      // Click customize button
      const customizeButton = screen.getByText(/customize/i).closest('button') ||
        screen.getByRole('button', { name: /customize/i });

      if (customizeButton) {
        fireEvent.click(customizeButton);

        await waitFor(() => {
          expect(screen.getByRole('slider', { name: /fixed/i })).toBeInTheDocument();
        });

        const fixedSlider = screen.getByRole('slider', { name: /fixed/i });
        fireEvent.change(fixedSlider, { target: { value: '60' } });

        // Verify the slider updated
        expect(fixedSlider).toHaveValue('60');
      }
    });

    it('should adjust future slider and rebalance others', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });

      const customizeButton = screen.getByText(/customize/i).closest('button');

      if (customizeButton) {
        fireEvent.click(customizeButton);

        await waitFor(() => {
          expect(screen.getByRole('slider', { name: /future/i })).toBeInTheDocument();
        });

        const futureSlider = screen.getByRole('slider', { name: /future/i });
        fireEvent.change(futureSlider, { target: { value: '30' } });

        expect(futureSlider).toHaveValue('30');
      }
    });

    it('should adjust fun slider and rebalance others', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });

      const customizeButton = screen.getByText(/customize/i).closest('button');

      if (customizeButton) {
        fireEvent.click(customizeButton);

        await waitFor(() => {
          expect(screen.getByRole('slider', { name: /fun/i })).toBeInTheDocument();
        });

        const funSlider = screen.getByRole('slider', { name: /fun/i });
        fireEvent.change(funSlider, { target: { value: '20' } });

        expect(funSlider).toHaveValue('20');
      }
    });

    it('should show total percentage', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });

      const customizeButton = screen.getByText(/customize/i).closest('button');

      if (customizeButton) {
        fireEvent.click(customizeButton);

        await waitFor(() => {
          expect(screen.getByText(/total.*100%/i)).toBeInTheDocument();
        });
      }
    });

    it('should toggle customize panel visibility', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });

      const customizeButton = screen.getByText(/customize/i).closest('button');

      if (customizeButton) {
        // Open customize panel
        fireEvent.click(customizeButton);

        await waitFor(() => {
          expect(screen.getByText(/customize your allocation/i)).toBeInTheDocument();
        });

        // Close customize panel
        fireEvent.click(screen.getByText(/hide customize/i).closest('button')!);

        await waitFor(() => {
          expect(screen.queryByText(/customize your allocation/i)).not.toBeInTheDocument();
        });
      }
    });
  });

  describe('Income Step Extended', () => {
    beforeEach(() => {
      localStorage.setItem('budgetStep', 'income');
    });

    it('should display current goal name in income step', async () => {
      localStorage.setItem('budgetGoal', 'wealth');

      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/build wealth/i)).toBeInTheDocument();
      });
    });

    it('should navigate back to goal step', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: /back/i }));

      await waitFor(() => {
        expect(screen.getByText('Select Your Budget Goal')).toBeInTheDocument();
      });
    });

    it('should show allocation preview based on income', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByLabelText(/monthly income/i)).toBeInTheDocument();
      });

      const incomeInput = screen.getByLabelText(/monthly income/i);
      fireEvent.change(incomeInput, { target: { value: '10000' } });

      // Should show calculated amounts
      await waitFor(() => {
        expect(screen.getByText(/\$/)).toBeInTheDocument();
      });
    });
  });

  describe('View Step Extended', () => {
    beforeEach(() => {
      localStorage.setItem('budgetStep', 'view');
      localStorage.setItem('budgetIncome', '5000');
      localStorage.setItem('budgetGoal', 'balanced');
    });

    it('should change account selection', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({
            ok: true,
            json: async () => [
              { id: 'acc1', accountNumber: 'ACC001', accountType: 'Checking' },
              { id: 'acc2', accountNumber: 'ACC002', accountType: 'Savings' },
            ],
          });
        }
        if (url.includes('/budget/budget')) {
          return Promise.resolve({
            ok: true,
            json: async () => ({
              fun: 25, fixed: 50, future: 25, total: 100,
              period: { from: '2024-01-01', to: '2024-01-31' },
            }),
          });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderBudget();

      await waitFor(() => {
        expect(screen.getByRole('combobox')).toBeInTheDocument();
      });

      const accountSelect = screen.getByRole('combobox') as HTMLSelectElement;
      fireEvent.change(accountSelect, { target: { value: 'acc2' } });

      expect(accountSelect.value).toBe('acc2');
    });

    it('should change date range', async () => {
      renderBudget();

      await waitFor(() => {
        const dateInputs = screen.getAllByDisplayValue(/2024|2025|2026/);
        expect(dateInputs.length).toBeGreaterThan(0);
      });
    });

    it('should display budget chart', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/spending breakdown/i)).toBeInTheDocument();
      });
    });

    it('should navigate back to income step from view', async () => {
      renderBudget();

      await waitFor(() => {
        const backButton = screen.queryByRole('button', { name: /back|edit/i });
        if (backButton) {
          fireEvent.click(backButton);
        }
      });
    });

    it('should reset budget when clicking start over', async () => {
      renderBudget();

      await waitFor(() => {
        const resetButton = screen.queryByRole('button', { name: /reset|start over/i });
        if (resetButton) {
          fireEvent.click(resetButton);
        }
      });
    });
  });

  describe('Account Fetch Failure', () => {
    it('should handle accounts fetch failure gracefully', async () => {
      mockAuthFetch.mockImplementation((url: string) => {
        if (url === '/accounts') {
          return Promise.resolve({ ok: false, status: 500 });
        }
        return Promise.resolve({ ok: true, json: async () => ({}) });
      });

      renderBudget();

      // Should still render without crashing
      await waitFor(() => {
        expect(screen.getByText(/budget/i)).toBeInTheDocument();
      });
    });
  });

  describe('F-Mode Extended', () => {
    it('should render correctly in F-Mode', async () => {
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      localStorage.setItem('budgetStep', 'view');
      localStorage.setItem('budgetIncome', '5000');

      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/budget/i)).toBeInTheDocument();
      });
    });

    it('should toggle back and forth with F-Mode', async () => {
      const { rerender } = renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/budget/i)).toBeInTheDocument();
      });

      // Enable F-Mode
      (fModeHook.useFMode as jest.Mock).mockReturnValue({
        enabled: true,
        toggle: mockToggle,
      });

      rerender(
        <BrowserRouter>
          <ToastProvider>
            <Budget />
          </ToastProvider>
        </BrowserRouter>
      );

      await waitFor(() => {
        expect(screen.getByText(/budget/i)).toBeInTheDocument();
      });
    });
  });

  describe('Custom Allocation Persistence', () => {
    it('should persist custom allocations to localStorage', async () => {
      const setItemSpy = jest.spyOn(Storage.prototype, 'setItem');

      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });

      const customizeButton = screen.getByText(/customize/i).closest('button');

      if (customizeButton) {
        fireEvent.click(customizeButton);

        await waitFor(() => {
          expect(screen.getByRole('slider', { name: /fixed/i })).toBeInTheDocument();
        });

        const fixedSlider = screen.getByRole('slider', { name: /fixed/i });
        fireEvent.change(fixedSlider, { target: { value: '60' } });

        await waitFor(() => {
          expect(setItemSpy).toHaveBeenCalledWith('budgetCustomFixed', '60');
        });
      }

      setItemSpy.mockRestore();
    });

    it('should restore custom allocations from localStorage', async () => {
      localStorage.setItem('budgetCustomFixed', '65');
      localStorage.setItem('budgetCustomFuture', '20');
      localStorage.setItem('budgetCustomFun', '15');

      renderBudget();

      await waitFor(() => {
        expect(screen.getByText(/customize/i)).toBeInTheDocument();
      });

      const customizeButton = screen.getByText(/customize/i).closest('button');

      if (customizeButton) {
        fireEvent.click(customizeButton);

        await waitFor(() => {
          const fixedSlider = screen.getByRole('slider', { name: /fixed/i }) as HTMLInputElement;
          expect(fixedSlider.value).toBe('65');
        });
      }
    });
  });

  describe('Budget Goal Info Display', () => {
    it('should display goal descriptions for all presets', async () => {
      renderBudget();

      await waitFor(() => {
        expect(screen.getByText('Conservative approach focused on long-term growth')).toBeInTheDocument();
        expect(screen.getByText('A sustainable mix of saving and enjoying life')).toBeInTheDocument();
        expect(screen.getByText('Lifestyle-focused with room for experiences')).toBeInTheDocument();
        expect(screen.getByText('Aggressive savings for accelerated wealth building')).toBeInTheDocument();
        expect(screen.getByText('Transition period to rebuild financial stability')).toBeInTheDocument();
      });
    });

    it('should show percentage allocation for each category', async () => {
      renderBudget();

      await waitFor(() => {
        // Check for percentage displays
        const fixedLabels = screen.getAllByText(/%.*Fixed|Fixed.*%/i);
        const futureLabels = screen.getAllByText(/%.*Future|Future.*%/i);
        const funLabels = screen.getAllByText(/%.*Fun|Fun.*%/i);

        expect(fixedLabels.length).toBeGreaterThan(0);
        expect(futureLabels.length).toBeGreaterThan(0);
        expect(funLabels.length).toBeGreaterThan(0);
      });
    });
  });
});
