import {
  Calendar,
  CalendarDays,
  ListChecks,
  Package,
  Receipt,
  Settings,
  SquareCheckBig,
  SquareKanban,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react';
import type { Permission, PermissionCheck } from '@/auth/permissions';

//----------------------------------------------------------\\
//                              ACCESS
//----------------------------------------------------------\\

//who may open each screen: any one of these codes is enough. the nav and the route guards
//both read from here so they can't drift apart
export const access = {
  events: ['event.view_all'],
  newEvent: ['event.create'],
  editEvent: ['event.edit'],
  //crew reach their own events here too, the api answers 404 for any they aren't assigned to
  eventDetail: ['event.view_all', 'event.view_assigned'],
  adminTasks: ['admin_task.view'],
  stock: ['stock.view'],
  finance: ['quote.view', 'invoice.view', 'order.approve'],
  quotes: ['quote.view'],
  calendar: ['calendar.view'],
  settings: ['user.manage', 'audit.view', 'calendar.connect', 'venue.edit'],
  myEvents: ['event.view_assigned'],
  myTasks: ['task.move', 'admin_task.view'],
  report: ['incident.create'],
} as const satisfies Record<string, readonly Permission[]>;

//----------------------------------------------------------\\
//                              NAV ITEMS
//----------------------------------------------------------\\

export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  anyOf: readonly Permission[];
};

//order is the prototype's sidebar order
export const deskNav: readonly NavItem[] = [
  { to: '/events', label: 'Events', icon: SquareKanban, anyOf: access.events },
  { to: '/admin-tasks', label: 'Admin Tasks', icon: SquareCheckBig, anyOf: access.adminTasks },
  { to: '/stock', label: 'Stock & Orders', icon: Package, anyOf: access.stock },
  { to: '/finance', label: 'Quotes & Invoices', icon: Receipt, anyOf: access.finance },
  { to: '/calendar', label: 'Calendar', icon: Calendar, anyOf: access.calendar },
  { to: '/settings', label: 'Settings', icon: Settings, anyOf: access.settings },
];

export const crewNav: readonly NavItem[] = [
  { to: '/my/events', label: 'My events', icon: CalendarDays, anyOf: access.myEvents },
  { to: '/my/tasks', label: 'My tasks', icon: ListChecks, anyOf: access.myTasks },
  { to: '/calendar', label: 'Calendar', icon: Calendar, anyOf: access.calendar },
  { to: '/my/report', label: 'Report', icon: TriangleAlert, anyOf: access.report },
];

export function visibleNav(items: readonly NavItem[], check: PermissionCheck) {
  return items.filter((item) => check.canAny(item.anyOf));
}

//----------------------------------------------------------\\
//                              LAYOUT AND LANDING
//----------------------------------------------------------\\

//anyone who sees every event works at a desk, everyone else gets the phone-first crew layout.
//accounts with several roles get the union, so a director who is also crew lead stays on desk
export function usesDeskLayout(check: PermissionCheck) {
  return check.can('event.view_all');
}

//each role starts on its primary task so it's within 3 steps (NFR-04)
export function landingPath(check: PermissionCheck) {
  if (check.can('event.create')) return '/events'; //director, ops, event manager
  if (check.canAny(['quote.view', 'invoice.view'])) return '/finance'; //accounts
  if (check.can('event.view_all')) return '/events';
  return '/my/events'; //crew
}
