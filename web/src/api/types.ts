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

//POST /api/auth/login. director and accounts get an mfa step instead of a token
//TODO(plan): how does login say that mfa still needs enrolling on first login? assumed a flag, confirm with C
export type LoginResponse =
  | { accessToken: string; user: UserSummary }
  | { mfaRequired: true; mfaToken: string; mfaEnrolmentRequired?: boolean };

//POST /api/auth/mfa/verify and /api/auth/mfa/confirm
export type TokenResponse = {
  accessToken: string;
};

//POST /api/auth/mfa/enrol
//TODO(plan): field name for the otpauth:// uri is assumed, confirm with C
export type MfaEnrolResponse = {
  otpauthUri: string;
};
