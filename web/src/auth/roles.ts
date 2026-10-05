import type { RoleName } from '@/api/types';

//labels shown next to the user's name, an account with several roles lists them all
export const roleLabels: Record<RoleName, string> = {
  Director: 'Director',
  OperationsManager: 'Operations Manager',
  EventManager: 'Event Manager',
  Accounts: 'Accounts',
  CrewLead: 'Crew Lead',
  CasualCrew: 'Casual Crew',
};

export function describeRoles(roles: readonly RoleName[]) {
  return roles.map((role) => roleLabels[role]).join(', ');
}

//"Sarah M." becomes "SM" for the avatar circle
export function initials(fullName: string) {
  return fullName
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();
}
