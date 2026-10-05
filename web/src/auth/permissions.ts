//----------------------------------------------------------\\
//                              CODES
//----------------------------------------------------------\\

//codes from the permission matrix in the project plan (section 7.4). the api and its seed
//data are the source of truth, this list just gives the spa typo-proof names
export const permissionCodes = [
  'event.view_all',
  'event.view_assigned',
  'event.create',
  'event.edit',
  'event.delete',
  'event.transition',
  'crew.assign',
  'finance.view_client_price',
  'finance.view_internal_cost',
  'finance.view_margin',
  'finance.view_staff_cost',
  'quote.view',
  'quote.edit',
  'quote.approve',
  'confirmation.record',
  'invoice.view',
  'invoice.manage',
  'reconciliation.edit',
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
  'order.approve',
  'order.place',
  'incident.create',
  'incident.view',
  'document.upload',
  'document.view_confidential',
  'calendar.view',
  'calendar.connect',
  'user.manage',
  'audit.view',
] as const;

export type Permission = (typeof permissionCodes)[number];

//----------------------------------------------------------\\
//                              CHECKS
//----------------------------------------------------------\\

export type PermissionCheck = {
  can: (code: Permission) => boolean;
  canAny: (codes: readonly Permission[]) => boolean;
};

//permissions only decide layout, navigation and which buttons show. hiding data is the api's job
export function permissionCheck(granted: readonly string[]): PermissionCheck {
  const set = new Set(granted);
  return {
    can: (code) => set.has(code),
    canAny: (codes) => codes.some((code) => set.has(code)),
  };
}
