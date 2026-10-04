import {
  pointerWithin,
  rectIntersection,
  type Announcements,
  type CollisionDetection,
  type KeyboardCoordinateGetter,
} from '@dnd-kit/core';
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
//                              DRAGGING
//----------------------------------------------------------\\

//a mouse or finger counts as over whichever column it's inside. keyboard drags have no pointer,
//so they fall back to whichever column the card overlaps
export const columnCollision: CollisionDetection = (args) =>
  args.pointerCoordinates ? pointerWithin(args) : rectIntersection(args);

//arrow keys jump a whole column at a time instead of nudging the card a few pixels. only columns
//the card may drop into are stops, in screen order: across on desktop, down when stacked on phones
export const columnKeyboardCoordinates: KeyboardCoordinateGetter = (event, { context }) => {
  const forward = event.code === 'ArrowRight' || event.code === 'ArrowDown';
  const backward = event.code === 'ArrowLeft' || event.code === 'ArrowUp';
  if (!forward && !backward) return undefined;
  event.preventDefault();

  const { collisionRect, droppableContainers, droppableRects, over } = context;
  if (!collisionRect) return undefined;

  const stops = droppableContainers
    .getEnabled()
    .flatMap((container) => {
      const rect = droppableRects.get(container.id);
      return rect ? [{ id: container.id, rect }] : [];
    })
    .sort((a, b) => a.rect.left - b.rect.left || a.rect.top - b.rect.top);

  const current = stops.findIndex((stop) => stop.id === over?.id);
  const next = stops[current + (forward ? 1 : -1)];
  if (!next) return undefined;

  return {
    x: next.rect.left + next.rect.width / 2 - collisionRect.width / 2,
    y: next.rect.top + 8,
  };
};

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
