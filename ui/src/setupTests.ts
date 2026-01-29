import '@testing-library/jest-dom';

// Mock localStorage with proper jest.fn() instances
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: jest.fn((key: string) => store[key] || null),
    setItem: jest.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: jest.fn((key: string) => {
      delete store[key];
    }),
    clear: jest.fn(() => {
      store = {};
    }),
  };
})();

Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
  writable: true,
});

// Mock window.ethereum for crypto tests
Object.defineProperty(window, 'ethereum', {
  value: {
    request: jest.fn(),
  },
  writable: true,
});

// Mock fetch globally
global.fetch = jest.fn();

// Mock process.env
process.env.REACT_APP_SEPOLIA_RPC = 'https://sepolia.infura.io/v3/test';
process.env.REACT_APP_FTK_ADDRESS = '0x1234567890123456789012345678901234567890';
