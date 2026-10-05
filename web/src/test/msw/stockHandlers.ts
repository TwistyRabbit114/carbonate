import { delay, http, HttpResponse } from 'msw';
import type {
  EquipmentAsset,
  Incident,
  IncidentType,
  OrderList,
  OrderListStatus,
  SourceMode,
  StockItem,
  StockRequirement,
  StockWarning,
  Supplier,
} from '@/api/types';
import {
  businessRule,
  caller,
  forbidden,
  invalid,
  invalidTransition,
  mock,
  nextRowVersion,
  notFound,
  numberOrNull,
  orNull,
  readJson,
  seesAll,
  stale,
  today,
  trimmed,
  unauthorised,
  visibleEvent,
  without,
  type Caller,
} from './mockCore';

//D's stock catalogue, event requirements, order lists and incidents (FR-24 to FR-31), with the
//api's permissions and wording

//----------------------------------------------------------\\
//                              MONEY
//----------------------------------------------------------\\

//standard, estimated and replacement costs are all $cost (plan section 7.3)
const seesCost = (who: Caller) => who.can('finance.view_internal_cost');

const itemFor = (who: Caller, item: StockItem): StockItem =>
  seesCost(who) ? item : without(item, 'standardUnitCost');

const orderListFor = (who: Caller, list: OrderList): OrderList =>
  seesCost(who) ? list : { ...list, lines: list.lines.map((line) => without(line, 'estimatedUnitCost')) };

const incidentFor = (who: Caller, incident: Incident): Incident =>
  seesCost(who) ? incident : without(incident, 'replacementCost');

//----------------------------------------------------------\\
//                              WARNINGS
//----------------------------------------------------------\\

const dayMs = 24 * 3_600_000;
const sourceModes: SourceMode[] = ['Stock', 'Order', 'Rent'];

//a supplier who can't deliver in time (FR-29)
function leadTimeWarnings(supplierId: string | null | undefined, requiredBy: string): StockWarning[] {
  const supplier = mock.suppliers.find((candidate) => candidate.supplierId === supplierId);
  const daysLeft =
    (new Date(`${requiredBy}T12:00:00Z`).getTime() - new Date(`${today()}T12:00:00Z`).getTime()) / dayMs;
  return supplier && supplier.leadTimeDays > daysLeft
    ? [{ code: 'LEAD_TIME', leadTimeDays: supplier.leadTimeDays, requiredBy }]
    : [];
}

//below normal use for the pack size (FR-27), then the lead time
function warningsFor(eventId: string, item: StockItem, quantity: number, requiredBy: string): StockWarning[] {
  const event = mock.events.find((candidate) => candidate.eventId === eventId);
  const warnings: StockWarning[] = [];
  if (event && item.consumptionPerHundredGuests != null) {
    const pax = event.packSizeActual ?? event.packSizeEstimated;
    const expected = Math.ceil((item.consumptionPerHundredGuests * pax) / 100);
    if (quantity < expected) warnings.push({ code: 'SHORTFALL', expected, planned: quantity });
  }
  return [...warnings, ...leadTimeWarnings(item.defaultSupplierId, requiredBy)];
}

//----------------------------------------------------------\\
//                              CATALOGUE CHECKS
//----------------------------------------------------------\\

function checkItem(
  body: Record<string, unknown>,
  exceptId: string | null,
): Omit<StockItem, 'stockItemId'> | Response {
  const errors: Record<string, string[]> = {};
  const category = mock.categories.find((candidate) => candidate.categoryId === body.categoryId);
  const supplierId = orNull(body.defaultSupplierId);
  const sku = trimmed(body.sku);
  const reorderLevel = numberOrNull(body.reorderLevel);
  const perHundred = numberOrNull(body.consumptionPerHundredGuests);
  const cost = numberOrNull(body.standardUnitCost);

  if (!category) errors.categoryId = ['That category does not exist.'];
  if (
    supplierId &&
    !mock.suppliers.some((supplier) => supplier.supplierId === supplierId && supplier.isActive)
  ) {
    errors.defaultSupplierId = ['That supplier does not exist or is not active.'];
  }
  if (!sku) errors.sku = ['Give the item a SKU.'];
  else if (mock.stockItems.some((item) => item.sku === sku && item.stockItemId !== exceptId)) {
    errors.sku = ['That SKU is already in use.'];
  }
  if (!trimmed(body.name)) errors.name = ["'Name' must not be empty."];
  if (!trimmed(body.unit)) errors.unit = ["'Unit' must not be empty."];
  if (reorderLevel !== null && reorderLevel < 0)
    errors.reorderLevel = ['A reorder level cannot be negative.'];
  if (perHundred !== null && perHundred < 0)
    errors.consumptionPerHundredGuests = ['Consumption cannot be negative.'];
  if (cost !== null && cost < 0) errors.standardUnitCost = ['A cost cannot be negative.'];
  if (Object.keys(errors).length > 0) return invalid(errors);

  return {
    categoryId: category!.categoryId,
    categoryName: category!.name,
    defaultSupplierId: supplierId,
    sku,
    name: trimmed(body.name),
    unit: trimmed(body.unit),
    isConsumable: body.isConsumable === true,
    isAsset: body.isAsset === true,
    reorderLevel,
    consumptionPerHundredGuests: perHundred,
    standardUnitCost: cost,
    isActive: body.isActive !== false,
  };
}

function checkSupplier(body: Record<string, unknown>): Omit<Supplier, 'supplierId'> | Response {
  const errors: Record<string, string[]> = {};
  const leadTime = numberOrNull(body.leadTimeDays);
  if (!trimmed(body.name)) errors.name = ['Give the supplier a name.'];
  if (leadTime === null || !Number.isInteger(leadTime))
    errors.leadTimeDays = ['Enter the lead time in whole days.'];
  else if (leadTime < 0) errors.leadTimeDays = ['A lead time cannot be negative.'];
  if (Object.keys(errors).length > 0) return invalid(errors);

  return {
    name: trimmed(body.name),
    contactName: orNull(trimmed(body.contactName)),
    email: orNull(trimmed(body.email)),
    phone: orNull(trimmed(body.phone)),
    leadTimeDays: leadTime!,
    isLiquorSupplier: body.isLiquorSupplier === true,
    isActive: body.isActive !== false,
  };
}

function checkAsset(
  body: Record<string, unknown>,
  exceptId: string | null,
): Omit<EquipmentAsset, 'assetId'> | Response {
  const errors: Record<string, string[]> = {};
  const item = mock.stockItems.find((candidate) => candidate.stockItemId === body.stockItemId);
  const serial = trimmed(body.serialNumber);
  if (!item) errors.stockItemId = ['That stock item does not exist.'];
  if (!serial) errors.serialNumber = ['Give the asset a serial number.'];
  else if (mock.equipment.some((asset) => asset.serialNumber === serial && asset.assetId !== exceptId)) {
    errors.serialNumber = ['That serial number is already recorded.'];
  }
  if (!trimmed(body.condition)) errors.condition = ["'Condition' must not be empty."];
  if (!trimmed(body.status)) errors.status = ["'Status' must not be empty."];
  if (Object.keys(errors).length > 0) return invalid(errors);

  return {
    stockItemId: item!.stockItemId,
    stockItemName: item!.name,
    serialNumber: serial,
    condition: trimmed(body.condition),
    status: trimmed(body.status),
    purchaseDate: orNull(body.purchaseDate),
  };
}

//----------------------------------------------------------\\
//                              ORDER LIST MOVES
//----------------------------------------------------------\\

const nextStatus: Record<OrderListStatus, OrderListStatus | null> = {
  Draft: 'PendingApproval',
  PendingApproval: 'Approved',
  Approved: 'Placed',
  Placed: null,
};

async function moveList(request: Request, orderListId: unknown, to: OrderListStatus) {
  await delay();
  const who = caller(request);
  if (!who) return unauthorised();
  const permission =
    to === 'PendingApproval' ? 'order.generate' : to === 'Approved' ? 'order.approve' : 'order.place';
  if (!who.can(permission)) return forbidden();

  const list = mock.orderLists.find((candidate) => candidate.orderListId === orderListId);
  if (!list) return notFound();
  if (nextStatus[list.status] !== to) {
    return invalidTransition(`An order list cannot move from ${list.status} to ${to}.`);
  }
  //separation of duties (FR-30)
  if (to === 'Approved' && list.generatedByUserId === who.me.userId) {
    return businessRule(
      'The person who generated an order list cannot approve it. Ask someone else with approval rights.',
    );
  }
  const { rowVersion } = await readJson(request);
  if (rowVersion !== list.rowVersion) {
    return stale(
      'Someone else changed this order list.',
      'Reload it to see the latest.',
      orderListFor(who, list),
    );
  }

  const now = new Date().toISOString();
  const moved: OrderList = {
    ...list,
    status: to,
    approvedByUserId: to === 'Approved' ? who.me.userId : list.approvedByUserId,
    approvedAt: to === 'Approved' ? now : list.approvedAt,
    placedAt: to === 'Placed' ? now : list.placedAt,
    rowVersion: nextRowVersion(),
  };
  mock.orderLists = mock.orderLists.map((candidate) => (candidate === list ? moved : candidate));
  return HttpResponse.json(orderListFor(who, moved));
}

//one list per supplier for the confirmed and live events' order and rental lines in the period.
//items with no supplier are reported back rather than listed (FR-28)
function generate(who: Caller, from: string, to: string) {
  const live = new Set(
    mock.events
      .filter(
        (event) =>
          event.isActive && (event.status === 'ConfirmedInPlanning' || event.status === 'InProgress'),
      )
      .map((event) => event.eventId),
  );
  const candidates = mock.requirements.filter(
    (requirement) =>
      live.has(requirement.eventId) &&
      requirement.sourceMode !== 'Stock' &&
      requirement.requiredByDate >= from &&
      requirement.requiredByDate <= to,
  );

  const bySupplier = new Map<string, StockRequirement[]>();
  const unassigned = new Map<string, string>();
  for (const requirement of candidates) {
    const item = mock.stockItems.find((candidate) => candidate.stockItemId === requirement.stockItemId);
    if (!item?.defaultSupplierId) {
      unassigned.set(requirement.stockItemId, requirement.stockItemName);
      continue;
    }
    bySupplier.set(item.defaultSupplierId, [...(bySupplier.get(item.defaultSupplierId) ?? []), requirement]);
  }

  const lists: OrderList[] = [...bySupplier].map(([supplierId, requirements]) => {
    const supplier = mock.suppliers.find((candidate) => candidate.supplierId === supplierId)!;
    const requiredBy = requirements.map((requirement) => requirement.requiredByDate).sort()[0]!;
    const items = [...new Set(requirements.map((requirement) => requirement.stockItemId))];
    return {
      orderListId: crypto.randomUUID(),
      eventId: null,
      supplierId,
      supplierName: supplier.name,
      generatedByUserId: who.me.userId,
      status: 'Draft',
      requiredByDate: requiredBy,
      generatedAt: new Date().toISOString(),
      periodStart: from,
      periodEnd: to,
      approvedByUserId: null,
      approvedAt: null,
      placedAt: null,
      rowVersion: nextRowVersion(),
      lines: items.map((stockItemId) => {
        const item = mock.stockItems.find((candidate) => candidate.stockItemId === stockItemId)!;
        return {
          lineId: crypto.randomUUID(),
          stockItemId,
          stockItemName: item.name,
          unit: item.unit,
          quantityOrdered: requirements
            .filter((requirement) => requirement.stockItemId === stockItemId)
            .reduce((total, requirement) => total + requirement.quantityRequired, 0),
          estimatedUnitCost: item.standardUnitCost ?? null,
          notes: null,
        };
      }),
      warnings: leadTimeWarnings(supplierId, requiredBy),
    };
  });

  mock.orderLists = [...lists, ...mock.orderLists];
  return {
    orderLists: lists.map((list) => orderListFor(who, list)),
    unassignedSupplier: [...unassigned].map(([stockItemId, stockItemName]) => ({
      stockItemId,
      stockItemName,
    })),
  };
}

//----------------------------------------------------------\\
//                              HANDLERS
//----------------------------------------------------------\\

const incidentTypes: IncidentType[] = ['Breakage', 'EquipmentFailure', 'StockShortfall'];

export const stockHandlers = [
  //D's api takes stock.plan to read these, so crew and accounts get a 403
  http.get('/api/events/:eventId/stock-requirements', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.plan')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();
    return HttpResponse.json(
      mock.requirements.filter((requirement) => requirement.eventId === event.eventId),
    );
  }),

  //the whole list at once: a line without a requirementId is new, one left out is removed
  http.put('/api/events/:eventId/stock-requirements', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.plan')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();

    const body = await readJson(request);
    const lines = Array.isArray(body.items) ? (body.items as Record<string, unknown>[]) : [];
    const errors: Record<string, string[]> = {};
    lines.forEach((line, index) => {
      const quantity = numberOrNull(line.quantityRequired);
      if (!mock.stockItems.some((item) => item.stockItemId === line.stockItemId)) {
        errors[`items[${index}].stockItemId`] = ['Choose a stock item.'];
      }
      if (quantity === null || quantity < 0)
        errors[`items[${index}].quantityRequired`] = ['A quantity cannot be negative.'];
      if (!trimmed(line.requiredByDate))
        errors[`items[${index}].requiredByDate`] = ['Choose the date it is needed by.'];
      if (!sourceModes.includes(line.sourceMode as SourceMode)) {
        errors[`items[${index}].sourceMode`] = ['Choose stock, order or rent.'];
      }
    });
    const ids = lines.map((line) => line.stockItemId);
    if (new Set(ids).size !== ids.length)
      errors.items = ['Each stock item can appear only once. Combine the duplicate lines.'];
    if (Object.keys(errors).length > 0) return invalid(errors);

    const saved: StockRequirement[] = lines.map((line) => {
      const item = mock.stockItems.find((candidate) => candidate.stockItemId === line.stockItemId)!;
      const existing = mock.requirements.find(
        (requirement) =>
          requirement.requirementId === line.requirementId && requirement.eventId === event.eventId,
      );
      const quantity = line.quantityRequired as number;
      const requiredBy = trimmed(line.requiredByDate);
      return {
        requirementId: existing?.requirementId ?? crypto.randomUUID(),
        eventId: event.eventId,
        stockItemId: item.stockItemId,
        stockItemName: item.name,
        unit: item.unit,
        quantityRequired: quantity,
        quantityAllocated: existing?.quantityAllocated ?? 0,
        requiredByDate: requiredBy,
        sourceMode: line.sourceMode as SourceMode,
        notes: orNull(trimmed(line.notes)),
        warnings: warningsFor(event.eventId, item, quantity, requiredBy),
      };
    });
    mock.requirements = [
      ...mock.requirements.filter((requirement) => requirement.eventId !== event.eventId),
      ...saved,
    ];
    return HttpResponse.json(saved);
  }),

  http.get('/api/stock/categories', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    return who.can('stock.view') ? HttpResponse.json(mock.categories) : forbidden();
  }),

  http.get('/api/stock/items', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.view')) return forbidden();

    const params = new URL(request.url).searchParams;
    const q = (params.get('q') ?? '').trim().toLowerCase();
    const categoryId = params.get('categoryId');
    const items = mock.stockItems
      .filter((item) => item.name.toLowerCase().includes(q) || item.sku.toLowerCase().includes(q))
      .filter((item) => !categoryId || item.categoryId === categoryId)
      .map((item) => itemFor(who, item));
    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  http.post('/api/stock/items', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.manage')) return forbidden();
    const checked = checkItem(await readJson(request), null);
    if (checked instanceof Response) return checked;
    const item: StockItem = { ...checked, stockItemId: crypto.randomUUID() };
    mock.stockItems = [...mock.stockItems, item];
    return HttpResponse.json(itemFor(who, item), { status: 201 });
  }),

  http.put('/api/stock/items/:stockItemId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.manage')) return forbidden();
    const item = mock.stockItems.find((candidate) => candidate.stockItemId === params.stockItemId);
    if (!item) return notFound();

    const body = await readJson(request);
    const checked = checkItem(body, item.stockItemId);
    if (checked instanceof Response) return checked;
    //someone who can't see the cost can't wipe it either
    const standardUnitCost = seesCost(who) ? checked.standardUnitCost : item.standardUnitCost;
    const updated: StockItem = { ...checked, standardUnitCost, stockItemId: item.stockItemId };
    mock.stockItems = mock.stockItems.map((candidate) => (candidate === item ? updated : candidate));
    return HttpResponse.json(itemFor(who, updated));
  }),

  http.get('/api/suppliers', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    return who.can('stock.view') ? HttpResponse.json(mock.suppliers) : forbidden();
  }),

  http.post('/api/suppliers', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.manage')) return forbidden();
    const checked = checkSupplier(await readJson(request));
    if (checked instanceof Response) return checked;
    const supplier: Supplier = { ...checked, supplierId: crypto.randomUUID() };
    mock.suppliers = [...mock.suppliers, supplier];
    return HttpResponse.json(supplier, { status: 201 });
  }),

  http.put('/api/suppliers/:supplierId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.manage')) return forbidden();
    const supplier = mock.suppliers.find((candidate) => candidate.supplierId === params.supplierId);
    if (!supplier) return notFound();
    const checked = checkSupplier(await readJson(request));
    if (checked instanceof Response) return checked;
    const updated: Supplier = { ...checked, supplierId: supplier.supplierId };
    mock.suppliers = mock.suppliers.map((candidate) => (candidate === supplier ? updated : candidate));
    return HttpResponse.json(updated);
  }),

  http.get('/api/equipment', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    return who.can('stock.view') ? HttpResponse.json(mock.equipment) : forbidden();
  }),

  http.post('/api/equipment', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.manage')) return forbidden();
    const checked = checkAsset(await readJson(request), null);
    if (checked instanceof Response) return checked;
    const asset: EquipmentAsset = { ...checked, assetId: crypto.randomUUID() };
    mock.equipment = [...mock.equipment, asset];
    return HttpResponse.json(asset, { status: 201 });
  }),

  http.put('/api/equipment/:assetId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.manage')) return forbidden();
    const asset = mock.equipment.find((candidate) => candidate.assetId === params.assetId);
    if (!asset) return notFound();
    const checked = checkAsset(await readJson(request), asset.assetId);
    if (checked instanceof Response) return checked;
    const updated: EquipmentAsset = { ...checked, assetId: asset.assetId };
    mock.equipment = mock.equipment.map((candidate) => (candidate === asset ? updated : candidate));
    return HttpResponse.json(updated);
  }),

  http.post('/api/order-lists/generate', async ({ request }) => {
    await delay(400);
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('order.generate')) return forbidden();

    const { from, to } = await readJson(request);
    const errors: Record<string, string[]> = {};
    if (!trimmed(from)) errors.from = ['Choose the start of the period.'];
    if (!trimmed(to)) errors.to = ['Choose the end of the period.'];
    else if (trimmed(from) && trimmed(to) < trimmed(from))
      errors.to = ['The end of the period cannot be before the start.'];
    if (Object.keys(errors).length > 0) return invalid(errors);
    return HttpResponse.json(generate(who, trimmed(from), trimmed(to)));
  }),

  http.get('/api/order-lists', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.view')) return forbidden();
    const status = new URL(request.url).searchParams.get('status');
    const items = mock.orderLists
      .filter((list) => !status || list.status === status)
      .map((list) => orderListFor(who, list));
    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  http.get('/api/order-lists/:orderListId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.view')) return forbidden();
    const list = mock.orderLists.find((candidate) => candidate.orderListId === params.orderListId);
    return list ? HttpResponse.json(orderListFor(who, list)) : notFound();
  }),

  http.post('/api/order-lists/:orderListId/submit', ({ request, params }) =>
    moveList(request, params.orderListId, 'PendingApproval'),
  ),
  http.post('/api/order-lists/:orderListId/approve', ({ request, params }) =>
    moveList(request, params.orderListId, 'Approved'),
  ),
  http.post('/api/order-lists/:orderListId/mark-placed', ({ request, params }) =>
    moveList(request, params.orderListId, 'Placed'),
  ),

  //desk roles see every report, a crew lead those on their events, casual crew only their own
  http.get('/api/events/:eventId/incidents', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('incident.view')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();

    const ownOnly = !seesAll(who) && !who.can('stock.view');
    const items = mock.incidents
      .filter((incident) => incident.eventId === event.eventId)
      .filter((incident) => !ownOnly || incident.reportedByUserId === who.me.userId)
      .map((incident) => incidentFor(who, incident));
    return HttpResponse.json(items);
  }),

  //an asset's history across events, for whoever can see those events
  http.get('/api/incidents', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('incident.view')) return forbidden();
    const assetId = new URL(request.url).searchParams.get('assetId');
    const items = mock.incidents
      .filter((incident) => !assetId || incident.assetId === assetId)
      .filter((incident) => visibleEvent(who, incident.eventId))
      .map((incident) => incidentFor(who, incident));
    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  http.post('/api/events/:eventId/incidents', async ({ request, params }) => {
    await delay(600);
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('incident.create')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();

    const form = await request.formData();
    const text = (key: string) => {
      const value = form.get(key);
      return typeof value === 'string' ? value.trim() : '';
    };
    const incidentType = text('IncidentType') as IncidentType;
    const asset = mock.equipment.find((candidate) => candidate.assetId === text('AssetId'));
    const item = mock.stockItems.find((candidate) => candidate.stockItemId === text('StockItemId'));
    const quantity = text('Quantity') ? Number(text('Quantity')) : null;
    const photo = form.get('Photo');

    const errors: Record<string, string[]> = {};
    if (!incidentTypes.includes(incidentType)) errors.incidentType = ['Choose what kind of problem it was.'];
    if (!asset && !item) errors.assetId = ['Choose the equipment or the stock item this happened to.'];
    if (quantity !== null && !(Number.isInteger(quantity) && quantity >= 1)) {
      errors.quantity = ['A quantity must be more than zero.'];
    }
    if (!text('Description')) errors.description = ['Say what happened.'];
    if (photo instanceof File && photo.size > 10 * 1024 * 1024) errors.file = ['Photos can be up to 10 MB.'];
    if (Object.keys(errors).length > 0) return invalid(errors);

    const incident: Incident = {
      incidentId: crypto.randomUUID(),
      eventId: event.eventId,
      assetId: asset?.assetId ?? null,
      stockItemId: item?.stockItemId ?? asset?.stockItemId ?? null,
      subjectName: asset ? `${asset.stockItemName} ${asset.serialNumber}` : item!.name,
      reportedByUserId: who.me.userId,
      reportedByName: who.me.fullName,
      incidentType,
      quantity,
      reportedAt: new Date().toISOString(),
      description: text('Description'),
      resolutionNotes: null,
      replacementCost: null,
      photoUrl: null,
    };
    mock.incidents = [...mock.incidents, incident];
    return HttpResponse.json(incidentFor(who, incident), { status: 201 });
  }),

  //the office follows a report up with what was done and what it cost
  http.patch('/api/incidents/:incidentId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('stock.manage')) return forbidden();
    const incident = mock.incidents.find((candidate) => candidate.incidentId === params.incidentId);
    if (!incident) return notFound();

    const body = await readJson(request);
    const cost = numberOrNull(body.replacementCost);
    if (cost !== null && cost < 0) return invalid({ replacementCost: ['A cost cannot be negative.'] });
    const updated: Incident = {
      ...incident,
      resolutionNotes:
        'resolutionNotes' in body ? orNull(trimmed(body.resolutionNotes)) : incident.resolutionNotes,
      replacementCost: 'replacementCost' in body ? cost : incident.replacementCost,
    };
    mock.incidents = mock.incidents.map((candidate) => (candidate === incident ? updated : candidate));
    return HttpResponse.json(incidentFor(who, updated));
  }),
];
