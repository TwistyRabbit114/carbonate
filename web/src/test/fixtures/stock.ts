import type { EquipmentAsset, EventListItem, Incident, StockItem, StockRequirement } from '@/api/types';
import { users } from './users';

//the demo stock from plan appendix d in the api seed's units, each event's planned quantities,
//and a couple of incidents. the activation's ice is planned below normal use and inside the ice
//supplier's lead time, so both warnings show (FR-27, FR-29). quantities per 100 guests are
//placeholders until the client's real figures are in

//----------------------------------------------------------\\
//                              CATALOGUE
//----------------------------------------------------------\\

type ItemSpec = Pick<StockItem, 'sku' | 'name' | 'unit' | 'categoryName'> & {
  perHundred: number | null;
  source: StockRequirement['sourceMode'];
  asset?: boolean;
};

const catalogue: ItemSpec[] = [
  {
    sku: 'CUP-500',
    name: 'Cups 500 ml',
    unit: 'cups',
    categoryName: 'Disposables',
    perHundred: 150,
    source: 'Stock',
  },
  {
    sku: 'ICE-KG',
    name: 'Ice, bulk',
    unit: 'kg',
    categoryName: 'Consumables',
    perHundred: 50,
    source: 'Order',
  },
  {
    sku: 'SPR-MIX',
    name: 'Spirits, mixed',
    unit: 'bottles',
    categoryName: 'Consumables',
    perHundred: 12,
    source: 'Order',
  },
  {
    sku: 'GLS-WINE',
    name: 'Wine glasses',
    unit: 'glasses',
    categoryName: 'Glassware',
    perHundred: 200,
    source: 'Stock',
  },
  {
    sku: 'BAR-MOB',
    name: 'Mobile bar unit',
    unit: 'units',
    categoryName: 'Bar kit',
    perHundred: null,
    source: 'Rent',
  },
  {
    sku: 'ICE-MCH',
    name: 'Ice machine',
    unit: 'units',
    categoryName: 'Bar kit',
    perHundred: null,
    source: 'Stock',
    asset: true,
  },
];

const itemId = (index: number) => `57000000-0000-0000-0000-00000000000${index}`;

//no standard cost: it's a $cost field, and crew pick items from this list
export function demoStockItems(): StockItem[] {
  return catalogue.map((spec, index) => ({
    stockItemId: itemId(index),
    categoryId: `57ca0000-0000-0000-0000-00000000000${index}`,
    categoryName: spec.categoryName,
    defaultSupplierId: null,
    sku: spec.sku,
    name: spec.name,
    unit: spec.unit,
    isConsumable: !spec.asset && spec.source !== 'Rent',
    isAsset: spec.asset ?? false,
    reorderLevel: null,
    consumptionPerHundredGuests: spec.perHundred,
    isActive: true,
  }));
}

export function demoEquipment(): EquipmentAsset[] {
  return [
    {
      assetId: '5a5e0000-0000-0000-0000-000000000001',
      stockItemId: itemId(5),
      stockItemName: 'Ice machine',
      serialNumber: 'IM-0042',
      condition: 'Good',
      status: 'Available',
      purchaseDate: '2024-03-01',
    },
    {
      assetId: '5a5e0000-0000-0000-0000-000000000002',
      stockItemId: itemId(5),
      stockItemName: 'Ice machine',
      serialNumber: 'IM-0057',
      condition: 'Fair',
      status: 'Available',
      purchaseDate: '2022-11-15',
    },
  ];
}

//----------------------------------------------------------\\
//                              REQUIREMENTS
//----------------------------------------------------------\\

const sastDate = (instant: number) =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Johannesburg' }).format(new Date(instant));

//everything is wanted by load-in, six hours before doors
export function demoStockRequirements(event: EventListItem): StockRequirement[] {
  if (event.status === 'Enquired' || event.status === 'Cancelled') return [];
  const number = event.eventId.slice(-1);
  const requiredByDate = sastDate(new Date(event.startsAt).getTime() - 6 * 3_600_000);
  const pax = event.packSizeActual ?? event.packSizeEstimated;
  const shortOnIce = event.eventCode === 'VAN-ACT-26';

  return catalogue
    .filter((spec) => !spec.asset)
    .map((spec, index) => {
      const expected = spec.perHundred === null ? 1 : Math.ceil((spec.perHundred * pax) / 100);
      const iceShort = shortOnIce && spec.sku === 'ICE-KG';
      const planned = iceShort ? 180 : expected;
      return {
        requirementId: `5e9e000${number}-0000-0000-0000-00000000000${index}`,
        eventId: event.eventId,
        stockItemId: itemId(index),
        stockItemName: spec.name,
        unit: spec.unit,
        quantityRequired: planned,
        quantityAllocated: spec.source === 'Stock' ? planned : 0,
        requiredByDate,
        sourceMode: spec.source,
        notes: null,
        warnings: iceShort
          ? [
              { code: 'SHORTFALL', expected, planned },
              { code: 'LEAD_TIME', leadTimeDays: 5, requiredBy: requiredByDate },
            ]
          : [],
      };
    });
}

//----------------------------------------------------------\\
//                              INCIDENTS
//----------------------------------------------------------\\

//replacement cost is $cost, the mock leaves it out for roles without it like the api does
export function demoIncidents(event: EventListItem): Incident[] {
  const hour = 3_600_000;
  if (event.eventCode === 'RIV-FEST-26') {
    return [
      {
        incidentId: '1c000000-0000-0000-0000-000000000001',
        eventId: event.eventId,
        assetId: null,
        stockItemId: itemId(3),
        subjectName: 'Wine glasses',
        reportedByUserId: users.crewLead.user.userId,
        reportedByName: users.crewLead.user.fullName,
        incidentType: 'Breakage',
        quantity: 24,
        reportedAt: new Date(Date.now() - hour).toISOString(),
        description: 'A crate came off the trolley at the back bar.',
        resolutionNotes: null,
        replacementCost: 1_080,
        photoUrl: null,
      },
    ];
  }
  if (event.eventCode === 'DEL-GOLF-26') {
    return [
      {
        incidentId: '1c000000-0000-0000-0000-000000000002',
        eventId: event.eventId,
        assetId: '5a5e0000-0000-0000-0000-000000000002',
        stockItemId: itemId(5),
        subjectName: 'Ice machine IM-0057',
        reportedByUserId: users.casualCrew.user.userId,
        reportedByName: users.casualCrew.user.fullName,
        incidentType: 'EquipmentFailure',
        quantity: 1,
        reportedAt: new Date(new Date(event.startsAt).getTime() + 3 * hour).toISOString(),
        description: 'Stopped making ice at halfway house. Bagged ice from the club kitchen covered it.',
        resolutionNotes: 'Compressor replaced.',
        replacementCost: 4_500,
        photoUrl: null,
      },
    ];
  }
  return [];
}
