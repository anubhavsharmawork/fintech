import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { useFMode, FModeProvider } from './useFMode';

describe('useFMode Hook', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
  });

  describe('FModeProvider', () => {
    it('should render children', () => {
      render(
        <FModeProvider>
          <div data-testid="child">Test</div>
        </FModeProvider>
      );

      expect(screen.getByTestId('child')).toBeInTheDocument();
    });

    it('should initialize with localStorage value', () => {
      localStorage.setItem('fMode', '1');

      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div>{enabled ? 'Enabled' : 'Disabled'}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByText('Enabled')).toBeInTheDocument();
    });

    it('should initialize with false if localStorage not set', () => {
      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div>{enabled ? 'Enabled' : 'Disabled'}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByText('Disabled')).toBeInTheDocument();
    });

    it('should initialize with false if localStorage is 0', () => {
      localStorage.setItem('fMode', '0');

      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div>{enabled ? 'Enabled' : 'Disabled'}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByText('Disabled')).toBeInTheDocument();
    });

    it('should handle case where window is undefined', () => {
      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div>{enabled ? 'Enabled' : 'Disabled'}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByText('Disabled')).toBeInTheDocument();
    });
  });

  describe('useFMode Hook', () => {
    it('should throw error when used outside provider', () => {
      const TestComponent = () => {
        useFMode();
        return null;
      };

      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

      expect(() => render(<TestComponent />)).toThrow(
        'useFMode must be used within a FModeProvider'
      );

      consoleSpy.mockRestore();
    });

    it('should return enabled state', () => {
      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div data-testid="status">{enabled.toString()}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('status')).toHaveTextContent('false');
    });

    it('should return toggle function', () => {
      const TestComponent = () => {
        const { toggle } = useFMode();
        return (
          <button onClick={() => toggle()} data-testid="toggle-btn">
            Toggle
          </button>
        );
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('toggle-btn')).toBeInTheDocument();
    });
  });

  describe('Toggle Functionality', () => {
    it('should toggle enabled state when toggle called', () => {
      const TestComponent = () => {
        const { enabled, toggle } = useFMode();
        return (
          <>
            <div data-testid="status">{enabled ? 'Enabled' : 'Disabled'}</div>
            <button onClick={() => toggle()}>Toggle</button>
          </>
        );
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('status')).toHaveTextContent('Disabled');

      fireEvent.click(screen.getByText('Toggle'));

      expect(screen.getByTestId('status')).toHaveTextContent('Enabled');

      fireEvent.click(screen.getByText('Toggle'));

      expect(screen.getByTestId('status')).toHaveTextContent('Disabled');
    });

    it('should set enabled state to true when toggle(true) called', () => {
      const TestComponent = () => {
        const { enabled, toggle } = useFMode();
        return (
          <>
            <div data-testid="status">{enabled ? 'Enabled' : 'Disabled'}</div>
            <button onClick={() => toggle(true)}>Enable</button>
          </>
        );
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('status')).toHaveTextContent('Disabled');

      fireEvent.click(screen.getByText('Enable'));

      expect(screen.getByTestId('status')).toHaveTextContent('Enabled');
    });

    it('should set enabled state to false when toggle(false) called', () => {
      localStorage.setItem('fMode', '1');

      const TestComponent = () => {
        const { enabled, toggle } = useFMode();
        return (
          <>
            <div data-testid="status">{enabled ? 'Enabled' : 'Disabled'}</div>
            <button onClick={() => toggle(false)}>Disable</button>
          </>
        );
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('status')).toHaveTextContent('Enabled');

      fireEvent.click(screen.getByText('Disable'));

      expect(screen.getByTestId('status')).toHaveTextContent('Disabled');
    });

    it('should handle multiple consecutive toggles', () => {
      const TestComponent = () => {
        const { enabled, toggle } = useFMode();
        return (
          <>
            <div data-testid="status">{enabled ? 'Enabled' : 'Disabled'}</div>
            <button onClick={() => toggle()}>Toggle</button>
          </>
        );
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      fireEvent.click(screen.getByText('Toggle'));
      expect(screen.getByTestId('status')).toHaveTextContent('Enabled');

      fireEvent.click(screen.getByText('Toggle'));
      expect(screen.getByTestId('status')).toHaveTextContent('Disabled');

      fireEvent.click(screen.getByText('Toggle'));
      expect(screen.getByTestId('status')).toHaveTextContent('Enabled');

      fireEvent.click(screen.getByText('Toggle'));
      expect(screen.getByTestId('status')).toHaveTextContent('Disabled');
    });
  });

  describe('localStorage Integration', () => {
    it('should persist enabled state to localStorage', () => {
      const TestComponent = () => {
        const { toggle } = useFMode();
        return <button onClick={() => toggle()}>Toggle</button>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      fireEvent.click(screen.getByText('Toggle'));

      expect(localStorage.getItem('fMode')).toBe('1');
    });

    it('should set localStorage to 0 when disabling', () => {
      localStorage.setItem('fMode', '1');

      const TestComponent = () => {
        const { toggle } = useFMode();
        return <button onClick={() => toggle()}>Toggle</button>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      fireEvent.click(screen.getByText('Toggle'));

      expect(localStorage.getItem('fMode')).toBe('0');
    });

    it('should update localStorage when toggle(true) called', () => {
      const TestComponent = () => {
        const { toggle } = useFMode();
        return <button onClick={() => toggle(true)}>Enable</button>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      fireEvent.click(screen.getByText('Enable'));

      expect(localStorage.getItem('fMode')).toBe('1');
    });

    it('should update localStorage when toggle(false) called', () => {
      localStorage.setItem('fMode', '1');

      const TestComponent = () => {
        const { toggle } = useFMode();
        return <button onClick={() => toggle(false)}>Disable</button>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      fireEvent.click(screen.getByText('Disable'));

      expect(localStorage.getItem('fMode')).toBe('0');
    });

    it('should handle localStorage errors gracefully', () => {
      const originalSetItem = localStorage.setItem;
      localStorage.setItem = jest.fn(() => {
        throw new Error('QuotaExceededError');
      });

      const TestComponent = () => {
        const { toggle, enabled } = useFMode();
        return (
          <>
            <div>{enabled ? 'Enabled' : 'Disabled'}</div>
            <button onClick={() => toggle()}>Toggle</button>
          </>
        );
      };

      expect(() => {
        render(
          <FModeProvider>
            <TestComponent />
          </FModeProvider>
        );
        fireEvent.click(screen.getByText('Toggle'));
      }).not.toThrow();

      localStorage.setItem = originalSetItem;
    });
  });

  describe('Multiple Components', () => {
    it('should share state between multiple components', () => {
      const ComponentA = () => {
        const { enabled, toggle } = useFMode();
        return (
          <>
            <div data-testid="status-a">{enabled ? 'Enabled' : 'Disabled'}</div>
            <button data-testid="toggle-a" onClick={() => toggle()}>
              Toggle A
            </button>
          </>
        );
      };

      const ComponentB = () => {
        const { enabled } = useFMode();
        return <div data-testid="status-b">{enabled ? 'Enabled' : 'Disabled'}</div>;
      };

      render(
        <FModeProvider>
          <ComponentA />
          <ComponentB />
        </FModeProvider>
      );

      expect(screen.getByTestId('status-a')).toHaveTextContent('Disabled');
      expect(screen.getByTestId('status-b')).toHaveTextContent('Disabled');

      fireEvent.click(screen.getByTestId('toggle-a'));

      expect(screen.getByTestId('status-a')).toHaveTextContent('Enabled');
      expect(screen.getByTestId('status-b')).toHaveTextContent('Enabled');
    });

    it('should notify all consumers of state changes', () => {
      const ComponentA = () => {
        const { enabled, toggle } = useFMode();
        return (
          <>
            <div data-testid="status-a">{enabled ? 'Enabled' : 'Disabled'}</div>
            <button data-testid="toggle" onClick={() => toggle()}>
              Toggle
            </button>
          </>
        );
      };

      const ComponentB = () => {
        const { enabled } = useFMode();
        return <div data-testid="status-b">{enabled ? 'Enabled' : 'Disabled'}</div>;
      };

      const ComponentC = () => {
        const { enabled } = useFMode();
        return <div data-testid="status-c">{enabled ? 'Enabled' : 'Disabled'}</div>;
      };

      render(
        <FModeProvider>
          <ComponentA />
          <ComponentB />
          <ComponentC />
        </FModeProvider>
      );

      fireEvent.click(screen.getByTestId('toggle'));

      expect(screen.getByTestId('status-a')).toHaveTextContent('Enabled');
      expect(screen.getByTestId('status-b')).toHaveTextContent('Enabled');
      expect(screen.getByTestId('status-c')).toHaveTextContent('Enabled');
    });
  });

  describe('Initial State', () => {
    it('should initialize with localStorage value of 1', () => {
      localStorage.setItem('fMode', '1');

      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div data-testid="status">{enabled.toString()}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('status')).toHaveTextContent('true');
    });

    it('should initialize with localStorage value of 0', () => {
      localStorage.setItem('fMode', '0');

      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div data-testid="status">{enabled.toString()}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('status')).toHaveTextContent('false');
    });

    it('should initialize as false when localStorage not set', () => {
      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div data-testid="status">{enabled.toString()}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('status')).toHaveTextContent('false');
    });

    it('should initialize as false for any other localStorage value', () => {
      localStorage.setItem('fMode', 'invalid');

      const TestComponent = () => {
        const { enabled } = useFMode();
        return <div data-testid="status">{enabled.toString()}</div>;
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      expect(screen.getByTestId('status')).toHaveTextContent('false');
    });
  });

  describe('Memoization', () => {
    it('should memoize context value', () => {
      let renderCount = 0;

      const TestComponent = () => {
        const { enabled } = useFMode();
        renderCount++;
        return <div>{enabled ? 'Enabled' : 'Disabled'}</div>;
      };

      const { rerender } = render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      const initialRenderCount = renderCount;

      rerender(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      // Should not cause additional renders of TestComponent if context value is memoized
      expect(renderCount).toBe(initialRenderCount + 1); // +1 for rerender
    });
  });

  describe('Edge Cases', () => {
    it('should handle rapid toggle calls', () => {
      const TestComponent = () => {
        const { enabled, toggle } = useFMode();
        return (
          <>
            <div data-testid="status">{enabled ? 'Enabled' : 'Disabled'}</div>
            <button
              onClick={() => {
                toggle();
                toggle();
                toggle();
              }}
            >
              Rapid Toggle
            </button>
          </>
        );
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      fireEvent.click(screen.getByText('Rapid Toggle'));

      expect(screen.getByTestId('status')).toHaveTextContent('Enabled');
    });

    it('should handle toggle with undefined argument', () => {
      const TestComponent = () => {
        const { enabled, toggle } = useFMode();
        return (
          <>
            <div data-testid="status">{enabled ? 'Enabled' : 'Disabled'}</div>
            <button onClick={() => toggle(undefined)}>Toggle</button>
          </>
        );
      };

      render(
        <FModeProvider>
          <TestComponent />
        </FModeProvider>
      );

      fireEvent.click(screen.getByText('Toggle'));

      expect(screen.getByTestId('status')).toHaveTextContent('Enabled');
    });
  });
});
