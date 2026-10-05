import type { OrderListStatus, SourceMode, StockWarning } from '@/api/types';
import { formatDateOnly } from '@/lib/format';

//the stock screens' words, shared by the event page and stock and orders

export const sourceLabels: Record<SourceMode, string> = {
  Stock: 'From stock',
  Order: 'To order',
  Rent: 'To rent',
};

export const orderListStatusLabels: Record<OrderListStatus, string> = {
  Draft: 'Draft',
  PendingApproval: 'Waiting for approval',
  Approved: 'Approved',
  Placed: 'Placed',
};

const quantityFormat = new Intl.NumberFormat('en-ZA', { maximumFractionDigits: 2 });

export const formatQuantity = (value: number) => quantityFormat.format(value);

//the warnings in plain words (FR-27, FR-29). anything new from the api still gets a line
export function describeWarning(warning: StockWarning, unit: string) {
  if (warning.code === 'SHORTFALL' && warning.planned != null && warning.expected != null) {
    return `Below normal use for the pack size: ${formatQuantity(warning.planned)} ${unit} planned, about ${formatQuantity(warning.expected)} ${unit} expected.`;
  }
  if (warning.code === 'LEAD_TIME' && warning.leadTimeDays != null && warning.requiredBy) {
    return `The supplier needs ${warning.leadTimeDays} days' notice and it's wanted by ${formatDateOnly(warning.requiredBy)}.`;
  }
  return 'Check this line before ordering.';
}
