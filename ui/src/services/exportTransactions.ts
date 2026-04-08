import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';

export interface ExportableTransaction {
  id: string;
  accountNumber: string;
  amount: number;
  currency: string;
  type: 'credit' | 'debit';
  description: string;
  createdAt: string;
  spendingType?: string;
  status?: 'Completed' | 'Pending' | 'Failed' | 'Processing';
}

// Branding constants
const PLATFORM_NAME = 'FinTech Open Finance Platform';
const REPORT_TITLE = 'Transaction Statement';
const DISCLAIMER = 'This document is system-generated and does not constitute financial advice.';

const formatDate = (dateString: string): string =>
  new Date(dateString).toLocaleDateString('en-NZ');

const formatDateTime = (date: Date): string =>
  date.toLocaleDateString('en-NZ', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: true,
  });

const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat('en-NZ', { style: 'currency', currency: 'NZD' }).format(amount);

const buildFilename = (extension: string): string => {
  const date = new Date().toISOString().slice(0, 10);
  return `transactions_${date}.${extension}`;
};

const triggerDownload = (blob: Blob, filename: string): void => {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
};

interface TransactionSummary {
  totalCount: number;
  totalCredits: number;
  totalDebits: number;
  netPosition: number;
}

const calculateSummary = (transactions: ExportableTransaction[]): TransactionSummary => {
  const totalCredits = transactions
    .filter((t) => t.type === 'credit')
    .reduce((sum, t) => sum + t.amount, 0);
  const totalDebits = transactions
    .filter((t) => t.type === 'debit')
    .reduce((sum, t) => sum + t.amount, 0);

  return {
    totalCount: transactions.length,
    totalCredits,
    totalDebits,
    netPosition: totalCredits - totalDebits,
  };
};

export const exportCSV = (transactions: ExportableTransaction[]): void => {
  const exportDate = new Date();
  const summary = calculateSummary(transactions);

  // Branded header block
  const headerBlock = [
    PLATFORM_NAME,
    REPORT_TITLE,
    `Generated: ${formatDateTime(exportDate)}`,
    DISCLAIMER,
    '', // Blank line before column headers
  ];

  const headers = ['Date', 'Description', 'Account', 'Type', 'Category', 'Amount'];
  const rows = transactions.map((t) => [
    formatDate(t.createdAt),
    `"${(t.description || '').replace(/"/g, '""')}"`,
    t.accountNumber,
    t.type.toUpperCase(),
    t.spendingType || '',
    `${t.type === 'credit' ? '' : '-'}${formatCurrency(t.amount)}`,
  ]);

  // Summary row at the end
  const summaryRows = [
    '', // Blank line before summary
    `SUMMARY,Total Transactions: ${summary.totalCount},Total Credits: ${formatCurrency(summary.totalCredits)},Total Debits: ${formatCurrency(summary.totalDebits)},Net Position: ${formatCurrency(summary.netPosition)},`,
  ];

  const csv = [
    ...headerBlock,
    headers.join(','),
    ...rows.map((r) => r.join(',')),
    ...summaryRows,
  ].join('\r\n');

  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  triggerDownload(blob, buildFilename('csv'));
};

export const exportPDF = (transactions: ExportableTransaction[]): void => {
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
  const pageWidth = doc.internal.pageSize.getWidth();
  const pageHeight = doc.internal.pageSize.getHeight();
  const exportDate = new Date();
  const summary = calculateSummary(transactions);

  // Cover section - Platform name (large heading)
  doc.setFontSize(22);
  doc.setFont('helvetica', 'bold');
  doc.setTextColor(26, 35, 50);
  doc.text(PLATFORM_NAME, 14, 20);

  // Report title (subheading)
  doc.setFontSize(16);
  doc.setFont('helvetica', 'normal');
  doc.setTextColor(60, 60, 60);
  doc.text(REPORT_TITLE, 14, 30);

  // Export metadata
  doc.setFontSize(10);
  doc.setTextColor(100);
  doc.text(`Generated: ${formatDateTime(exportDate)}`, 14, 40);
  doc.text(`Total Transactions: ${transactions.length}`, 14, 46);
  doc.setTextColor(0);

  // Divider line
  doc.setDrawColor(200);
  doc.setLineWidth(0.5);
  doc.line(14, 52, pageWidth - 14, 52);

  // Transaction table data
  const tableData = transactions.map((t) => [
    formatDate(t.createdAt),
    t.description || '—',
    t.accountNumber,
    t.type.toUpperCase(),
    t.spendingType || '—',
    `${t.type === 'credit' ? '+' : '-'}${formatCurrency(t.amount)}`,
  ]);

  // Summary row
  const summaryRow = [
    'SUMMARY',
    `${summary.totalCount} transactions`,
    '',
    '',
    `Credits: ${formatCurrency(summary.totalCredits)} | Debits: ${formatCurrency(summary.totalDebits)}`,
    `Net: ${formatCurrency(summary.netPosition)}`,
  ];

  autoTable(doc, {
    startY: 58,
    head: [['Date', 'Description', 'Account', 'Type', 'Category', 'Amount']],
    body: [...tableData, summaryRow],
    styles: { fontSize: 8, cellPadding: 3 },
    headStyles: { fillColor: [26, 35, 50], textColor: 255, fontStyle: 'bold' },
    alternateRowStyles: { fillColor: [245, 247, 250] },
    columnStyles: {
      5: { halign: 'right' },
    },
    // Style the summary row
    didParseCell: (data) => {
      if (data.row.index === tableData.length && data.section === 'body') {
        data.cell.styles.fillColor = [26, 35, 50];
        data.cell.styles.textColor = [255, 255, 255];
        data.cell.styles.fontStyle = 'bold';
      }
    },
    margin: { bottom: 22 }, // Reserve space for footer
  });

  // Add branded footer to every page after table is complete
  const pagesObj = (doc as any).internal?.pages;
  const totalPages = typeof (doc as any).getNumberOfPages === 'function'
    ? (doc as any).getNumberOfPages()
    : Array.isArray(pagesObj)
    ? pagesObj.length - (pagesObj[0] == null ? 1 : 0)
    : Object.keys(pagesObj || {}).length - ((pagesObj && pagesObj[0]) ? 0 : 1);

  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i);

    // Footer divider line
    doc.setDrawColor(200);
    doc.setLineWidth(0.3);
    doc.line(14, pageHeight - 15, pageWidth - 14, pageHeight - 15);

    // Footer text
    doc.setFontSize(8);
    doc.setTextColor(100);

    // Left: Platform name
    doc.text(PLATFORM_NAME, 14, pageHeight - 10);

    // Center: Disclaimer
    const disclaimerWidth = doc.getTextWidth(DISCLAIMER);
    doc.text(DISCLAIMER, (pageWidth - disclaimerWidth) / 2, pageHeight - 10);

    // Right: Page number
    const pageText = `Page ${i} of ${totalPages}`;
    const pageTextWidth = doc.getTextWidth(pageText);
    doc.text(pageText, pageWidth - 14 - pageTextWidth, pageHeight - 10);

    doc.setTextColor(0);
  }

  doc.save(buildFilename('pdf'));
};
