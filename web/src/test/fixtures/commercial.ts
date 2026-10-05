import type {
  ClientOption,
  Division,
  EventListItem,
  Invoice,
  Quote,
  QuoteLine,
  QuoteLineCategory,
  QuoteStatus,
} from '@/api/types';
import { users } from './users';

//clients, divisions and each demo event's costing. client names match the api's demo seed, and
//the wedding's figures are the prototype's: R 84 500 to the client, R 58 200 to us, 31%

//----------------------------------------------------------\\
//                              CLIENTS AND DIVISIONS
//----------------------------------------------------------\\

export const clientNames: Record<string, string> = {
  'NAI-WED-26': 'Naidoo Family',
  'VAN-ACT-26': 'Vantage Brands',
  'MER-YE-26': 'Meridian Group',
  'RIV-FEST-26': 'Riverlight Events',
  'DEL-GOLF-26': 'Delacroix Holdings',
  'ATL-LNCH-26': 'Atlas Brands',
};

const clientCodes = Object.keys(clientNames);

export const clientIdFor = (eventCode: string) =>
  `c1000000-0000-0000-0000-00000000000${clientCodes.indexOf(eventCode) + 1}`;

export function demoClients(): ClientOption[] {
  return clientCodes
    .map((code) => ({ clientId: clientIdFor(code), name: clientNames[code]! }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

export const divisionIds = {
  CE: 'd1000000-0000-0000-0000-0000000000ce',
  CLM: 'd1000000-0000-0000-0000-00000000c1a0',
};

export function demoDivisions(): Division[] {
  return [
    { divisionId: divisionIds.CE, code: 'CE', name: 'Carbon Events' },
    { divisionId: divisionIds.CLM, code: 'CLM', name: 'Carbon Logistics Management' },
  ];
}

//----------------------------------------------------------\\
//                              COSTINGS
//----------------------------------------------------------\\

//the api's placeholder threshold until the director confirms the real one (plan section 15)
export const approvalThreshold = 100_000;

//price to the client ex VAT and the cost to us per event, and where its costing has got to.
//the activation is over the approval threshold and waiting for the director (FR-14)
const figures: Record<string, { price: number; cost: number; status: QuoteStatus }> = {
  'NAI-WED-26': { price: 84_500, cost: 58_200, status: 'Accepted' },
  'VAN-ACT-26': { price: 126_000, cost: 91_400, status: 'PendingApproval' },
  'MER-YE-26': { price: 98_000, cost: 66_150, status: 'Issued' },
  'RIV-FEST-26': { price: 412_000, cost: 301_500, status: 'Accepted' },
  'DEL-GOLF-26': { price: 38_500, cost: 25_900, status: 'Accepted' },
};

const round = (value: number) => Math.round(value * 100) / 100;
const day = 24 * 3_600_000;

//how each costing splits across the workbook's categories (T1 appendix B5). the last line takes
//whatever rounding left over, so the lines always add up to the totals
const split: [QuoteLineCategory, string, number][] = [
  ['SetUpAndStrike', 'Bar set-up and strike', 0.1],
  ['Infrastructure', 'Mobile bars and back bar', 0.2],
  ['Transportation', 'Truck and driver', 0.05],
  ['Crew', 'Bar staff', 0.25],
  ['Ice', 'Ice', 0.05],
  ['Glassware', 'Glassware hire', 0.1],
  ['Stock', 'Beverage stock', 0.25],
];

function linesFor(number: string, price: number, cost: number): QuoteLine[] {
  let priceLeft = price;
  let costLeft = cost;
  return split.map(([category, description, share], index) => {
    const last = index === split.length - 1;
    const linePrice = last ? round(priceLeft) : round(price * share);
    const lineCost = last ? round(costLeft) : round(cost * share);
    priceLeft -= linePrice;
    costLeft -= lineCost;
    return {
      quoteLineId: `9e1e000${number}-0000-0000-0000-00000000000${index}`,
      description,
      quantity: 1,
      category,
      unitCostToUs: lineCost,
      unitPriceToClient: linePrice,
      lineTotal: linePrice,
    };
  });
}

//one costing per event that has one, with its lines. the enquiry is still being costed
export function demoQuotes(event: EventListItem): Quote[] {
  const money = figures[event.eventCode];
  if (!money) return [];
  const number = event.eventId.slice(-1);
  const vat = round(money.price * 0.15);
  const issued = money.status === 'Issued' || money.status === 'Accepted';
  //one sent above the limit had the director's approval first, which the api then stops asking for
  const overLimit = round(money.price + vat) > approvalThreshold;
  const approved = overLimit && issued;

  return [
    {
      quoteId: `9e000000-0000-0000-0000-00000000000${number}`,
      eventId: event.eventId,
      copiedFromQuoteId: null,
      version: 1,
      status: money.status,
      subtotalExVat: money.price,
      vatAmount: vat,
      totalIncVat: round(money.price + vat),
      internalCostTotal: money.cost,
      marginPercent: round(((money.price - money.cost) / money.price) * 100),
      targetMarginBand: { targetMinPct: 28, targetMaxPct: 35 },
      requiresApproval: overLimit && !approved,
      validUntil: null,
      issuedAt: issued ? new Date(Date.now() - 20 * day).toISOString() : null,
      acceptedAt: money.status === 'Accepted' ? new Date(Date.now() - 18 * day).toISOString() : null,
      approvedByUserId: approved ? users.director.user.userId : null,
      approvedAt: approved ? new Date(Date.now() - 21 * day).toISOString() : null,
      createdAt: new Date(Date.now() - 25 * day).toISOString(),
      rowVersion: btoa(`quote-${number}-v1`),
      lines: linesFor(number, money.price, money.cost),
    },
  ];
}

//----------------------------------------------------------\\
//                              INVOICES
//----------------------------------------------------------\\

//the golf day is paid, the festival's invoice went out this morning (FR-13)
export function demoInvoices(events: EventListItem[]): Invoice[] {
  const sastDate = (instant: number) =>
    new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Johannesburg' }).format(new Date(instant));
  const byCode = (code: string) => events.find((event) => event.eventCode === code);
  const golf = byCode('DEL-GOLF-26');
  const festival = byCode('RIV-FEST-26');
  const now = Date.now();

  return [
    golf && {
      invoiceId: '1e000000-0000-0000-0000-000000000001',
      eventId: golf.eventId,
      eventCode: golf.eventCode,
      confirmationId: 'cf000000-0000-0000-0000-000000000005',
      confirmationReference: 'PO 4471-DH',
      invoiceNumber: 'INV-2026-0041',
      issuedDate: sastDate(now - 12 * day),
      dueDate: sastDate(now + 18 * day),
      amountIncVat: 44_275,
      status: 'Paid' as const,
      paidDate: sastDate(now - 2 * day),
    },
    festival && {
      invoiceId: '1e000000-0000-0000-0000-000000000002',
      eventId: festival.eventId,
      eventCode: festival.eventCode,
      confirmationId: 'cf000000-0000-0000-0000-000000000004',
      confirmationReference: 'Deposit RL-0926',
      invoiceNumber: 'INV-2026-0042',
      issuedDate: sastDate(now),
      dueDate: sastDate(now + 30 * day),
      amountIncVat: 473_800,
      status: 'Issued' as const,
      paidDate: null,
    },
  ].filter((invoice) => invoice !== undefined);
}

//each event's budget, a $price field. the mock adds it to the event only for roles that may see it
export const demoBudgets: Record<string, number> = {
  'NAI-WED-26': 90_000,
  'VAN-ACT-26': 130_000,
  'MER-YE-26': 100_000,
  'RIV-FEST-26': 420_000,
  'DEL-GOLF-26': 40_000,
};
