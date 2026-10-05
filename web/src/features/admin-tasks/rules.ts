import { isApiError } from '@/api/problem';
import type { BoardColumn, TaskCard } from '@/api/types';

//----------------------------------------------------------\\
//                              STAGES
//----------------------------------------------------------\\

//the admin board's three columns, by the position the api gives them (FR-19)
export type AdminStage = 'assigned' | 'review' | 'complete';

const stagesByPosition: readonly AdminStage[] = ['assigned', 'review', 'complete'];

export function stageOf(column: BoardColumn): AdminStage | undefined {
  return stagesByPosition[column.position];
}

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

//----------------------------------------------------------\\
//                              ERRORS
//----------------------------------------------------------\\

//plain words for why a step didn't happen. the card is already back where it was when this shows
export function describeTaskError(error: unknown, subject: string) {
  if (!isApiError(error)) return `Something went wrong, so nothing changed on ${subject}.`;
  if (error.status === 0) return `Couldn't reach Carbonate, so nothing changed on ${subject}.`;
  if (error.status === 409 && error.problem.type?.endsWith('/concurrency-conflict')) {
    return `Someone else changed ${subject} just now, so nothing changed. The board shows their change.`;
  }
  //the api says why: not theirs to sign off, or not at that stage yet
  if ([403, 409, 422].includes(error.status) && error.problem.detail) return error.problem.detail;
  if (error.status === 403) return `Your role can't do that on ${subject}.`;
  if (error.status === 404) return `${subject} isn't on the board any more.`;
  return `Something went wrong, so nothing changed on ${subject}.`;
}
