import type { EventListItem, EventStatus } from '@/api/types';
import { stageLabels } from './labels';

//----------------------------------------------------------\\
//                              COLUMNS
//----------------------------------------------------------\\

//the three active stages (FR-18). enquired and cancelled events have no column, so they can
//never show here even if a response included one (FR-03)
export const boardColumns = [
  { status: 'ConfirmedInPlanning', label: stageLabels.ConfirmedInPlanning, automatic: false },
  { status: 'InProgress', label: stageLabels.InProgress, automatic: true },
  { status: 'Finished', label: stageLabels.Finished, automatic: true },
] as const satisfies ReadonlyArray<{ status: EventStatus; label: string; automatic: boolean }>;

export type BoardStatus = (typeof boardColumns)[number]['status'];

//----------------------------------------------------------\\
//                              GROUPING
//----------------------------------------------------------\\

const byStart = (a: EventListItem, b: EventListItem) => a.startsAt.localeCompare(b.startsAt);
const byEnd = (a: EventListItem, b: EventListItem) => a.endsAt.localeCompare(b.endsAt);

//upcoming work soonest first, live events by when they end, finished ones newest first
export function groupByStage(events: readonly EventListItem[]) {
  const pick = (status: BoardStatus) => events.filter((event) => event.status === status);
  return {
    ConfirmedInPlanning: pick('ConfirmedInPlanning').sort(byStart),
    InProgress: pick('InProgress').sort(byEnd),
    Finished: pick('Finished').sort((a, b) => byEnd(b, a)),
  } satisfies Record<BoardStatus, EventListItem[]>;
}
