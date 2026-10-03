import type { Permission } from '@/auth/permissions';

//hand-written from the plan's auth section (7.1) until docs/api/openapi.json is committed.
//once it is, run npm run gen:api and turn these into aliases over the generated schema

export type RoleName =
  'Director' | 'OperationsManager' | 'EventManager' | 'Accounts' | 'CrewLead' | 'CasualCrew';

export type UserSummary = {
  userId: string;
  fullName: string;
  email: string;
};

//GET /api/me
export type MeResponse = {
  user: UserSummary;
  roles: RoleName[];
  permissions: Permission[];
};
