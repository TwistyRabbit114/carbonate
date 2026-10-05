import type { Permission } from '@/auth/permissions';

//hand-written to match the api's contracts until docs/api/openapi.json is committed.
//once it is, run npm run gen:api and turn these into aliases over the generated schema

//----------------------------------------------------------\\
//                              AUTH
//----------------------------------------------------------\\

export type RoleName =
  'Director' | 'OperationsManager' | 'EventManager' | 'Accounts' | 'CrewLead' | 'CasualCrew';

export type UserSummary = {
  userId: string;
  fullName: string;
  email: string;
  employmentType: 'Permanent' | 'Casual';
};

//GET /api/me
export type MeResponse = {
  user: UserSummary;
  roles: RoleName[];
  permissions: Permission[];
};

//the api sends one session shape with every field present: either a code is still owed or
//the user is in. director and accounts always get the code step first (FR-34)
export type MfaStep = {
  mfaRequired: true;
  mfaEnrolmentRequired: boolean; //first login, the authenticator app isn't set up yet
  mfaToken: string; //short-lived, held in memory between the password and the code
  accessToken: null;
  expiresInSeconds: null;
};

export type SignedInSession = {
  mfaRequired: false;
  mfaEnrolmentRequired: false;
  mfaToken: null;
  accessToken: string;
  expiresInSeconds: number;
};

//login can answer either way. mfa verify, mfa confirm and refresh only ever sign the user in
export type SessionResponse = MfaStep | SignedInSession;

//POST /api/auth/mfa/enrol
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

//----------------------------------------------------------\\
//                              BOARDS
//----------------------------------------------------------\\

export type CardPriority = 'Low' | 'Normal' | 'High' | 'Critical';

//admin tasks follow their column (FR-19), event board cards are open or done (D-009)
export type CardStatus = 'Assigned' | 'InProgressOrNeedsReview' | 'Complete' | 'Open' | 'Done';

//a person as other records show them
export type UserRef = {
  userId: string;
  fullName: string;
};

//one card on either board. no money lives on a card
export type TaskCard = {
  cardId: string;
  boardId: string;
  columnId: string;
  subject: string;
  description: string | null; //sanitised html, only ever rendered through SanitisedDescription
  priority: CardPriority;
  dueAt: string | null;
  position: number;
  status: CardStatus;
  milestoneId: string | null;
  assignees: UserRef[];
  createdBy: UserRef; //on an admin task, the manager who handed it out and signs it off (FR-20)
  reviewNotes: string | null;
  returnedBy: UserRef | null;
  returnedAt: string | null;
  completedAt: string | null;
  attachmentCount: number;
  rowVersion: string;
};

export type BoardColumn = {
  columnId: string;
  name: string;
  position: number;
  wipLimit: number | null; //display only (FR-23)
  isDoneColumn: boolean;
  cards: TaskCard[];
};

//GET /api/boards/admin and GET /api/events/{id}/board. crew get only the cards assigned to them
export type Board = {
  boardId: string;
  boardType: 'Event' | 'Admin';
  eventId: string | null;
  name: string;
  columns: BoardColumn[];
};

//----------------------------------------------------------\\
//                              USERS
//----------------------------------------------------------\\

//one row of GET /api/users, which takes user.manage
//TODO(plan): row shape and the isActive filter are assumed until C's users endpoint lands
export type UserListItem = UserSummary & {
  roles: RoleName[];
  isActive: boolean;
};
