import { describe, expect, it } from 'vitest';
import { permissionCheck } from '@/auth/permissions';
import { users } from '@/test/fixtures/users';
import { crewNav, deskNav, landingPath, usesDeskLayout, visibleNav } from './navigation';

const checkFor = (user: keyof typeof users) => permissionCheck(users[user].permissions);
const deskLabels = (user: keyof typeof users) =>
  visibleNav(deskNav, checkFor(user)).map((item) => item.label);

//----------------------------------------------------------\\
//                              NAV BY ROLE
//----------------------------------------------------------\\

describe('desk navigation', () => {
  it('gives the director all six items', () => {
    expect(deskLabels('director')).toEqual([
      'Events',
      'Admin Tasks',
      'Stock & Orders',
      'Quotes & Invoices',
      'Calendar',
      'Settings',
    ]);
  });

  it('gives ops no quotes and invoices, since ops has no finance permissions', () => {
    expect(deskLabels('operationsManager')).toEqual([
      'Events',
      'Admin Tasks',
      'Stock & Orders',
      'Calendar',
      'Settings',
    ]);
  });

  it('gives the event manager settings for venues', () => {
    expect(deskLabels('eventManager')).toContain('Settings');
  });

  it('gives accounts finance but no settings', () => {
    expect(deskLabels('accounts')).toEqual([
      'Events',
      'Admin Tasks',
      'Stock & Orders',
      'Quotes & Invoices',
      'Calendar',
    ]);
  });
});

describe('crew navigation', () => {
  it('gives crew their four tabs', () => {
    expect(visibleNav(crewNav, checkFor('casualCrew')).map((item) => item.label)).toEqual([
      'My events',
      'My tasks',
      'Calendar',
      'Report',
    ]);
  });
});

//----------------------------------------------------------\\
//                              LAYOUT AND LANDING
//----------------------------------------------------------\\

describe('layout', () => {
  it.each(['director', 'operationsManager', 'eventManager', 'accounts'] as const)(
    '%s works at a desk',
    (user) => {
      expect(usesDeskLayout(checkFor(user))).toBe(true);
    },
  );

  it.each(['crewLead', 'casualCrew'] as const)('%s gets the crew layout', (user) => {
    expect(usesDeskLayout(checkFor(user))).toBe(false);
  });

  it('keeps someone with a desk role and a crew role on the desk layout', () => {
    const both = permissionCheck([...users.director.permissions, ...users.crewLead.permissions]);
    expect(usesDeskLayout(both)).toBe(true);
  });
});

describe('landing page', () => {
  it.each([
    ['director', '/events'],
    ['operationsManager', '/events'],
    ['eventManager', '/events'],
    ['accounts', '/finance'],
    ['crewLead', '/my/events'],
    ['casualCrew', '/my/events'],
  ] as const)('%s starts on %s', (user, path) => {
    expect(landingPath(checkFor(user))).toBe(path);
  });
});
