import { describe, expect, it } from 'vitest';
import { users } from '@/test/fixtures/users';
import { afterLoginPath, nextQuery, safeNext } from './redirect';

describe('safeNext', () => {
  it.each(['/events', '/events/abc?tab=costing', '/my/tasks'])('lets %s through', (path) => {
    expect(safeNext(path)).toBe(path);
  });

  it.each([
    ['nothing', null],
    ['an absolute url', 'https://evil.example'],
    ['a protocol-relative url', '//evil.example'],
    ['a backslash trick', '/\\evil.example'],
    ['a relative path', 'events'],
    ['the login page itself', '/login'],
    ['an mfa page', '/login/mfa'],
  ])('refuses %s', (_label, next) => {
    expect(safeNext(next)).toBeNull();
  });
});

describe('afterLoginPath', () => {
  it('goes back to where the user was heading', () => {
    expect(afterLoginPath('/stock', users.eventManager)).toBe('/stock');
  });

  it("falls back to the role's start page", () => {
    expect(afterLoginPath(null, users.accounts)).toBe('/finance');
    expect(afterLoginPath('//evil.example', users.casualCrew)).toBe('/my/events');
  });
});

describe('nextQuery', () => {
  it('encodes a safe path and drops an unsafe one', () => {
    expect(nextQuery('/events?view=board')).toBe('?next=%2Fevents%3Fview%3Dboard');
    expect(nextQuery('https://evil.example')).toBe('');
  });
});
