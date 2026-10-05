import type { ClientOption, Division, EventListItem, Quote } from '@/api/types';

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

//price to the client ex VAT and the cost to us, per event
const figures: Record<string, { price: number; cost: number }> = {
  'NAI-WED-26': { price: 84_500, cost: 58_200 },
  'VAN-ACT-26': { price: 126_000, cost: 91_400 },
  'MER-YE-26': { price: 98_000, cost: 66_150 },
  'RIV-FEST-26': { price: 412_000, cost: 301_500 },
  'DEL-GOLF-26': { price: 38_500, cost: 25_900 },
};

const round = (value: number) => Math.round(value * 100) / 100;

//one accepted costing per event that has one. the enquiry is still being costed
export function demoQuotes(event: EventListItem): Quote[] {
  const money = figures[event.eventCode];
  if (!money) return [];
  const number = event.eventId.slice(-1);
  const vat = round(money.price * 0.15);

  return [
    {
      quoteId: `9e000000-0000-0000-0000-00000000000${number}`,
      eventId: event.eventId,
      copiedFromQuoteId: null,
      version: 1,
      status: 'Accepted',
      subtotalExVat: money.price,
      vatAmount: vat,
      totalIncVat: round(money.price + vat),
      internalCostTotal: money.cost,
      marginPercent: round(((money.price - money.cost) / money.price) * 100),
      targetMarginBand: { targetMinPct: 28, targetMaxPct: 35 },
      requiresApproval: false,
      validUntil: null,
      issuedAt: new Date(Date.now() - 20 * 24 * 3_600_000).toISOString(),
      acceptedAt: new Date(Date.now() - 18 * 24 * 3_600_000).toISOString(),
      approvedByUserId: null,
      approvedAt: null,
      createdAt: new Date(Date.now() - 25 * 24 * 3_600_000).toISOString(),
      rowVersion: btoa(`quote-${number}-v1`),
      lines: [],
    },
  ];
}

//each event's budget, a $price field. the mock adds it to the event only for roles that may see it
export const demoBudgets: Record<string, number> = {
  'NAI-WED-26': 90_000,
  'VAN-ACT-26': 130_000,
  'MER-YE-26': 100_000,
  'RIV-FEST-26': 420_000,
  'DEL-GOLF-26': 40_000,
};
