import { pointerWithin, rectIntersection, type CollisionDetection, type KeyboardCoordinateGetter } from '@dnd-kit/core';

//shared by both boards: cards drop into whole columns, never between other cards

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
