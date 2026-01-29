/**
 * Types and interfaces for blockchain/crypto functionality
 */

/**
 * Supported blockchain networks
 */
export type Network = 'sepolia' | 'mainnet' | 'goerli';

/**
 * Network configuration
 */
export interface NetworkConfig {
  chainId: number;
  chainIdHex: string;
  name: string;
  rpcUrl: string;
  blockExplorerUrl: string;
  nativeCurrency: {
    name: string;
    symbol: string;
    decimals: number;
  };
}

/**
 * Sepolia testnet configuration
 */
export const SEPOLIA_CONFIG: NetworkConfig = {
  chainId: 11155111,
  chainIdHex: '0xaa36a7',
  name: 'Sepolia Testnet',
  rpcUrl: 'https://sepolia.infura.io/v3/',
  blockExplorerUrl: 'https://sepolia.etherscan.io',
  nativeCurrency: {
    name: 'SepoliaETH',
    symbol: 'ETH',
    decimals: 18,
  },
};

/**
 * Transaction status
 */
export type TransactionStatus = 'pending' | 'confirming' | 'confirmed' | 'failed';

/**
 * Token transfer event from the blockchain
 */
export interface TokenTransfer {
  txHash: string;
  from: string;
  to: string;
  amount: string;
  blockNumber: number;
  timestamp?: number;
  etherscanUrl: string;
}

/**
 * Wallet connection state
 */
export interface WalletState {
  isConnected: boolean;
  address: string | null;
  chainId: number | null;
  isCorrectNetwork: boolean;
}

/**
 * ERC-20 Token information
 */
export interface TokenInfo {
  address: string;
  name: string;
  symbol: string;
  decimals: number;
  totalSupply?: string;
}

/**
 * Gas estimation result
 */
export interface GasEstimation {
  gasLimit: string;
  gasPrice: string;
  maxFeePerGas?: string;
  maxPriorityFeePerGas?: string;
  estimatedCostWei: string;
  estimatedCostEth: string;
}

/**
 * Transaction receipt from blockchain
 */
export interface BlockchainReceipt {
  transactionHash: string;
  blockNumber: number;
  blockHash: string;
  from: string;
  to: string | null;
  gasUsed: string;
  effectiveGasPrice: string;
  status: 0 | 1; // 0 = failed, 1 = success
  logs: any[];
}

/**
 * Helper to format address for display
 */
export function formatAddress(address: string, chars: number = 6): string {
  if (!address || address.length < 10) return address;
  return `${address.slice(0, chars)}...${address.slice(-chars + 2)}`;
}

/**
 * Helper to format token amount
 */
export function formatTokenAmount(amount: string, decimals: number = 4): string {
  const num = parseFloat(amount);
  if (isNaN(num)) return '0';
  if (num === 0) return '0';
  if (num < Math.pow(10, -decimals)) return `<${Math.pow(10, -decimals)}`;
  return num.toFixed(decimals);
}

/**
 * Helper to get Etherscan URL for various entity types
 */
export function getEtherscanUrl(
  type: 'tx' | 'address' | 'token' | 'block',
  value: string,
  baseUrl: string = SEPOLIA_CONFIG.blockExplorerUrl
): string {
  switch (type) {
    case 'tx':
      return `${baseUrl}/tx/${value}`;
    case 'address':
      return `${baseUrl}/address/${value}`;
    case 'token':
      return `${baseUrl}/token/${value}`;
    case 'block':
      return `${baseUrl}/block/${value}`;
    default:
      return baseUrl;
  }
}
