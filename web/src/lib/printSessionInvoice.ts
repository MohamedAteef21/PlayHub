import type { SessionDetail } from '@/types';
import { PaymentMethod } from '@/types';

export interface InvoicePrintLabels {
  title: string;
  branch: string;
  invoiceNumber: string;
  date: string;
  device: string;
  room: string;
  mode: string;
  gaming: string;
  watching: string;
  plan: string;
  started: string;
  closed: string;
  timeCost: string;
  roomSurcharge: string;
  cafeteria: string;
  discount: string;
  customerWallet: string;
  total: string;
  payment: string;
  cash: string;
  deferred: string;
  bankTransfer: string;
  digitalWallet: string;
  openedBy: string;
  closedBy: string;
  qty: string;
  item: string;
  thankYou: string;
  print: string;
}

function paymentLabel(method: number, labels: InvoicePrintLabels): string {
  switch (method) {
    case PaymentMethod.Cash:
      return labels.cash;
    case PaymentMethod.Deferred:
      return labels.deferred;
    case PaymentMethod.CustomerWallet:
      return labels.customerWallet;
    case PaymentMethod.BankTransfer:
      return labels.bankTransfer;
    case PaymentMethod.DigitalWallet:
      return labels.digitalWallet;
    default:
      return String(method);
  }
}

function money(n: number): string {
  return `${n.toFixed(2)} EGP`;
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function printSessionInvoice(
  detail: SessionDetail,
  branchName: string,
  labels: InvoicePrintLabels,
  dir: 'rtl' | 'ltr' = 'ltr'
): void {
  const invoiceNo = detail.invoice?.invoiceNumber ?? '—';
  const closedAt = detail.closedAt ? new Date(detail.closedAt) : new Date();
  const startedAt = new Date(detail.startedAt);
  const modeLabel = detail.sessionMode === 1 ? labels.gaming : labels.watching;
  const payMethod = paymentLabel(detail.invoice?.paymentMethod ?? PaymentMethod.Cash, labels);

  const linesHtml = detail.cafeteriaLines
    .filter((l) => l.quantity - l.returnedQuantity > 0)
    .map((l) => {
      const qty = l.quantity - l.returnedQuantity;
      return `<tr>
        <td>${escapeHtml(l.itemName)}</td>
        <td style="text-align:center">${qty}</td>
        <td style="text-align:end">${money(l.unitPrice * qty)}</td>
      </tr>`;
    })
    .join('');

  const html = `<!DOCTYPE html>
<html dir="${dir}" lang="${dir === 'rtl' ? 'ar' : 'en'}">
<head>
  <meta charset="utf-8" />
  <title>${escapeHtml(labels.invoiceNumber)} ${escapeHtml(invoiceNo)}</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: "Segoe UI", Tahoma, Arial, sans-serif;
      color: #111;
      padding: 16px;
      max-width: 360px;
      margin: 0 auto;
      font-size: 13px;
      line-height: 1.45;
    }
    h1 { font-size: 18px; text-align: center; margin-bottom: 4px; }
    .branch { text-align: center; color: #444; margin-bottom: 12px; font-size: 12px; }
    .meta { margin-bottom: 12px; }
    .meta div { display: flex; justify-content: space-between; gap: 8px; margin: 2px 0; }
    .meta span:last-child { font-weight: 600; text-align: end; }
    hr { border: none; border-top: 1px dashed #999; margin: 10px 0; }
    table { width: 100%; border-collapse: collapse; margin: 6px 0; }
    th, td { padding: 3px 0; vertical-align: top; }
    th { font-size: 11px; color: #555; border-bottom: 1px solid #ccc; text-align: start; }
    .totals div { display: flex; justify-content: space-between; margin: 3px 0; }
    .grand { font-size: 16px; font-weight: 700; margin-top: 6px; }
    .thanks { text-align: center; margin-top: 14px; color: #555; font-size: 12px; }
    @media print {
      body { padding: 0; max-width: none; }
      @page { margin: 8mm; size: auto; }
    }
  </style>
</head>
<body>
  <h1>${escapeHtml(labels.title)}</h1>
  <div class="branch">${escapeHtml(branchName || labels.branch)}</div>
  <div class="meta">
    <div><span>${escapeHtml(labels.invoiceNumber)}</span><span>${escapeHtml(invoiceNo)}</span></div>
    <div><span>${escapeHtml(labels.date)}</span><span>${closedAt.toLocaleString()}</span></div>
    <div><span>${escapeHtml(labels.device)}</span><span>${escapeHtml(detail.deviceName)}</span></div>
    <div><span>${escapeHtml(labels.room)}</span><span>${escapeHtml(detail.roomName)}</span></div>
    <div><span>${escapeHtml(labels.mode)}</span><span>${escapeHtml(modeLabel)}</span></div>
    <div><span>${escapeHtml(labels.plan)}</span><span>${escapeHtml(detail.pricingPlanName)}</span></div>
    <div><span>${escapeHtml(labels.started)}</span><span>${startedAt.toLocaleString()}</span></div>
    <div><span>${escapeHtml(labels.closed)}</span><span>${closedAt.toLocaleString()}</span></div>
  </div>
  ${linesHtml ? `
  <hr />
  <table>
    <thead><tr>
      <th>${escapeHtml(labels.item)}</th>
      <th style="text-align:center">${escapeHtml(labels.qty)}</th>
      <th style="text-align:end">${escapeHtml(labels.total)}</th>
    </tr></thead>
    <tbody>${linesHtml}</tbody>
  </table>` : ''}
  <hr />
  <div class="totals">
    <div><span>${escapeHtml(labels.timeCost)}</span><span>${money(detail.timeCost)}</span></div>
    ${detail.roomSurchargeCost > 0 ? `<div><span>${escapeHtml(labels.roomSurcharge)}</span><span>${money(detail.roomSurchargeCost)}</span></div>` : ''}
    <div><span>${escapeHtml(labels.cafeteria)}</span><span>${money(detail.cafeteriaCost)}</span></div>
    ${detail.discountAmount > 0 ? `<div><span>${escapeHtml(labels.discount)}${detail.discountReason ? ` (${escapeHtml(detail.discountReason)})` : ''}</span><span>-${money(detail.discountAmount)}</span></div>` : ''}
    <div class="grand"><span>${escapeHtml(labels.total)}</span><span>${money(detail.totalCost)}</span></div>
    <div><span>${escapeHtml(labels.payment)}</span><span>${escapeHtml(payMethod)}</span></div>
    <div><span>${escapeHtml(labels.openedBy)}</span><span>${escapeHtml(detail.openedByName)}</span></div>
    ${detail.closedByName ? `<div><span>${escapeHtml(labels.closedBy)}</span><span>${escapeHtml(detail.closedByName)}</span></div>` : ''}
  </div>
  <p class="thanks">${escapeHtml(labels.thankYou)}</p>
  <script>
    window.onload = function () {
      window.focus();
      window.print();
    };
  </script>
</body>
</html>`;

  const win = window.open('', '_blank', 'noopener,noreferrer,width=420,height=720');
  if (!win) {
    // Popup blocked — fallback: download-like approach via blob URL
    const blob = new Blob([html], { type: 'text/html' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.target = '_blank';
    a.rel = 'noopener';
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 30_000);
    return;
  }
  win.document.open();
  win.document.write(html);
  win.document.close();
}
