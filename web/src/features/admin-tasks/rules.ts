import type { BoardColumn, CardStatus, TaskCard } from '@/api/types';

//----------------------------------------------------------\\
//                              STAGES
//----------------------------------------------------------\\

//the admin board's three columns, by the position the api gives them (FR-19)
export type AdminStage = 'assigned' | 'review' | 'complete';

const stagesByPosition: readonly AdminStage[] = ['assigned', 'review', 'complete'];

export function stageOf(column: BoardColumn): AdminStage | undefined {
  return stagesByPosition[column.position];
}

//the same stage read off the card's status, for a page that has the card but not its column.
//an event board card (open or done) has none
const stagesByStatus: Partial<Record<CardStatus, AdminStage>> = {
  Assigned: 'assigned',
  InProgressOrNeedsReview: 'review',
  Complete: 'complete',
};

export function stageOfCard(card: TaskCard): AdminStage | undefined {
  return stagesByStatus[card.status];
}

export const stageNames: Record<AdminStage, string> = {
  assigned: 'Assigned',
  review: 'In Progress / Needs Review',
  complete: 'Complete',
};

//----------------------------------------------------------\\
//                              WHO CAN DO WHAT
//----------------------------------------------------------\\

export type TaskAction = 'handIn' | 'complete' | 'return';

//a card with the column it's in and the steps this user can take with it
export type PlacedTask = {
  card: TaskCard;
  stage: AdminStage | undefined;
  columnName: string;
  actions: TaskAction[];
};

//where each action leaves the card
export const actionTarget: Record<TaskAction, AdminStage> = {
  handIn: 'review',
  complete: 'complete',
  return: 'assigned',
};

//a copy of the api's AdminTaskRules, used only to decide which buttons and drop targets show.
//the api checks every request again and has the last word (FR-20)
export function actionsFor(
  card: TaskCard,
  stage: AdminStage | undefined,
  me: string,
  canReview: boolean,
): TaskAction[] {
  const isAssignee = card.assignees.some((person) => person.userId === me);
  if (stage === 'assigned') return isAssignee ? ['handIn'] : [];

  //the manager who handed it out signs it off, never someone it's assigned to
  const signsOff = canReview && card.createdBy.userId === me && !isAssignee;
  if (stage === 'review' && signsOff) return ['complete', 'return'];
  return [];
}
