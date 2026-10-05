import type { MeResponse, UserListItem } from '@/api/types';
import { permissionCodes, type Permission } from '@/auth/permissions';

//one demo account per role, names from the prototype and plan appendix d. permission sets
//follow the matrix in plan section 7.4, where "asg"/"own" still means the role holds the
//code and the api narrows it to assigned records

//----------------------------------------------------------\\
//                              PERMISSIONS BY ROLE
//----------------------------------------------------------\\

const director: Permission[] = [...permissionCodes];

const operationsManager: Permission[] = [
  'event.view_all',
  'event.view_assigned',
  'event.create',
  'event.edit',
  'event.transition',
  'crew.assign',
  'client.view_contacts',
  'task.edit',
  'task.move',
  'admin_task.view',
  'admin_task.edit',
  'admin_task.assign',
  'admin_task.review',
  'venue.edit',
  'stock.view',
  'stock.manage',
  'stock.plan',
  'order.generate',
  'incident.create',
  'incident.view',
  'document.upload',
  'document.view_confidential',
  'calendar.view',
  'calendar.connect',
  'user.manage',
];

const eventManager: Permission[] = [
  'event.view_all',
  'event.view_assigned',
  'event.create',
  'event.edit',
  'event.transition',
  'crew.assign',
  'finance.view_client_price',
  'finance.view_internal_cost',
  'finance.view_margin',
  'quote.view',
  'quote.edit',
  'confirmation.record',
  'invoice.view',
  'client.view_contacts',
  'task.edit',
  'task.move',
  'admin_task.view',
  'admin_task.edit',
  'venue.edit',
  'stock.view',
  'stock.plan',
  'order.generate',
  'incident.create',
  'incident.view',
  'document.upload',
  'document.view_confidential',
  'calendar.view',
];

const accounts: Permission[] = [
  'event.view_all',
  'event.view_assigned',
  'finance.view_client_price',
  'finance.view_internal_cost',
  'finance.view_margin',
  'finance.view_staff_cost',
  'quote.view',
  'quote.edit',
  'confirmation.record',
  'invoice.view',
  'invoice.manage',
  'reconciliation.edit',
  'client.view_contacts',
  'admin_task.view',
  'admin_task.edit',
  'stock.view',
  'order.approve',
  'order.place',
  'document.view_confidential',
  'calendar.view',
];

const crewLead: Permission[] = [
  'event.view_assigned',
  'task.edit',
  'task.move',
  'admin_task.view',
  'admin_task.edit',
  'stock.view',
  'incident.create',
  'incident.view',
  'calendar.view',
];

const casualCrew: Permission[] = [
  'event.view_assigned',
  'task.edit',
  'task.move',
  'admin_task.view',
  'admin_task.edit',
  'incident.create',
  'incident.view',
  'calendar.view',
];

//----------------------------------------------------------\\
//                              ACCOUNTS
//----------------------------------------------------------\\

function account(
  id: number,
  fullName: string,
  email: string,
  role: MeResponse['roles'][number],
  permissions: Permission[],
) {
  return {
    user: {
      userId: `00000000-0000-0000-0000-00000000000${id}`,
      fullName,
      email,
      employmentType: role === 'CasualCrew' ? 'Casual' : 'Permanent',
    },
    roles: [role],
    permissions,
  } satisfies MeResponse;
}

export const users = {
  director: account(1, 'Director', 'director@example.com', 'Director', director),
  operationsManager: account(2, 'Ops Manager', 'ops@example.com', 'OperationsManager', operationsManager),
  eventManager: account(3, 'Sarah M.', 'sarah@example.com', 'EventManager', eventManager),
  accounts: account(4, 'Bookkeeper', 'bookkeeper@example.com', 'Accounts', accounts),
  crewLead: account(5, 'Thabo N.', 'thabo@example.com', 'CrewLead', crewLead),
  casualCrew: account(6, 'Priya R.', 'priya@example.com', 'CasualCrew', casualCrew),
};

//the same accounts as rows of GET /api/users. director and accounts sign in with a code (FR-34)
export function userList(): UserListItem[] {
  return Object.values(users).map(({ user, roles }, index) => ({
    ...user,
    employeeNumber: `CB-${String(index + 1).padStart(3, '0')}`,
    roles,
    isActive: true,
    mfaEnabled: roles.some((role) => role === 'Director' || role === 'Accounts'),
    lastLoginAt: null,
  }));
}
