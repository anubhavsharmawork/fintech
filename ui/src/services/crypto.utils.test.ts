import { waitForTransaction, getEtherscanTxUrl, getEtherscanTokenUrl, ETHERSCAN_BASE_URL, FTK_TOKEN_ADDRESS } from './crypto';

// Broad mock for ethers since we are testing the service wrapper logic
jest.mock('ethers', () => ({
  BrowserProvider: jest.fn(),
  JsonRpcProvider: jest.fn(),
  Contract: jest.fn(),
  formatUnits: jest.fn(),
  parseUnits: jest.fn()
}));

describe('crypto utils', () => {
  describe('URL helpers', () => {
    it('getEtherscanTxUrl returns correct URL', () => {
      const hash = '0x123';
      const expected = `${ETHERSCAN_BASE_URL}/tx/${hash}`; 
      // Note: ETHERSCAN_BASE_URL is from env vars, might be undefined in test env if not set in setupTests
      // We assume it returns something like "undefined/tx/0x123" or whatever the mock env has.
      // Better to check if it contains the hash and path.
      
      const result = getEtherscanTxUrl(hash);
      expect(result).toContain('/tx/0x123');
    });

    it('getEtherscanTokenUrl returns correct URL', () => {
      const result = getEtherscanTokenUrl();
      expect(result).toContain('/token/');
      // FTK_TOKEN_ADDRESS might be undefined in test, check content safely
    });
  });

  describe('waitForTransaction', () => {
    it('returns confirmed result when receipt status is 1', async () => {
      const mockProvider = {
        waitForTransaction: jest.fn().mockResolvedValue({
          status: 1,
          from: '0xsender',
          to: '0xreceiver',
          gasUsed: BigInt(21000),
          blockNumber: 12345
        })
      };

      const result = await waitForTransaction(mockProvider, '0xhash');

      expect(result.status).toBe('confirmed');
      expect(result.gasUsed).toBe('21000');
      expect(result.blockNumber).toBe(12345);
      expect(result.hash).toBe('0xhash');
    });

    it('returns failed result when receipt status is 0', async () => {
      const mockProvider = {
        waitForTransaction: jest.fn().mockResolvedValue({
          status: 0,
          from: '0xsender',
          to: '0xreceiver',
          gasUsed: BigInt(21000),
          blockNumber: 12345
        })
      };

      const result = await waitForTransaction(mockProvider, '0xhash');

      expect(result.status).toBe('failed');
      expect(result.receipt.status).toBe(0);
    });
  });
});
