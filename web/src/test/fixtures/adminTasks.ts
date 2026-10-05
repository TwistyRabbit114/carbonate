import type { Board, CardStatus, TaskCard, UserRef } from '@/api/types';
import { users } from './users';

//the four admin tasks from plan appendix d. due and sign-off dates are worked out relative to
//now, like the demo events, so nothing is overdue just because the demo is old

//----------------------------------------------------------\\
//                              HELPERS
//----------------------------------------------------------\\

const day = 24 * 3_600_000;
const daysFromNow = (days: number) => new Date(Date.now() + days * day).toISOString();

export function personRef(account: keyof typeof users): UserRef {
  return { userId: users[account].user.userId, fullName: users[account].user.fullName };
}

export const adminBoardId = 'ad000000-0000-0000-0000-000000000000';

export const adminColumnIds = {
  assigned: 'ad000000-0000-0000-0000-0000000000a0',
  review: 'ad000000-0000-0000-0000-0000000000a1',
  complete: 'ad000000-0000-0000-0000-0000000000a2',
};

type CardFields = Partial<TaskCard> & Pick<TaskCard, 'subject' | 'assignees'>;

function card(id: number, columnId: string, position: number, status: CardStatus, fields: CardFields): TaskCard {
  return {
    cardId: `ad000000-0000-0000-0000-00000000000${id}`,
    boardId: adminBoardId,
    columnId,
    description: null,
    priority: 'Normal',
    dueAt: null,
    position,
    status,
    milestoneId: null,
    createdBy: personRef('operationsManager'),
    reviewNotes: null,
    returnedBy: null,
    returnedAt: null,
    completedAt: null,
    attachmentCount: 0,
    rowVersion: btoa(`admin-card-${id}-v1`),
    ...fields,
  };
}

//----------------------------------------------------------\\
//                              BOARD
//----------------------------------------------------------\\

export function demoAdminBoard(): Board {
  return {
    boardId: adminBoardId,
    boardType: 'Admin',
    eventId: null,
    name: 'Admin Tasks',
    columns: [
      {
        columnId: adminColumnIds.assigned,
        name: 'Assigned',
        position: 0,
        wipLimit: null,
        isDoneColumn: false,
        cards: [
          card(1, adminColumnIds.assigned, 0, 'Assigned', {
            subject: 'Renew liquor licence for the Riverside venue',
            priority: 'High',
            assignees: [personRef('crewLead')],
            dueAt: daysFromNow(10),
            description:
              "<p>Riverside Grounds' liquor licence runs out at the end of the month. Submit the renewal on the municipal portal and attach proof of submission before the Riverlight Festival load-in.</p>",
          }),
          card(2, adminColumnIds.assigned, 1, 'Assigned', {
            subject: 'Update PPE stock list',
            assignees: [personRef('casualCrew')],
            dueAt: daysFromNow(14),
          }),
        ],
      },
      {
        columnId: adminColumnIds.review,
        name: 'In Progress / Needs Review',
        position: 1,
        wipLimit: null,
        isDoneColumn: false,
        cards: [
          card(3, adminColumnIds.review, 0, 'InProgressOrNeedsReview', {
            subject: 'Quarterly ice machine service',
            assignees: [personRef('crewLead')],
            dueAt: daysFromNow(3),
          }),
        ],
      },
      {
        columnId: adminColumnIds.complete,
        name: 'Complete',
        position: 2,
        wipLimit: null,
        isDoneColumn: true,
        cards: [
          card(4, adminColumnIds.complete, 0, 'Complete', {
            subject: 'Health & safety file for the October audit',
            assignees: [personRef('casualCrew')],
            createdBy: personRef('director'),
            completedAt: daysFromNow(-5),
          }),
        ],
      },
    ],
  };
}
