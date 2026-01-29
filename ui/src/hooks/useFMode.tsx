import * as React from 'react';

interface FModeContextType {
  enabled: boolean;
  toggle: (value?: boolean) => void;
}

const FModeContext = React.createContext<FModeContextType | undefined>(undefined);

export const FModeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [enabled, setEnabled] = React.useState(() => {
    if (typeof window === 'undefined') return false;
    return localStorage.getItem('fMode') === '1';
  });

  const toggle = React.useCallback((value?: boolean) => {
    setEnabled(prev => {
      const newValue = value !== undefined ? value : !prev;
      if (typeof window !== 'undefined') {
        localStorage.setItem('fMode', newValue ? '1' : '0');
      }
      return newValue;
    });
  }, []);

  const value = React.useMemo(() => ({ enabled, toggle }), [enabled, toggle]);

  return <FModeContext.Provider value={value}>{children}</FModeContext.Provider>;
};

export const useFMode = () => {
  const context = React.useContext(FModeContext);
  if (context === undefined) {
    throw new Error('useFMode must be used within a FModeProvider');
  }
  return context;
};
