import type { MeResponse } from '@/api/types';
import { landingPath } from '@/app/navigation';
import { permissionCheck } from './permissions';

//?next= comes from the address bar, so only same-site paths are allowed through.
//"//evil.com" and "/\evil.com" are protocol-relative and would leave the site
export function safeNext(next: string | null) {
  if (!next || !next.startsWith('/')) return null;
  if (next.startsWith('//') || next.startsWith('/\\')) return null;
  if (next === '/login' || next.startsWith('/login/') || next.startsWith('/login?')) return null;
  return next;
}

export function afterLoginPath(next: string | null, me: MeResponse) {
  return safeNext(next) ?? landingPath(permissionCheck(me.permissions));
}

//carries ?next= across the login and mfa screens, dropping anything else like ?reason=
export function nextQuery(next: string | null) {
  const safe = safeNext(next);
  return safe ? `?next=${encodeURIComponent(safe)}` : '';
}
