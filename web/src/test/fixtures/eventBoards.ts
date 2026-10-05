import type {
  Board,
  BoardColumn,
  CardPriority,
  EventListItem,
  MilestoneType,
  TaskCard,
  UserRef,
} from '@/api/types';
import { personRef } from './adminTasks';
import { demoMilestones, milestoneOf } from './eventDetails';

//each demo event's task board, the way D's seeder would lay one out from a template (FR-25): three
//columns with a limit on the middle one, and cards due a set time before or after a milestone.
//placeholder template content until the client's real checklists are in the seed data

//----------------------------------------------------------\\
//                              TEMPLATE
//----------------------------------------------------------\\

type TemplateCard = {
  subject: string;
  priority?: CardPriority;
  milestone: MilestoneType;
  offsetHours: number;
  assignees: UserRef[];
  description?: string;
  column: 0 | 1; //where it starts while the event is still ahead
};

const thabo = personRef('crewLead');
const priya = personRef('casualCrew');

const templateCards: TemplateCard[] = [
  {
    subject: 'Recce the venue and loading bay',
    milestone: 'SiteVisit',
    offsetHours: 0,
    assignees: [thabo],
    column: 0,
  },
  {
    subject: 'Confirm ice delivery with Coastal Ice',
    priority: 'High',
    milestone: 'LoadIn',
    offsetHours: -24,
    assignees: [thabo],
    description:
      '<p>Coastal Ice needs <strong>5 days</strong> notice. Confirm the drop time against load-in.</p>',
    column: 0,
  },
  {
    subject: 'Pack bar kit and glassware',
    milestone: 'LoadIn',
    offsetHours: -4,
    assignees: [priya],
    description:
      '<p>From the store room:</p><ul><li>Bar kit, two sets</li><li>Glassware crates</li><li>Ice buckets</li></ul>',
    column: 0,
  },
  {
    subject: 'Print the run sheet',
    milestone: 'LoadIn',
    offsetHours: -2,
    assignees: [],
    column: 0,
  },
  {
    subject: 'Set up the bar',
    priority: 'Critical',
    milestone: 'Doors',
    offsetHours: -2,
    assignees: [thabo],
    column: 1,
  },
  {
    subject: 'Brief the bar team on the menu',
    milestone: 'Doors',
    offsetHours: -1,
    assignees: [thabo, priya],
    column: 1,
  },
];

//----------------------------------------------------------\\
//                              BOARD
//----------------------------------------------------------\\

export const eventBoardIdFor = (eventId: string) => `b0a00000-0000-0000-0000-00000000000${eventId.slice(-1)}`;

export function demoEventBoard(event: EventListItem): Board {
  const number = event.eventId.slice(-1);
  const boardId = eventBoardIdFor(event.eventId);
  const milestones = demoMilestones(event);
  const columnId = (position: number) => `c01a000${number}-0000-0000-0000-00000000000${position}`;
  const columns: BoardColumn[] = [
    { columnId: columnId(0), name: 'To do', position: 0, wipLimit: null, isDoneColumn: false, cards: [] },
    { columnId: columnId(1), name: 'Doing', position: 1, wipLimit: 2, isDoneColumn: false, cards: [] },
    { columnId: columnId(2), name: 'Done', position: 2, wipLimit: null, isDoneColumn: true, cards: [] },
  ];

  templateCards.forEach((template, index) => {
    const milestone = milestoneOf(milestones, template.milestone);
    const dueAt = new Date(new Date(milestone.scheduledStart).getTime() + template.offsetHours * 3_600_000);
    //a finished event's cards are all done, and so is anything due before now on a live one
    const done = event.status === 'Finished' || dueAt.getTime() < Date.now();
    const column = columns[done ? 2 : template.column]!;

    const card: TaskCard = {
      cardId: `ca000000-0000-0000-000${number}-00000000000${index}`,
      boardId,
      columnId: column.columnId,
      subject: template.subject,
      description: template.description ?? null,
      priority: template.priority ?? 'Normal',
      dueAt: dueAt.toISOString(),
      position: column.cards.length,
      status: done ? 'Done' : 'Open',
      milestoneId: milestone.milestoneId,
      assignees: template.assignees,
      createdBy: personRef('eventManager'),
      reviewNotes: null,
      returnedBy: null,
      returnedAt: null,
      completedAt: done ? dueAt.toISOString() : null,
      attachmentCount: index === 1 ? 1 : 0,
      rowVersion: btoa(`event-card-${number}-${index}-v1`),
    };
    column.cards.push(card);
  });

  return { boardId, boardType: 'Event', eventId: event.eventId, name: 'Activation checklist', columns };
}
