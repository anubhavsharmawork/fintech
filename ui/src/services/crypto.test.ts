import {
  SEPOLIA_RPC,
  FTK_TOKEN_ADDRESS,
  ETHERSCAN_BASE_URL,
  connectWallet,
  getETHBalance,
  getFTKBalance,
  sendFTKTransfer,
  isMetaMaskAvailable,
  isValidAddress,
  getEtherscanTxUrl,
  getEtherscanAddressUrl,
  getEtherscanTokenUrl,
  getTokenInfo,
  getNetworkInfo,
  switchToSepolia,
  estimateTransferGas,
  waitForTransaction,
  DEMO_WALLET,
} from './crypto';

// Mock the ethers module
jest.mock('ethers', () => {
  const mockProvider = {
    getBalance: jest.fn(),
    getSigner: jest.fn(),
    getNetwork: jest.fn(),
    send: jest.fn(),
    waitForTransaction: jest.fn(),
    getFeeData: jest.fn(),
  };

  const mockSigner = {
    getAddress: jest.fn(),
    provider: mockProvider,
  };

  const mockContract = {
    balanceOf: jest.fn(),
    transfer: jest.fn(),
    name: jest.fn(),
    symbol: jest.fn(),
    decimals: jest.fn(),
  };

  return {
    BrowserProvider: jest.fn(() => mockProvider),
    JsonRpcProvider: jest.fn(() => mockProvider),
    Contract: jest.fn(() => mockContract),
    formatUnits: jest.fn((value, decimals) => {
      if (typeof value === 'bigint' || typeof value === 'string') {
        return (BigInt(value) / BigInt(10 ** decimals)).toString();
      }
      return '0';
    }),
    parseUnits: jest.fn((value, decimals) => {
      return (BigInt(Math.floor(parseFloat(value) * (10 ** decimals)))).toString();
    }),
    isAddress: jest.fn((address) => {
      return typeof address === 'string' && address.startsWith('0x') && address.length === 42;
    }),
  };
});

describe('crypto.ts', () => {
  let ethers: any;
  let mockProvider: any;
  let mockSigner: any;
  let mockContract: any;

  beforeEach(() => {
    jest.clearAllMocks();

    // Get references to mocked ethers module
    ethers = require('ethers');

    // Get mock instances that will be returned by constructors
    mockProvider = new ethers.BrowserProvider();
    mockSigner = {
      getAddress: jest.fn(),
      provider: mockProvider,
    };
    mockContract = new ethers.Contract();

    // Reset mock implementations
    mockProvider.getBalance = jest.fn();
    mockProvider.getSigner = jest.fn();
    mockProvider.getNetwork = jest.fn();
    mockProvider.send = jest.fn();
    mockProvider.waitForTransaction = jest.fn();
    mockProvider.getFeeData = jest.fn();

    mockSigner.getAddress = jest.fn();

    mockContract.balanceOf = jest.fn();
    mockContract.transfer = jest.fn();
    mockContract.name = jest.fn();
    mockContract.symbol = jest.fn();
    mockContract.decimals = jest.fn();
  });

  afterEach(() => {
    delete (window as any).ethereum;
    jest.resetModules();
  });

  describe('Environment Variables', () => {
    it('should have SEPOLIA_RPC defined', () => {
      expect(SEPOLIA_RPC).toBe('https://sepolia.infura.io/v3/test');
    });

    it('should have FTK_TOKEN_ADDRESS defined', () => {
      expect(FTK_TOKEN_ADDRESS).toBe('0x1234567890123456789012345678901234567890');
    });
  });

  describe('connectWallet', () => {
    it('should throw error if MetaMask not installed', async () => {
      delete (window as any).ethereum;

      await expect(connectWallet()).rejects.toThrow('MetaMask not installed');
    });

    it('should create BrowserProvider with ethereum object', async () => {
      (window as any).ethereum = {};
      mockProvider.getSigner.mockResolvedValueOnce(mockSigner);
      mockProvider.send.mockResolvedValueOnce(null);
      mockProvider.getNetwork.mockResolvedValueOnce({ chainId: BigInt(11155111) });
      mockSigner.getAddress.mockResolvedValueOnce('0x123');

      await connectWallet();

      expect(ethers.BrowserProvider).toHaveBeenCalledWith((window as any).ethereum);
    });

    it('should get signer from provider', async () => {
      (window as any).ethereum = {};
      mockProvider.getSigner.mockResolvedValueOnce(mockSigner);
      mockProvider.send.mockResolvedValueOnce(null);
      mockProvider.getNetwork.mockResolvedValueOnce({ chainId: BigInt(11155111) });
      mockSigner.getAddress.mockResolvedValueOnce('0x123');

      await connectWallet();

      expect(mockProvider.getSigner).toHaveBeenCalled();
    });

    it('should return provider and signer', async () => {
      (window as any).ethereum = {};
      mockProvider.getSigner.mockResolvedValueOnce(mockSigner);
      mockProvider.send.mockResolvedValueOnce(null);
      mockProvider.getNetwork.mockResolvedValueOnce({ chainId: BigInt(11155111) });
      mockSigner.getAddress.mockResolvedValueOnce('0x123');

      const result = await connectWallet();

      expect(result).toHaveProperty('provider');
      expect(result).toHaveProperty('signer');
      expect(result).toHaveProperty('address');
    });

    it('should handle MetaMask errors', async () => {
      (window as any).ethereum = {};
      mockProvider.send.mockRejectedValueOnce(new Error('User denied access'));

      await expect(connectWallet()).rejects.toThrow('User denied access');
    });
  });

  describe('getETHBalance', () => {
    beforeEach(() => {
      (window as any).ethereum = {}; // Set ethereum for getProvider to work
    });

    it('should create JsonRpcProvider with SEPOLIA_RPC when no MetaMask', async () => {
      delete (window as any).ethereum;
      mockProvider.getBalance.mockResolvedValueOnce('1000000000000000000'); // 1 ETH

      await getETHBalance('0x1234567890123456789012345678901234567890');

      expect(ethers.JsonRpcProvider).toHaveBeenCalledWith(SEPOLIA_RPC);
    });

    it('should get balance for address', async () => {
      mockProvider.getBalance.mockResolvedValueOnce('1000000000000000000'); // 1 ETH

      await getETHBalance('0x1234567890123456789012345678901234567890');

      expect(mockProvider.getBalance).toHaveBeenCalledWith('0x1234567890123456789012345678901234567890');
    });

    it('should format balance with 18 decimals', async () => {
      mockProvider.getBalance.mockResolvedValueOnce('1000000000000000000'); // 1 ETH

      await getETHBalance('0x1234567890123456789012345678901234567890');

      expect(ethers.formatUnits).toHaveBeenCalledWith(
        '1000000000000000000',
        18
      );
    });

    it('should return formatted balance as string', async () => {
      mockProvider.getBalance.mockResolvedValueOnce('1000000000000000000'); // 1 ETH

      const balance = await getETHBalance('0x1234567890123456789012345678901234567890');

      expect(typeof balance).toBe('string');
      expect(balance).toBe('1');
    });

    it('should handle multiple addresses independently', async () => {
      mockProvider.getBalance
        .mockResolvedValueOnce('1000000000000000000') // 1 ETH
        .mockResolvedValueOnce('2000000000000000000'); // 2 ETH

      const balance1 = await getETHBalance('0x1234567890123456789012345678901234567890');
      const balance2 = await getETHBalance('0x1234567890123456789012345678901234567891');

      expect(balance1).toBe('1');
      expect(balance2).toBe('2');
    });
  });

  describe('getFTKBalance', () => {
    beforeEach(() => {
      (window as any).ethereum = {}; // Set ethereum for getProvider to work
    });

    it('should create JsonRpcProvider when no MetaMask', async () => {
      delete (window as any).ethereum;
      mockContract.balanceOf.mockResolvedValueOnce('1000000000000000000'); // 1 FTK

      await getFTKBalance('0x1234567890123456789012345678901234567890');

      expect(ethers.JsonRpcProvider).toHaveBeenCalled();
    });

    it('should create Contract with correct parameters', async () => {
      mockContract.balanceOf.mockResolvedValueOnce('1000000000000000000'); // 1 FTK

      await getFTKBalance('0x1234567890123456789012345678901234567890');

      expect(ethers.Contract).toHaveBeenCalledWith(
        FTK_TOKEN_ADDRESS,
        expect.any(Array),
        expect.anything()
      );
    });

    it('should call balanceOf with correct address', async () => {
      mockContract.balanceOf.mockResolvedValueOnce('1000000000000000000'); // 1 FTK

      await getFTKBalance('0x1234567890123456789012345678901234567890');

      expect(mockContract.balanceOf).toHaveBeenCalledWith('0x1234567890123456789012345678901234567890');
    });

    it('should format balance with 18 decimals', async () => {
      mockContract.balanceOf.mockResolvedValueOnce('1000000000000000000'); // 1 FTK

      await getFTKBalance('0x1234567890123456789012345678901234567890');

      expect(ethers.formatUnits).toHaveBeenCalledWith(
        '1000000000000000000',
        18
      );
    });

    it('should return formatted balance as string', async () => {
      mockContract.balanceOf.mockResolvedValueOnce('1000000000000000000'); // 1 FTK

      const balance = await getFTKBalance('0x1234567890123456789012345678901234567890');

      expect(typeof balance).toBe('string');
      expect(balance).toBe('1');
    });

    it('should handle zero balance', async () => {
      mockContract.balanceOf.mockResolvedValueOnce('0');

      const balance = await getFTKBalance('0x1234567890123456789012345678901234567890');

      expect(balance).toBe('0');
    });
  });

  describe('sendFTKTransfer', () => {
    it('should create Contract with signer', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValueOnce({ hash: '0xtxhash' });

      await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(ethers.Contract).toHaveBeenCalledWith(
        FTK_TOKEN_ADDRESS,
        expect.any(Array),
        mockSigner
      );
    });

    it('should parse amount with 18 decimals', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValueOnce({ hash: '0xtxhash' });

      await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1.5');

      expect(ethers.parseUnits).toHaveBeenCalledWith('1.5', 18);
    });

    it('should call transfer with recipient and amount', async () => {
      const txHash = '0x1234567890abcdef';
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValueOnce({ hash: txHash });

      await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(mockContract.transfer).toHaveBeenCalledWith(
        '0x1234567890123456789012345678901234567890',
        expect.any(String)
      );
    });

    it('should return transaction hash', async () => {
      const txHash = '0x1234567890abcdef';
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValueOnce({ hash: txHash });

      const result = await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(result.hash).toBe(txHash);
    });

    it('should handle large amounts', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValueOnce({ hash: '0xtxhash' });

      const result = await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1000000');

      expect(mockContract.transfer).toHaveBeenCalled();
      expect(result.hash).toBe('0xtxhash');
    });

    it('should handle decimal amounts', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValueOnce({ hash: '0xtxhash' });

      await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '0.5');

      expect(ethers.parseUnits).toHaveBeenCalledWith('0.5', 18);
    });

    it('should propagate contract errors', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockRejectedValueOnce(new Error('Insufficient balance'));

      await expect(
        sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1000000')
      ).rejects.toThrow('Insufficient balance');
    });

    it('should handle zero amount transfer', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValueOnce({ hash: '0xtxhash' });

      const result = await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '0');

      expect(ethers.parseUnits).toHaveBeenCalledWith('0', 18);
      expect(result.hash).toBe('0xtxhash');
    });
  });

  describe('isMetaMaskAvailable', () => {
    it('should return true when ethereum object exists', () => {
      (window as any).ethereum = {};
      expect(isMetaMaskAvailable()).toBe(true);
    });

    it('should return false when ethereum object missing', () => {
      delete (window as any).ethereum;
      expect(isMetaMaskAvailable()).toBe(false);
    });
  });

  describe('isValidAddress', () => {
    it('should return true for valid address', () => {
      expect(isValidAddress('0x1234567890123456789012345678901234567890')).toBe(true);
    });

    it('should return false for invalid address', () => {
      expect(isValidAddress('invalid')).toBe(false);
    });

    it('should return false for empty string', () => {
      expect(isValidAddress('')).toBe(false);
    });

    it('should return false for short address', () => {
      expect(isValidAddress('0x123')).toBe(false);
    });
  });

  describe('URL Helper Functions', () => {
    describe('getEtherscanTxUrl', () => {
      it('should return correct transaction URL', () => {
        const txHash = '0xabc123';
        const url = getEtherscanTxUrl(txHash);
        expect(url).toBe(`${ETHERSCAN_BASE_URL}/tx/${txHash}`);
      });

      it('should handle different transaction hashes', () => {
        const txHash = '0x123456789abcdef';
        const url = getEtherscanTxUrl(txHash);
        expect(url).toContain(txHash);
      });
    });

    describe('getEtherscanAddressUrl', () => {
      it('should return correct address URL', () => {
        const address = '0x1234567890123456789012345678901234567890';
        const url = getEtherscanAddressUrl(address);
        expect(url).toBe(`${ETHERSCAN_BASE_URL}/address/${address}`);
      });

      it('should handle different addresses', () => {
        const address = '0xabcdef';
        const url = getEtherscanAddressUrl(address);
        expect(url).toContain(address);
      });
    });

    describe('getEtherscanTokenUrl', () => {
      it('should return correct token URL', () => {
        const url = getEtherscanTokenUrl();
        expect(url).toBe(`${ETHERSCAN_BASE_URL}/token/${FTK_TOKEN_ADDRESS}`);
      });

      it('should include token address', () => {
        const url = getEtherscanTokenUrl();
        expect(url).toContain(FTK_TOKEN_ADDRESS);
      });
    });
  });

  describe('getTokenInfo', () => {
    beforeEach(() => {
      (window as any).ethereum = {};
    });

    it('should fetch token name, symbol, and decimals', async () => {
      mockContract.name.mockResolvedValue('FinTech Token');
      mockContract.symbol.mockResolvedValue('FTK');
      mockContract.decimals.mockResolvedValue(18);

      const info = await getTokenInfo();

      expect(info.name).toBe('FinTech Token');
      expect(info.symbol).toBe('FTK');
      expect(info.decimals).toBe(18);
    });

    it('should convert decimals to number', async () => {
      mockContract.name.mockResolvedValue('Token');
      mockContract.symbol.mockResolvedValue('TKN');
      mockContract.decimals.mockResolvedValue(BigInt(18));

      const info = await getTokenInfo();

      expect(typeof info.decimals).toBe('number');
      expect(info.decimals).toBe(18);
    });

    it('should call all token methods in parallel', async () => {
      mockContract.name.mockResolvedValue('Token');
      mockContract.symbol.mockResolvedValue('TKN');
      mockContract.decimals.mockResolvedValue(18);

      await getTokenInfo();

      expect(mockContract.name).toHaveBeenCalled();
      expect(mockContract.symbol).toHaveBeenCalled();
      expect(mockContract.decimals).toHaveBeenCalled();
    });
  });

  describe('getNetworkInfo', () => {
    it('should throw error if MetaMask not installed', async () => {
      delete (window as any).ethereum;

      const { getNetworkInfo } = require('./crypto');
      await expect(getNetworkInfo()).rejects.toThrow('MetaMask not installed');
    });

    it('should return network info for Sepolia', async () => {
      (window as any).ethereum = {};
      mockProvider.getNetwork.mockResolvedValue({ chainId: BigInt(11155111) });

      const { getNetworkInfo } = require('./crypto');
      const info = await getNetworkInfo();

      expect(info.chainId).toBe(11155111);
      expect(info.name).toBe('Sepolia Testnet');
      expect(info.isCorrectNetwork).toBe(true);
    });

    it('should detect wrong network', async () => {
      (window as any).ethereum = {};
      mockProvider.getNetwork.mockResolvedValue({ chainId: BigInt(1) });

      const { getNetworkInfo } = require('./crypto');
      const info = await getNetworkInfo();

      expect(info.chainId).toBe(1);
      expect(info.isCorrectNetwork).toBe(false);
    });

    it('should show unknown for unsupported networks', async () => {
      (window as any).ethereum = {};
      mockProvider.getNetwork.mockResolvedValue({ chainId: BigInt(999) });

      const { getNetworkInfo } = require('./crypto');
      const info = await getNetworkInfo();

      expect(info.name).toContain('Unknown');
      expect(info.name).toContain('999');
    });
  });

  describe('switchToSepolia', () => {
    it('should throw error if MetaMask not installed', async () => {
      delete (window as any).ethereum;

      const { switchToSepolia } = require('./crypto');
      await expect(switchToSepolia()).rejects.toThrow('MetaMask not installed');
    });

    it('should call wallet_switchEthereumChain', async () => {
      (window as any).ethereum = {
        request: jest.fn().mockResolvedValue(null)
      };

      const { switchToSepolia } = require('./crypto');
      await switchToSepolia();

      expect((window as any).ethereum.request).toHaveBeenCalledWith({
        method: 'wallet_switchEthereumChain',
        params: [{ chainId: '0xaa36a7' }]
      });
    });

    it('should add chain if not found (error 4902)', async () => {
      const error = { code: 4902 };
      (window as any).ethereum = {
        request: jest.fn()
          .mockRejectedValueOnce(error)
          .mockResolvedValueOnce(null)
      };

      const { switchToSepolia } = require('./crypto');
      await switchToSepolia();

      expect((window as any).ethereum.request).toHaveBeenCalledWith({
        method: 'wallet_addEthereumChain',
        params: expect.any(Array)
      });
    });

    it('should rethrow other errors', async () => {
      const error = { code: 1234, message: 'User rejected' };
      (window as any).ethereum = {
        request: jest.fn().mockRejectedValueOnce(error)
      };

      const { switchToSepolia } = require('./crypto');
      await expect(switchToSepolia()).rejects.toEqual(error);
    });
  });

  describe('estimateTransferGas', () => {
    it('should throw error if FTK_TOKEN_ADDRESS missing', async () => {
      const originalAddress = process.env.REACT_APP_FTK_ADDRESS;
      delete (process.env as any).REACT_APP_FTK_ADDRESS;

      const { estimateTransferGas } = require('./crypto');
      await expect(estimateTransferGas(mockSigner, '0x123', '1')).rejects.toThrow(
        'FTK token address not configured'
      );

      process.env.REACT_APP_FTK_ADDRESS = originalAddress;
    });

    it('should throw error for invalid recipient address', async () => {
      const { estimateTransferGas } = require('./crypto');
      await expect(estimateTransferGas(mockSigner, 'invalid', '1')).rejects.toThrow(
        'Invalid recipient address'
      );
    });

    it('should estimate gas limit', async () => {
      mockContract.transfer = {
        estimateGas: jest.fn().mockResolvedValue(BigInt(50000))
      };
      mockSigner.provider = {
        getFeeData: jest.fn().mockResolvedValue({
          gasPrice: BigInt(20000000000)
        })
      };

      const { estimateTransferGas } = require('./crypto');
      const estimate = await estimateTransferGas(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(estimate.gasLimit).toBe(BigInt(50000));
    });

    it('should calculate estimated cost', async () => {
      mockContract.transfer = {
        estimateGas: jest.fn().mockResolvedValue(BigInt(50000))
      };
      mockSigner.provider = {
        getFeeData: jest.fn().mockResolvedValue({
          gasPrice: BigInt(20000000000)
        })
      };

      const { estimateTransferGas } = require('./crypto');
      const estimate = await estimateTransferGas(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(estimate.estimatedCostWei).toBe(BigInt(50000) * BigInt(20000000000));
    });

    it('should return ETH formatted cost', async () => {
      mockContract.transfer = {
        estimateGas: jest.fn().mockResolvedValue(BigInt(50000))
      };
      mockSigner.provider = {
        getFeeData: jest.fn().mockResolvedValue({
          gasPrice: BigInt(20000000000)
        })
      };

      const { estimateTransferGas } = require('./crypto');
      const estimate = await estimateTransferGas(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(typeof estimate.estimatedCostEth).toBe('string');
    });
  });

  describe('waitForTransaction', () => {
    it('should wait for transaction with default confirmations', async () => {
      const receipt = {
        from: '0xfrom',
        to: '0xto',
        gasUsed: BigInt(21000),
        blockNumber: 12345,
        status: 1
      };
      mockProvider.waitForTransaction.mockResolvedValue(receipt);

      const { waitForTransaction } = require('./crypto');
      const result = await waitForTransaction(mockProvider, '0xtxhash');

      expect(mockProvider.waitForTransaction).toHaveBeenCalledWith('0xtxhash', 1);
      expect(result.status).toBe('confirmed');
    });

    it('should handle custom confirmations', async () => {
      const receipt = {
        from: '0xfrom',
        to: '0xto',
        gasUsed: BigInt(21000),
        blockNumber: 12345,
        status: 1
      };
      mockProvider.waitForTransaction.mockResolvedValue(receipt);

      const { waitForTransaction } = require('./crypto');
      await waitForTransaction(mockProvider, '0xtxhash', 3);

      expect(mockProvider.waitForTransaction).toHaveBeenCalledWith('0xtxhash', 3);
    });

    it('should return failed status for failed transactions', async () => {
      const receipt = {
        from: '0xfrom',
        to: '0xto',
        gasUsed: BigInt(21000),
        blockNumber: 12345,
        status: 0
      };
      mockProvider.waitForTransaction.mockResolvedValue(receipt);

      const { waitForTransaction } = require('./crypto');
      const result = await waitForTransaction(mockProvider, '0xtxhash');

      expect(result.status).toBe('failed');
    });

    it('should include receipt in result', async () => {
      const receipt = {
        from: '0xfrom',
        to: '0xto',
        gasUsed: BigInt(21000),
        blockNumber: 12345,
        status: 1
      };
      mockProvider.waitForTransaction.mockResolvedValue(receipt);

      const { waitForTransaction } = require('./crypto');
      const result = await waitForTransaction(mockProvider, '0xtxhash');

      expect(result.receipt).toEqual(receipt);
    });

    it('should include gas used as string', async () => {
      const receipt = {
        from: '0xfrom',
        to: '0xto',
        gasUsed: BigInt(21000),
        blockNumber: 12345,
        status: 1
      };
      mockProvider.waitForTransaction.mockResolvedValue(receipt);

      const { waitForTransaction } = require('./crypto');
      const result = await waitForTransaction(mockProvider, '0xtxhash');

      expect(result.gasUsed).toBe('21000');
    });

    it('should include block number', async () => {
      const receipt = {
        from: '0xfrom',
        to: '0xto',
        gasUsed: BigInt(21000),
        blockNumber: 99999,
        status: 1
      };
      mockProvider.waitForTransaction.mockResolvedValue(receipt);

      const { waitForTransaction } = require('./crypto');
      const result = await waitForTransaction(mockProvider, '0xtxhash');

      expect(result.blockNumber).toBe(99999);
    });

    it('should create Etherscan URL', async () => {
      const receipt = {
        from: '0xfrom',
        to: '0xto',
        gasUsed: BigInt(21000),
        blockNumber: 12345,
        status: 1
      };
      mockProvider.waitForTransaction.mockResolvedValue(receipt);

      const { waitForTransaction } = require('./crypto');
      const result = await waitForTransaction(mockProvider, '0xtxhash');

      expect(result.etherscanUrl).toContain('0xtxhash');
    });

    it('should handle missing to address', async () => {
      const receipt = {
        from: '0xfrom',
        to: null,
        gasUsed: BigInt(21000),
        blockNumber: 12345,
        status: 1
      };
      mockProvider.waitForTransaction.mockResolvedValue(receipt);

      const { waitForTransaction } = require('./crypto');
      const result = await waitForTransaction(mockProvider, '0xtxhash');

      expect(result.to).toBe('');
    });
  });

  describe('sendFTKTransfer', () => {
    it('should return transaction result with pending status', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValue({
        hash: '0xtxhash'
      });

      const { sendFTKTransfer } = require('./crypto');
      const result = await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(result.status).toBe('pending');
      expect(result.hash).toBe('0xtxhash');
    });

    it('should throw error for invalid recipient address', async () => {
      const { sendFTKTransfer } = require('./crypto');
      await expect(sendFTKTransfer(mockSigner, 'invalid', '1')).rejects.toThrow(
        'Invalid recipient address'
      );
    });

    it('should include from address', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfromaddress');
      mockContract.transfer.mockResolvedValue({
        hash: '0xtxhash'
      });

      const { sendFTKTransfer } = require('./crypto');
      const result = await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(result.from).toBe('0xfromaddress');
    });

    it('should include amount in result', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValue({
        hash: '0xtxhash'
      });

      const { sendFTKTransfer } = require('./crypto');
      const result = await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '123.45');

      expect(result.amount).toBe('123.45');
    });

    it('should include etherscan URL', async () => {
      mockSigner.getAddress.mockResolvedValue('0xfrom');
      mockContract.transfer.mockResolvedValue({
        hash: '0xtxhash'
      });

      const { sendFTKTransfer } = require('./crypto');
      const result = await sendFTKTransfer(mockSigner, '0x1234567890123456789012345678901234567890', '1');

      expect(result.etherscanUrl).toContain('0xtxhash');
    });
  });

  describe('getETHBalance validation', () => {
    it('should throw error for invalid address', async () => {
      await expect(getETHBalance('invalid')).rejects.toThrow('Invalid Ethereum address');
    });

    it('should accept valid addresses', async () => {
      mockProvider.getBalance.mockResolvedValue('1000000000000000000');
      const balance = await getETHBalance('0x1234567890123456789012345678901234567890');
      expect(balance).toBeDefined();
    });
  });

  describe('getFTKBalance validation', () => {
    it('should throw error for invalid address', async () => {
      await expect(getFTKBalance('invalid')).rejects.toThrow('Invalid Ethereum address');
    });

    it('should accept valid addresses', async () => {
      mockContract.balanceOf.mockResolvedValue('1000000000000000000');
      const balance = await getFTKBalance('0x1234567890123456789012345678901234567890');
      expect(balance).toBeDefined();
    });

    it('should throw error if FTK_TOKEN_ADDRESS not configured', async () => {
      const originalAddress = process.env.REACT_APP_FTK_ADDRESS;
      delete (process.env as any).REACT_APP_FTK_ADDRESS;

      await expect(getFTKBalance('0x1234567890123456789012345678901234567890')).rejects.toThrow(
        'FTK token address not configured'
      );

      process.env.REACT_APP_FTK_ADDRESS = originalAddress;
    });
  });

  describe('DEMO_WALLET', () => {
    it('should have demo wallet configuration', () => {
      const { DEMO_WALLET } = require('./crypto');
      expect(DEMO_WALLET).toBeDefined();
      expect(DEMO_WALLET.address).toBeDefined();
      expect(DEMO_WALLET.ethBalance).toBeDefined();
      expect(DEMO_WALLET.ftkBalance).toBeDefined();
      expect(DEMO_WALLET.isDemo).toBe(true);
    });
  });
});
