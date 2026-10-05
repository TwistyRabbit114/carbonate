import { delay, http, HttpResponse } from 'msw';
import type { RoleName, UserListItem } from '@/api/types';
import { users } from '../fixtures/users';
import {
  businessRule,
  type Caller,
  caller,
  forbidden,
  invalid,
  mock,
  notFound,
  readJson,
  trimmed,
  unauthorised,
} from './mockCore';

//C's user admin and audit trail (FR-37, FR-38) and D's google calendar link (FR-40), with the
//api's rules and wording

//----------------------------------------------------------\\
//                              USERS
//----------------------------------------------------------\\

const roleNames: RoleName[] = [
  'Director',
  'OperationsManager',
  'EventManager',
  'Accounts',
  'CrewLead',
  'CasualCrew',
];

const isDirector = (who: Caller) => users[who.key].roles.some((role) => role === 'Director');

const activeDirectors = (except?: string) =>
  mock.users.filter((user) => user.isActive && user.roles.includes('Director') && user.userId !== except)
    .length;

function rolesFrom(value: unknown): RoleName[] | Response {
  const sent = Array.isArray(value) ? [...new Set(value as string[])] : [];
  if (sent.length === 0) return invalid({ roles: ['Choose at least one role.'] });
  const unknown = sent.filter((role) => !roleNames.includes(role as RoleName));
  if (unknown.length > 0) return invalid({ roles: [`Not a role: ${unknown.join(', ')}.`] });
  return sent as RoleName[];
}

//----------------------------------------------------------\\
//                              HANDLERS
//----------------------------------------------------------\\

export const settingsHandlers = [
  //only director and ops hold user.manage, the same people who hand tasks out
  http.get('/api/users', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('user.manage')) return forbidden();

    const params = new URL(request.url).searchParams;
    const q = (params.get('q') ?? '').trim().toLowerCase();
    const active = params.get('isActive');
    const items = mock.users
      .filter((user) => active === null || String(user.isActive) === active)
      .filter((user) => user.fullName.toLowerCase().includes(q) || user.email.toLowerCase().includes(q))
      .sort((a, b) => a.fullName.localeCompare(b.fullName));
    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  http.post('/api/users', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('user.manage')) return forbidden();

    const body = await readJson(request);
    const roles = rolesFrom(body.roles);
    if (roles instanceof Response) return roles;
    if (roles.includes('Director') && !isDirector(who)) {
      return forbidden('Only the Director can give someone the Director role.');
    }

    const errors: Record<string, string[]> = {};
    const email = trimmed(body.email);
    const number = trimmed(body.employeeNumber);
    if (!/^[A-Za-z0-9-]+$/.test(number)) errors.employeeNumber = ['Use letters, numbers and hyphens only.'];
    else if (mock.users.some((user) => user.employeeNumber === number)) {
      errors.employeeNumber = ['Someone already has that employee number.'];
    }
    if (!/^[^@\s]+@[^@\s]+$/.test(email)) errors.email = ["'Email' is not a valid email address."];
    else if (mock.users.some((user) => user.email.toLowerCase() === email.toLowerCase())) {
      errors.email = ['Someone already has that email address.'];
    }
    if (!trimmed(body.fullName)) errors.fullName = ["'Full Name' must not be empty."];
    if (trimmed(body.initialPassword).length < 12) errors.initialPassword = ['Use at least 12 characters.'];
    if (Object.keys(errors).length > 0) return invalid(errors);

    const user: UserListItem = {
      userId: crypto.randomUUID(),
      employeeNumber: number,
      email,
      fullName: trimmed(body.fullName),
      employmentType: body.employmentType === 'Casual' ? 'Casual' : 'Permanent',
      isActive: true,
      mfaEnabled: false,
      roles,
      lastLoginAt: null,
    };
    mock.users = [...mock.users, user];
    return HttpResponse.json(user, { status: 201 });
  }),

  //the api keeps at least one active director, and nobody changes their own roles or access
  http.patch('/api/users/:userId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('user.manage')) return forbidden();
    const user = mock.users.find((candidate) => candidate.userId === params.userId);
    if (!user) return notFound('That user was not found.');

    const body = await readJson(request);
    if (user.roles.includes('Director') && !isDirector(who)) {
      return forbidden("Only the Director can change a Director's account.");
    }
    const roles = body.roles === undefined || body.roles === null ? null : rolesFrom(body.roles);
    if (roles instanceof Response) return roles;
    if (roles && user.userId === who.me.userId) {
      return businessRule('You cannot change your own roles. Ask another administrator.');
    }
    if (roles?.includes('Director') && !user.roles.includes('Director') && !isDirector(who)) {
      return forbidden('Only the Director can give someone the Director role.');
    }
    if (body.isActive === false && user.userId === who.me.userId) {
      return businessRule('You cannot deactivate your own account.');
    }

    const updated: UserListItem = {
      ...user,
      fullName: trimmed(body.fullName) || user.fullName,
      roles: roles ?? user.roles,
      isActive: typeof body.isActive === 'boolean' ? body.isActive : user.isActive,
    };
    if (user.roles.includes('Director') && (!updated.isActive || !updated.roles.includes('Director'))) {
      if (activeDirectors(user.userId) === 0) {
        return businessRule('There must always be at least one active Director.');
      }
    }
    mock.users = mock.users.map((candidate) => (candidate === user ? updated : candidate));
    return HttpResponse.json(updated);
  }),

  //newest first. from and to are whole days, so the end of the range is included
  http.get('/api/audit', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('audit.view')) return forbidden();

    const params = new URL(request.url).searchParams;
    const entity = params.get('entity');
    const from = params.get('from');
    const to = params.get('to');
    if (from && to && to < from) return invalid({ to: ['The end date cannot be before the start date.'] });
    const items = mock.audit
      .filter((entry) => !entity || entry.entityName === entity)
      .filter((entry) => !from || entry.occurredAt >= from)
      .filter((entry) => !to || entry.occurredAt <= to);
    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  http.get('/api/calendar/connection', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    return who.can('calendar.view') ? HttpResponse.json(mock.calendar) : forbidden();
  }),

  //the real one answers with google's consent page. the mock connects straight away and sends
  //you back to the settings page, so nothing leaves the app while demoing
  http.post('/api/calendar/connect', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('calendar.connect')) return forbidden();
    mock.calendar = {
      connected: true,
      googleAccountEmail: 'events-calendar@example.com',
      connectedAt: new Date().toISOString(),
      reconnectNeeded: false,
      lastPushedAt: null,
      lastError: null,
    };
    return HttpResponse.json({ authorizationUrl: '/settings' });
  }),

  http.delete('/api/calendar/connection', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('calendar.connect')) return forbidden();
    mock.calendar = {
      connected: false,
      googleAccountEmail: null,
      connectedAt: null,
      reconnectNeeded: false,
      lastPushedAt: null,
      lastError: null,
    };
    return new HttpResponse(null, { status: 204 });
  }),
];
