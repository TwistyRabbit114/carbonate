import { HttpResponse } from 'msw';
import type {
  AuditEntry,
  Board,
  CalendarConnection,
  CrewAssignment,
  EquipmentAsset,
  EventDetail,
  Incident,
  Invoice,
  Milestone,
  OrderList,
  Quote,
  SiteVisit,
  StockCategory,
  StockItem,
  StockRequirement,
  Supplier,
  UserListItem,
  UserRef,
  Venue,
} from '@/api/types';
import { permissionCheck, type Permission } from '@/auth/permissions';
import { demoAdminBoard } from '../fixtures/adminTasks';
import { demoBudgets, demoInvoices, demoQuotes } from '../fixtures/commercial';
import { demoEventBoard } from '../fixtures/eventBoards';
import { demoCrew, demoMilestones, eventDetailFor } from '../fixtures/eventDetails';
import { demoEvents } from '../fixtures/events';
import { demoAudit, demoCalendarConnection } from '../fixtures/settings';
import {
  demoCategories,
  demoEquipment,
  demoIncidents,
  demoOrderLists,
  demoStockItems,
  demoStockRequirements,
  demoSuppliers,
} from '../fixtures/stock';
import { userList, users } from '../fixtures/users';
import { demoSiteVisits, demoVenues } from '../fixtures/venues';

//what every part of the mock api shares: the demo accounts and their tokens, the data, the
//answers the api gives, and who is asking. each area's handlers live in a file of their own

//----------------------------------------------------------\\
//                              ACCOUNTS AND TOKENS
//----------------------------------------------------------\\

export type AccountKey = keyof typeof users;

//mock tokens just name the account, there is nothing secret in them
export const accessTokenFor = (key: AccountKey) => `mock-access-${key}`;
export const mfaTokenFor = (key: AccountKey) => `mock-mfa-${key}`;

function isAccount(value: string | undefined): value is AccountKey {
  return value !== undefined && value in users;
}

export function accountFromBearer(header: string | null) {
  const key = header?.replace(/^Bearer mock-access-/, '');
  return isAccount(key) ? key : null;
}

export function accountFromMfaToken(token: unknown) {
  const key = typeof token === 'string' ? token.replace(/^mock-mfa-/, '') : undefined;
  return isAccount(key) ? key : null;
}

//mfa enrol and confirm get the mfa token as the bearer, like the real api
export function accountFromMfaBearer(header: string | null) {
  return accountFromMfaToken(header?.replace(/^Bearer /, ''));
}

//----------------------------------------------------------\\
//                              STATE
//----------------------------------------------------------\\

//the full records, money included. each response leaves out what the caller can't see
function demoEventDetails(): EventDetail[] {
  return demoEvents().map((event) => ({
    ...eventDetailFor(event),
    budgetAmount: demoBudgets[event.eventCode] ?? null,
  }));
}

function freshState() {
  const events = demoEventDetails();
  return {
    //the real api keeps the session in an HttpOnly refresh cookie. msw would save a mocked cookie
    //to localStorage, so the mock holds it in memory instead and a full reload signs you out
    session: null as AccountKey | null,
    events,
    milestones: events.flatMap(demoMilestones) as Milestone[],
    adminBoard: demoAdminBoard() as Board,
    eventBoards: events.map(demoEventBoard) as Board[],
    crew: events.flatMap(demoCrew) as CrewAssignment[],
    incidents: events.flatMap(demoIncidents) as Incident[],
    venues: demoVenues() as Venue[],
    siteVisits: events.flatMap(demoSiteVisits) as SiteVisit[],
    categories: demoCategories() as StockCategory[],
    suppliers: demoSuppliers() as Supplier[],
    stockItems: demoStockItems() as StockItem[],
    equipment: demoEquipment() as EquipmentAsset[],
    requirements: events.flatMap(demoStockRequirements) as StockRequirement[],
    orderLists: demoOrderLists() as OrderList[],
    quotes: events.flatMap(demoQuotes) as Quote[],
    invoices: demoInvoices(events) as Invoice[],
    users: userList() as UserListItem[],
    audit: demoAudit() as AuditEntry[],
    calendar: demoCalendarConnection() as CalendarConnection,
  };
}

export const mock = freshState();

//back to signed out with the demo data as it started, tests call this before each run
export function resetMocks() {
  Object.assign(mock, freshState());
}

//----------------------------------------------------------\\
//                              RESPONSES
//----------------------------------------------------------\\

export function problem(status: number, title: string, detail?: string, extra: object = {}) {
  return HttpResponse.json(
    { status, title, detail, ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

export const invalid = (errors: Record<string, string[]>) =>
  problem(400, 'One or more validation errors occurred.', undefined, { errors });

export const unauthorised = () => new HttpResponse(null, { status: 401 });

export const forbidden = (detail?: string) => problem(403, 'Forbidden', detail);

export const notFound = (detail?: string) => problem(404, 'Not found', detail);

export const businessRule = (detail: string) => problem(422, 'Business rule', detail);

//a stale rowVersion, with what's there now so the screen can offer to load it
export const stale = (title: string, detail: string, current: unknown) =>
  problem(409, title, detail, { type: '/problems/concurrency-conflict', current });

export const invalidTransition = (detail: string) =>
  problem(409, "That isn't allowed from here", detail, { type: '/problems/invalid-transition' });

//what the api still answers for a feature it hasn't built yet
export const notBuilt = () => problem(501, 'Not built yet', 'This part of Carbonate is still being built.');

export async function readJson(request: Request) {
  try {
    return (await request.json()) as Record<string, unknown>;
  } catch {
    return {};
  }
}

export const trimmed = (value: unknown) => (typeof value === 'string' ? value.trim() : '');
export const orNull = (value: unknown) => (typeof value === 'string' && value ? value : null);
export const numberOrNull = (value: unknown) =>
  typeof value === 'number' && Number.isFinite(value) ? value : null;

let rowVersionCounter = 0;
export function nextRowVersion() {
  rowVersionCounter++;
  return btoa(`mock-version-${rowVersionCounter}`);
}

//a masked field is left out of the json altogether, not sent as null (plan section 7.3)
export function without<T extends object, K extends keyof T>(value: T, key: K) {
  return Object.fromEntries(Object.entries(value).filter(([name]) => name !== key)) as Omit<T, K>;
}

export function withoutKeys<T extends object>(value: T, keys: readonly string[]): T {
  return Object.fromEntries(Object.entries(value).filter(([name]) => !keys.includes(name))) as T;
}

export const today = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Johannesburg' }).format(new Date());

//----------------------------------------------------------\\
//                              WHO IS ASKING
//----------------------------------------------------------\\

export type Caller = { key: AccountKey; me: UserRef; can: (code: Permission) => boolean };

export function caller(request: Request): Caller | null {
  const key = accountFromBearer(request.headers.get('Authorization'));
  if (!key) return null;
  const check = permissionCheck(users[key].permissions);
  return {
    key,
    me: personRefFor(users[key].user.userId)!,
    can: (code: Permission) => check.can(code),
  };
}

export function personRefFor(userId: string): UserRef | undefined {
  const person = mock.users.find((candidate) => candidate.userId === userId);
  return person && { userId, fullName: person.fullName };
}

//desk roles see every event, crew only the ones they're crewed on (plan section 7.2). anything
//else is a 404, never a 403, so a hidden event doesn't give itself away
export const seesAll = (who: Caller) => who.can('event.view_all');
export const onCrew = (eventId: string, userId: string) =>
  mock.crew.some((shift) => shift.eventId === eventId && shift.userId === userId);
export const canSeeEvent = (who: Caller, eventId: string) => seesAll(who) || onCrew(eventId, who.me.userId);

//someone who can open the event, so a card can go to them
export function canOpenEvent(eventId: string, userId: string) {
  const account = Object.values(users).find((candidate) => candidate.user.userId === userId);
  return Boolean(
    account && (permissionCheck(account.permissions).can('event.view_all') || onCrew(eventId, userId)),
  );
}

//a retired event is gone for everyone, the api keeps the row but stops returning it (FR-09)
export function visibleEvent(who: Caller, eventId: unknown) {
  const event = mock.events.find((candidate) => candidate.eventId === eventId && candidate.isActive);
  return event && canSeeEvent(who, event.eventId) ? event : undefined;
}

//the budget is $price: left out, not nulled, for a role without it (plan section 7.3)
export function eventFor(who: Caller, event: EventDetail): EventDetail {
  return who.can('finance.view_client_price') ? event : without(event, 'budgetAmount');
}
