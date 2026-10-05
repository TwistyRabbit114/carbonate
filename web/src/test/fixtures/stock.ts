import type {
  EquipmentAsset,
  EventListItem,
  Incident,
  OrderList,
  StockCategory,
  StockItem,
  StockRequirement,
  Supplier,
} from '@/api/types';
import { divisionIds } from './commercial';
import { users } from './users';

//the demo stock from plan appendix d in the api seed's units, each event's planned quantities,
//a few order lists and a couple of incidents. the activation's ice is planned below normal use
//and inside the ice supplier's lead time, so both warnings show (FR-27, FR-29). quantities per
//100 guests and costs are placeholders until the client's real figures are in

//----------------------------------------------------------\\
//                              SUPPLIERS AND CATEGORIES
//----------------------------------------------------------\\

export const supplierIds = {
  ice: '5d000000-0000-0000-0000-000000000001',
  liquor: '5d000000-0000-0000-0000-000000000002',
  barHire: '5d000000-0000-0000-0000-000000000003',
};

export function demoSuppliers(): Supplier[] {
  return [
    {
      supplierId: supplierIds.ice,
      name: 'Coastal Ice Co.',
      contactName: 'Marlon K.',
      email: 'orders@coastalice.example',
      phone: '021 555 0101',
      leadTimeDays: 5,
      isLiquorSupplier: false,
      isActive: true,
    },
    {
      supplierId: supplierIds.liquor,
      name: 'Vine & Co. Distributors',
      contactName: 'Anele D.',
      email: 'trade@vineandco.example',
      phone: '021 555 0202',
      leadTimeDays: 2,
      isLiquorSupplier: true,
      isActive: true,
    },
    {
      supplierId: supplierIds.barHire,
      name: 'Cape Bar Hire',
      contactName: null,
      email: 'bookings@capebarhire.example',
      phone: null,
      leadTimeDays: 3,
      isLiquorSupplier: false,
      isActive: true,
    },
  ];
}

const categoryNames = ['Disposables', 'Consumables', 'Glassware', 'Bar kit'] as const;
type CategoryName = (typeof categoryNames)[number];

export const categoryIdFor = (name: CategoryName) =>
  `57ca0000-0000-0000-0000-00000000000${categoryNames.indexOf(name) + 1}`;

export function demoCategories(): StockCategory[] {
  return categoryNames.map((name) => ({
    categoryId: categoryIdFor(name),
    parentCategoryId: null,
    divisionId: divisionIds.CE,
    name,
  }));
}

//----------------------------------------------------------\\
//                              CATALOGUE
//----------------------------------------------------------\\

type ItemSpec = Pick<StockItem, 'sku' | 'name' | 'unit'> & {
  category: CategoryName;
  perHundred: number | null;
  source: StockRequirement['sourceMode'];
  supplierId: string | null;
  cost: number;
  asset?: boolean;
};

const catalogue: ItemSpec[] = [
  {
    sku: 'CUP-500',
    name: 'Cups 500 ml',
    unit: 'cups',
    category: 'Disposables',
    perHundred: 150,
    source: 'Stock',
    supplierId: null,
    cost: 0.85,
  },
  {
    sku: 'ICE-KG',
    name: 'Ice, bulk',
    unit: 'kg',
    category: 'Consumables',
    perHundred: 50,
    source: 'Order',
    supplierId: supplierIds.ice,
    cost: 4.2,
  },
  {
    sku: 'SPR-MIX',
    name: 'Spirits, mixed',
    unit: 'bottles',
    category: 'Consumables',
    perHundred: 12,
    source: 'Order',
    supplierId: supplierIds.liquor,
    cost: 189,
  },
  {
    sku: 'GLS-WINE',
    name: 'Wine glasses',
    unit: 'glasses',
    category: 'Glassware',
    perHundred: 200,
    source: 'Stock',
    supplierId: null,
    cost: 14.5,
  },
  {
    sku: 'BAR-MOB',
    name: 'Mobile bar unit',
    unit: 'units',
    category: 'Bar kit',
    perHundred: null,
    source: 'Rent',
    supplierId: supplierIds.barHire,
    cost: 950,
  },
  {
    sku: 'ICE-MCH',
    name: 'Ice machine',
    unit: 'units',
    category: 'Bar kit',
    perHundred: null,
    source: 'Stock',
    supplierId: null,
    cost: 18_000,
    asset: true,
  },
];

export const itemId = (index: number) => `57000000-0000-0000-0000-00000000000${index}`;

//the full records, standard cost included. the mock leaves the cost out for roles without
//finance.view_internal_cost, the way the api does
export function demoStockItems(): StockItem[] {
  return catalogue.map((spec, index) => ({
    stockItemId: itemId(index),
    categoryId: categoryIdFor(spec.category),
    categoryName: spec.category,
    defaultSupplierId: spec.supplierId,
    sku: spec.sku,
    name: spec.name,
    unit: spec.unit,
    isConsumable: !spec.asset && spec.source !== 'Rent',
    isAsset: spec.asset ?? false,
    reorderLevel: null,
    consumptionPerHundredGuests: spec.perHundred,
    standardUnitCost: spec.cost,
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

const day = 24 * 3_600_000;

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
//                              ORDER LISTS
//----------------------------------------------------------\\

//three lists for the coming fortnight, one at each step that needs someone: the ice is waiting
//for approval (sarah made it, so someone else signs it off), the liquor is still a draft, and
//the bar hire is approved and waiting to be placed (FR-28, FR-30)
export function demoOrderLists(): OrderList[] {
  const now = Date.now();
  const periodStart = sastDate(now);
  const periodEnd = sastDate(now + 14 * day);
  const at = (daysAgo: number) => new Date(now - daysAgo * day).toISOString();
  const line = (index: number, quantity: number, number: number) => ({
    lineId: `0111e000-0000-0000-0000-00000000000${number}`,
    stockItemId: itemId(index),
    stockItemName: catalogue[index]!.name,
    unit: catalogue[index]!.unit,
    quantityOrdered: quantity,
    estimatedUnitCost: catalogue[index]!.cost,
    notes: null,
  });

  return [
    {
      orderListId: '0111a000-0000-0000-0000-000000000001',
      eventId: null,
      supplierId: supplierIds.ice,
      supplierName: 'Coastal Ice Co.',
      generatedByUserId: users.eventManager.user.userId,
      status: 'PendingApproval',
      requiredByDate: sastDate(now + 3 * day),
      generatedAt: at(1),
      periodStart,
      periodEnd,
      approvedByUserId: null,
      approvedAt: null,
      placedAt: null,
      rowVersion: btoa('order-list-1-v2'),
      lines: [line(1, 570, 1)],
      warnings: [{ code: 'LEAD_TIME', leadTimeDays: 5, requiredBy: sastDate(now + 3 * day) }],
    },
    {
      orderListId: '0111a000-0000-0000-0000-000000000002',
      eventId: null,
      supplierId: supplierIds.liquor,
      supplierName: 'Vine & Co. Distributors',
      generatedByUserId: users.operationsManager.user.userId,
      status: 'Draft',
      requiredByDate: sastDate(now + 3 * day),
      generatedAt: at(0),
      periodStart,
      periodEnd,
      approvedByUserId: null,
      approvedAt: null,
      placedAt: null,
      rowVersion: btoa('order-list-2-v1'),
      lines: [line(2, 94, 2)],
      warnings: [],
    },
    {
      orderListId: '0111a000-0000-0000-0000-000000000003',
      eventId: null,
      supplierId: supplierIds.barHire,
      supplierName: 'Cape Bar Hire',
      generatedByUserId: users.operationsManager.user.userId,
      status: 'Approved',
      requiredByDate: sastDate(now + 12 * day),
      generatedAt: at(3),
      periodStart,
      periodEnd,
      approvedByUserId: users.accounts.user.userId,
      approvedAt: at(2),
      placedAt: null,
      rowVersion: btoa('order-list-3-v3'),
      lines: [line(4, 4, 3)],
      warnings: [],
    },
  ];
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
