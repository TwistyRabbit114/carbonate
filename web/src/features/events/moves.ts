import type { Announcements } from '@dnd-kit/core';
import { isApiError } from '@/api/problem';
import type { EventListItem, EventStatus } from '@/api/types';
import { stageLabels } from './labels';

//----------------------------------------------------------\\
//                              ERRORS
//----------------------------------------------------------\\

//plain words for why a move didn't happen. the card has already gone back by the time this shows
export function describeMoveError(error: unknown, eventName: string) {
  if (!isApiError(error)) return `Something went wrong, so ${eventName} wasn't moved.`;
  if (error.status === 0) return `Couldn't reach Carbonate, so ${eventName} wasn't moved.`;
  if (error.status === 409 && error.problem.type?.endsWith('/concurrency-conflict')) {
    return `Someone else changed ${eventName} just now, so it wasn't moved. The board shows their change.`;
  }
  //the server's own explanation for a move the lifecycle doesn't allow
  if (error.status === 409 || error.status === 422) {
    return error.problem.detail ?? `${eventName} can't move to that stage.`;
  }
  if (error.status === 403) return "Your role can't move events between stages.";
  if (error.status === 404) return `${eventName} isn't on the board any more.`;
  return `Something went wrong, so ${eventName} wasn't moved.`;
}

//----------------------------------------------------------\\
//                              ANNOUNCEMENTS
//----------------------------------------------------------\\

export const dragInstructions =
  'To move this event with the keyboard, press space to pick it up, use the arrow keys to choose a stage, then press space to drop it. Press escape to cancel.';

//what a screen reader hears during a drag, in event names and stage names rather than ids
export function moveAnnouncements(findEvent: (id: string) => EventListItem | undefined): Announcements {
  const name = (id: string | number) => findEvent(String(id))?.name ?? 'The event';
  const stage = (id: string | number) => stageLabels[id as EventStatus] ?? String(id);
  const home = (id: string | number) => stage(findEvent(String(id))?.status ?? '');

  return {
    onDragStart: ({ active }) => `Picked up ${name(active.id)}, currently in ${home(active.id)}.`,
    onDragOver: ({ active, over }) =>
      over
        ? `${name(active.id)} is over ${stage(over.id)}.`
        : `${name(active.id)} isn't over a stage it can move to.`,
    onDragEnd: ({ active, over }) =>
      over && over.id !== findEvent(String(active.id))?.status
        ? `Moving ${name(active.id)} to ${stage(over.id)}.`
        : `${name(active.id)} stays in ${home(active.id)}.`,
    onDragCancel: ({ active }) => `Move cancelled. ${name(active.id)} stays in ${home(active.id)}.`,
  };
}
