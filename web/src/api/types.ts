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

//----------------------------------------------------------\\
//                              LISTS
//----------------------------------------------------------\\

//every list endpoint answers in this shape (plan section 5)
export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};

//----------------------------------------------------------\\
//                              EVENTS
//----------------------------------------------------------\\

export type EventStatus = 'Enquired' | 'ConfirmedInPlanning' | 'InProgress' | 'Finished' | 'Cancelled';
export type EventType = 'Activation' | 'Corporate' | 'Wedding' | 'Festival' | 'YearEnd' | 'Private';
export type DivisionCode = 'CE' | 'CLM';

//one row of GET /api/events. no money here: the board doesn't show any, and the event
//detail is where masked price fields come in
//TODO(plan): list item field names (venueName, divisionCode) are assumed, confirm with C
export type EventListItem = {
  eventId: string;
  eventCode: string;
  name: string;
  status: EventStatus;
  eventType: EventType;
  divisionCode: DivisionCode;
  eventDate: string; //yyyy-MM-dd
  startsAt: string; //utc instant, the live window that drives automatic stage moves
  endsAt: string;
  venueName: string;
  packSizeEstimated: number;
  packSizeActual?: number | null;
  isConfidential: boolean;
  rowVersion: string;
};
