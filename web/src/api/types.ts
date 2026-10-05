import type { Permission } from '@/auth/permissions';
import type { components } from './schema.gen';

//aliases over the types generated from docs/api/openapi.json (npm run gen:api), so a field the api
//renames or drops breaks the build here first. where the contract only says string (roles,
//permissions, card status), these narrow it to the values the api actually sends
type Schemas = components['schemas'];

//----------------------------------------------------------\\
//                              AUTH
//----------------------------------------------------------\\

export type RoleName =
  'Director' | 'OperationsManager' | 'EventManager' | 'Accounts' | 'CrewLead' | 'CasualCrew';

export type UserSummary = Omit<Schemas['UserSummary'], 'employmentType'> & {
  employmentType: Schemas['EmploymentType'];
};

//GET /api/me
export type MeResponse = Omit<Schemas['MeResponse'], 'user' | 'roles' | 'permissions'> & {
  user: UserSummary;
  roles: RoleName[];
  permissions: Permission[];
};

//the api sends one flat session shape with every field present: either a code is still owed or
//the user is in. director and accounts always get the code step first (FR-34)
export type MfaStep = Schemas['SessionResponse'] & {
  mfaRequired: true;
  mfaToken: string; //short-lived, held in memory between the password and the code
  accessToken: null;
  expiresInSeconds: null;
};

export type SignedInSession = Schemas['SessionResponse'] & {
  mfaRequired: false;
  mfaEnrolmentRequired: false;
  mfaToken: null;
  accessToken: string;
  expiresInSeconds: number;
};

//login can answer either way. mfa verify, mfa confirm and refresh only ever sign the user in
export type SessionResponse = MfaStep | SignedInSession;

//POST /api/auth/mfa/enrol
export type MfaEnrolResponse = Schemas['MfaEnrolResponse'];

//----------------------------------------------------------\\
//                              LISTS
//----------------------------------------------------------\\

//every list endpoint answers in this shape (plan section 5). the contract spells it out once per
//item type (PagedResultOfEventListItem and so on), this is the same thing written once
export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};

//----------------------------------------------------------\\
//                              EVENTS
//----------------------------------------------------------\\

export type EventStatus = Schemas['EventStatus'];
export type EventType = Schemas['EventType'];

//the two seeded divisions. the contract leaves divisionCode as a plain string
export type DivisionCode = 'CE' | 'CLM';

//one row of GET /api/events. no money here: the board doesn't show any, and the event detail is
//where masked price fields come in
export type EventListItem = Schemas['EventListItem'];

//GET /api/events/{id}/allowed-transitions
export type AllowedTransitions = Schemas['AllowedTransitionsResponse'];

//----------------------------------------------------------\\
//                              BOARDS
//----------------------------------------------------------\\

export type CardPriority = Schemas['CardPriority'];

//admin tasks follow their column (FR-19), event board cards are open or done (D-009)
export type CardStatus = 'Assigned' | 'InProgressOrNeedsReview' | 'Complete' | 'Open' | 'Done';

//a person as other records show them
export type UserRef = Schemas['UserRefDto'];

//one card on either board. no money lives on a card. the description is sanitised html, only ever
//rendered through SanitisedDescription. createdBy is, on an admin task, the manager who handed it
//out and signs it off (FR-20)
export type TaskCard = Omit<Schemas['CardDto'], 'status'> & { status: CardStatus };

//wipLimit is display only (FR-23)
export type BoardColumn = Omit<Schemas['ColumnDto'], 'cards'> & { cards: TaskCard[] };

//GET /api/boards/admin and GET /api/events/{id}/board. crew get only the cards assigned to them
export type Board = Omit<Schemas['BoardDto'], 'columns'> & { columns: BoardColumn[] };

//----------------------------------------------------------\\
//                              USERS
//----------------------------------------------------------\\

//one row of GET /api/users, which takes user.manage
export type UserListItem = Omit<Schemas['UserListItem'], 'roles'> & { roles: RoleName[] };
