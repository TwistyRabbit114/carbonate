import type { AuditEntry, CalendarConnection } from '@/api/types';
import { users } from './users';

//a few days of the audit trail and the google calendar link, for the settings screens

//----------------------------------------------------------\\
//                              AUDIT
//----------------------------------------------------------\\

const hour = 3_600_000;

function entry(
  auditId: number,
  hoursAgo: number,
  who: keyof typeof users,
  action: string,
  entityName: string,
  entityId: string,
  before: object | null,
  after: object | null,
): AuditEntry {
  return {
    auditId,
    userId: users[who].user.userId,
    userName: users[who].user.fullName,
    action,
    entityName,
    entityId,
    occurredAt: new Date(Date.now() - hoursAgo * hour).toISOString(),
    ipAddress: '102.65.0.14',
    beforeJson: before && JSON.stringify(before),
    afterJson: after && JSON.stringify(after),
  };
}

//newest first, the way the api sorts them
export function demoAudit(): AuditEntry[] {
  return [
    entry(
      1046,
      1,
      'eventManager',
      'event.updated',
      'Event',
      '0e000000-0000-0000-0000-000000000001',
      { packSizeEstimated: 160, staffRequired: 10 },
      { packSizeEstimated: 180, staffRequired: 12 },
    ),
    entry(
      1045,
      3,
      'operationsManager',
      'card.completed',
      'TaskCard',
      'ad0c0000-0000-0000-0000-000000000004',
      { status: 'InProgressOrNeedsReview' },
      { status: 'Complete' },
    ),
    entry(
      1044,
      20,
      'director',
      'user.updated',
      'AppUser',
      users.casualCrew.user.userId,
      { roles: ['CasualCrew'], isActive: false },
      { roles: ['CasualCrew'], isActive: true },
    ),
    entry(
      1043,
      26,
      'operationsManager',
      'venue.created',
      'Venue',
      '5e000000-0000-0000-0000-000000000005',
      null,
      { name: 'Steenberg Golf Club', address: 'Steenberg Road, Tokai, Cape Town' },
    ),
    entry(
      1042,
      50,
      'accounts',
      'order_list.approved',
      'OrderList',
      '0111a000-0000-0000-0000-000000000003',
      { status: 'PendingApproval' },
      { status: 'Approved' },
    ),
  ];
}

//----------------------------------------------------------\\
//                              CALENDAR
//----------------------------------------------------------\\

export function demoCalendarConnection(): CalendarConnection {
  return {
    connected: true,
    googleAccountEmail: 'events-calendar@example.com',
    connectedAt: new Date(Date.now() - 4 * 24 * hour).toISOString(),
    reconnectNeeded: false,
    lastPushedAt: new Date(Date.now() - 2 * hour).toISOString(),
    lastError: null,
  };
}
