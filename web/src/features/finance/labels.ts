import type { InvoiceStatus, Quote, QuoteLineCategory, QuoteStatus } from '@/api/types';

//the commercial screens' words, shared by the event page, quotes and invoices

export const quoteStatusLabels: Record<QuoteStatus, string> = {
  Draft: 'Draft',
  PendingApproval: 'Waiting for the Director',
  Approved: 'Approved',
  Issued: 'Sent to the client',
  Accepted: 'Accepted by the client',
  Superseded: 'Replaced by a newer version',
};

//the old costing workbook's categories (T1 appendix B5)
export const categoryLabels: Record<QuoteLineCategory, string> = {
  SetUpAndStrike: 'Set up & strike',
  Infrastructure: 'Infrastructure',
  Transportation: 'Transportation',
  Crew: 'Crew',
  Ice: 'Ice',
  Stock: 'Stock',
  BarKit: 'Bar kit',
  Glassware: 'Glassware',
  Other: 'Other',
};

export const quoteCategories = Object.keys(categoryLabels) as QuoteLineCategory[];

export const invoiceStatusLabels: Record<InvoiceStatus, string> = {
  Draft: 'Draft',
  Issued: 'Sent',
  Paid: 'Paid',
  Void: 'Cancelled',
};

export const percent = (value: number) => `${Math.round(value * 10) / 10}%`;

//the margin against the target band for the pack size (FR-11). both come from the api, nothing
//here works money out
export function outsideBand(quote: Pick<Quote, 'marginPercent' | 'targetMarginBand'> | undefined) {
  const margin = quote?.marginPercent;
  const band = quote?.targetMarginBand;
  return (
    margin != null &&
    ((band?.targetMinPct != null && margin < band.targetMinPct) ||
      (band?.targetMaxPct != null && margin > band.targetMaxPct))
  );
}
