import { BrowserProvider, JsonRpcProvider, Contract, formatUnits, parseUnits, isAddress } from 'ethers';
import type { TransactionReceipt, TransactionResponse } from 'ethers';

// Environment configuration for Sepolia testnet
export const SEPOLIA_RPC = process.env.REACT_APP_SEPOLIA_RPC as string;
export const FTK_TOKEN_ADDRESS = process.env.REACT_APP_FTK_ADDRESS as string;

// Sepolia network configuration
export const SEPOLIA_CHAIN_ID = 11155111;
export const SEPOLIA_CHAIN_ID_HEX = '0xaa36a7';
export const ETHERSCAN_BASE_URL = 'https://sepolia.etherscan.io';

// ERC-20 token standard ABI (minimal interface for transfers)
const ERC20_ABI = [
  'function name() view returns (string)',
  'function symbol() view returns (string)',
  'function decimals() view returns (uint8)',
  'function balanceOf(address owner) view returns (uint256)',
  'function transfer(address to, uint256 amount) returns (bool)',
  'function allowance(address owner, address spender) view returns (uint256)',
  'event Transfer(address indexed from, address indexed to, uint256 value)',
];

export interface TransactionResult {
  hash: string;
  etherscanUrl: string;
  from: string;
  to: string;
  amount: string;
  gasUsed?: string;
  blockNumber?: number;
  status: 'pending' | 'confirmed' | 'failed';
}

export interface GasEstimate {
  gasLimit: bigint;
  gasPrice: bigint;
  estimatedCostWei: bigint;
  estimatedCostEth: string;
}

export interface NetworkInfo {
  chainId: number;
  name: string;
  isCorrectNetwork: boolean;
}

// Demo mode configuration for users without MetaMask
export const DEMO_WALLET = {
  address: '0xDEMO0000000000000000000000000000000000',
  ethBalance: '0.1976',
  ftkBalance: '999999.99',
  isDemo: true
};

/**
 * Checks if MetaMask is available in the browser
 */
export function isMetaMaskAvailable(): boolean {
  return typeof window !== 'undefined' && !!(window as any).ethereum;
}

/**
 * Validates if a string is a valid Ethereum address
 */
export function isValidAddress(address: string): boolean {
  return isAddress(address);
}

/**
 * Gets Etherscan URL for a transaction hash
 */
export function getEtherscanTxUrl(txHash: string): string {
  return `${ETHERSCAN_BASE_URL}/tx/${txHash}`;
}

/**
 * Gets Etherscan URL for an address
 */
export function getEtherscanAddressUrl(address: string): string {
  return `${ETHERSCAN_BASE_URL}/address/${address}`;
}

/**
 * Gets Etherscan URL for the FTK token contract
 */
export function getEtherscanTokenUrl(): string {
  return `${ETHERSCAN_BASE_URL}/token/${FTK_TOKEN_ADDRESS}`;
}

/**
 * Gets a provider - uses MetaMask's injected provider to avoid CSP issues
 * Falls back to JsonRpcProvider only if MetaMask is not available
 */
function getProvider(): BrowserProvider | JsonRpcProvider {
  // Prefer MetaMask's injected provider (no CSP issues)
  if (typeof window !== 'undefined' && (window as any).ethereum) {
    return new BrowserProvider((window as any).ethereum);
  }
  // Fallback to RPC (may have CSP issues in browser)
  if (!SEPOLIA_RPC) throw new Error('No provider available. Please install MetaMask.');
  return new JsonRpcProvider(SEPOLIA_RPC);
}

/**
 * Gets current network info from MetaMask
 */
export async function getNetworkInfo(): Promise<NetworkInfo> {
  if (typeof window === 'undefined' || !(window as any).ethereum) {
    throw new Error('MetaMask not installed');
  }

  const provider = new BrowserProvider((window as any).ethereum);
  const network = await provider.getNetwork();
  const chainId = Number(network.chainId);

  return {
    chainId,
    name: chainId === SEPOLIA_CHAIN_ID ? 'Sepolia Testnet' : `Unknown (${chainId})`,
    isCorrectNetwork: chainId === SEPOLIA_CHAIN_ID
  };
}

/**
 * Switches MetaMask to Sepolia network
 */
export async function switchToSepolia(): Promise<void> {
  if (typeof window === 'undefined' || !(window as any).ethereum) {
    throw new Error('MetaMask not installed');
  }

  try {
    await (window as any).ethereum.request({
      method: 'wallet_switchEthereumChain',
      params: [{ chainId: SEPOLIA_CHAIN_ID_HEX }],
    });
  } catch (switchError: any) {
    // Chain not added to MetaMask - add it
    if (switchError.code === 4902) {
      await (window as any).ethereum.request({
        method: 'wallet_addEthereumChain',
        params: [{
          chainId: SEPOLIA_CHAIN_ID_HEX,
          chainName: 'Sepolia Testnet',
          nativeCurrency: { name: 'SepoliaETH', symbol: 'ETH', decimals: 18 },
          rpcUrls: ['https://sepolia.infura.io/v3/'],
          blockExplorerUrls: [ETHERSCAN_BASE_URL],
        }],
      });
    } else {
      throw switchError;
    }
  }
}

/**
 * Connects to MetaMask wallet with network validation
 */
export async function connectWallet(): Promise<{ provider: BrowserProvider; signer: any; address: string }> {
  if (typeof window === 'undefined' || !(window as any).ethereum) {
    throw new Error('MetaMask not installed. Please install MetaMask to use crypto features.');
  }

  const provider = new BrowserProvider((window as any).ethereum);

  // Request account access
  await provider.send('eth_requestAccounts', []);

  // Verify network
  const networkInfo = await getNetworkInfo();
  if (!networkInfo.isCorrectNetwork) {
    await switchToSepolia();
  }

  const signer = await provider.getSigner();
  const address = await signer.getAddress();

  return { provider, signer, address };
}

/**
 * Gets ETH balance for an address on Sepolia
 */
export async function getETHBalance(address: string): Promise<string> {
  if (!isValidAddress(address)) throw new Error('Invalid Ethereum address');

  const provider = getProvider();
  const balance = await provider.getBalance(address);
  return formatUnits(balance, 18);
}

/**
 * Gets FTK token balance for an address
 */
export async function getFTKBalance(address: string): Promise<string> {
  if (!isValidAddress(address)) throw new Error('Invalid Ethereum address');
  if (!FTK_TOKEN_ADDRESS) throw new Error('FTK token address not configured');

  const provider = getProvider();
  const contract = new Contract(FTK_TOKEN_ADDRESS, ERC20_ABI, provider);
  const balance = await contract.balanceOf(address);
  return formatUnits(balance, 18);
}

/**
 * Gets token metadata (name, symbol, decimals)
 */
export async function getTokenInfo(): Promise<{ name: string; symbol: string; decimals: number }> {
  if (!FTK_TOKEN_ADDRESS) throw new Error('FTK token address not configured');

  const provider = getProvider();
  const contract = new Contract(FTK_TOKEN_ADDRESS, ERC20_ABI, provider);

  const [name, symbol, decimals] = await Promise.all([
    contract.name(),
    contract.symbol(),
    contract.decimals()
  ]);

  return { name, symbol, decimals: Number(decimals) };
}

/**
 * Estimates gas for FTK transfer
 */
export async function estimateTransferGas(
  signer: any,
  toAddress: string,
  amount: string
): Promise<GasEstimate> {
  if (!FTK_TOKEN_ADDRESS) throw new Error('FTK token address not configured');
  if (!isValidAddress(toAddress)) throw new Error('Invalid recipient address');

  const contract = new Contract(FTK_TOKEN_ADDRESS, ERC20_ABI, signer);
  const amountWei = parseUnits(amount, 18);

  const gasLimit = await contract.transfer.estimateGas(toAddress, amountWei);
  const feeData = await signer.provider.getFeeData();
  const gasPrice = feeData.gasPrice ?? BigInt(0);
  const estimatedCostWei = gasLimit * gasPrice;

  return {
    gasLimit,
    gasPrice,
    estimatedCostWei,
    estimatedCostEth: formatUnits(estimatedCostWei, 18)
  };
}

/**
 * Sends FTK tokens to a recipient address on Sepolia testnet
 * Returns transaction details including Etherscan link for verification
 */
export async function sendFTKTransfer(
  signer: any,
  toAddress: string,
  amount: string
): Promise<TransactionResult> {
  if (!FTK_TOKEN_ADDRESS) throw new Error('FTK token address not configured');
  if (!isValidAddress(toAddress)) throw new Error('Invalid recipient address');

  const fromAddress = await signer.getAddress();
  const contract = new Contract(FTK_TOKEN_ADDRESS, ERC20_ABI, signer);
  const amountWei = parseUnits(amount, 18);

  // Send transaction
  const tx: TransactionResponse = await contract.transfer(toAddress, amountWei);

  const result: TransactionResult = {
    hash: tx.hash,
    etherscanUrl: getEtherscanTxUrl(tx.hash),
    from: fromAddress,
    to: toAddress,
    amount,
    status: 'pending'
  };

  return result;
}

/**
 * Waits for transaction confirmation and returns updated result
 */
export async function waitForTransaction(
  provider: any,
  txHash: string,
  confirmations: number = 1
): Promise<TransactionResult & { receipt: TransactionReceipt }> {
  const receipt = await provider.waitForTransaction(txHash, confirmations);

  return {
    hash: txHash,
    etherscanUrl: getEtherscanTxUrl(txHash),
    from: receipt.from,
    to: receipt.to ?? '',
    amount: '',
    gasUsed: receipt.gasUsed.toString(),
    blockNumber: receipt.blockNumber,
    status: receipt.status === 1 ? 'confirmed' : 'failed',
    receipt
  };
}

/**
 * Gets recent FTK transfer events for an address
 */
export async function getRecentTransfers(address: string, blockCount: number = 1000): Promise<Array<{
  txHash: string;
  from: string;
  to: string;
  amount: string;
  blockNumber: number;
  etherscanUrl: string;
}>> {
  if (!FTK_TOKEN_ADDRESS) throw new Error('FTK token address not configured');
  if (!isValidAddress(address)) throw new Error('Invalid address');

  const provider = getProvider();
  const contract = new Contract(FTK_TOKEN_ADDRESS, ERC20_ABI, provider);

  const currentBlock = await provider.getBlockNumber();
  const fromBlock = Math.max(0, currentBlock - blockCount);

  // Query transfer events where address is sender or receiver
  const filterFrom = contract.filters.Transfer(address, null);
  const filterTo = contract.filters.Transfer(null, address);

  const [eventsFrom, eventsTo] = await Promise.all([
    contract.queryFilter(filterFrom, fromBlock, currentBlock),
    contract.queryFilter(filterTo, fromBlock, currentBlock)
  ]);

  const allEvents = [...eventsFrom, ...eventsTo]
    .sort((a, b) => (b.blockNumber ?? 0) - (a.blockNumber ?? 0))
    .slice(0, 20);

  return allEvents.map(event => ({
    txHash: event.transactionHash,
    from: (event as any).args[0],
    to: (event as any).args[1],
    amount: formatUnits((event as any).args[2], 18),
    blockNumber: event.blockNumber ?? 0,
    etherscanUrl: getEtherscanTxUrl(event.transactionHash)
  }));
}
