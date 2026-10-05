import type { Announcements } from '@dnd-kit/core';
import type { PlacedTask } from './rules';

export const taskDragInstructions =
  'To move this task with the keyboard, press space to pick it up, use the arrow keys to choose a column, then press space to drop it. Press escape to cancel.';

//what a screen reader hears during a drag, in task subjects and column names rather than ids
export function taskAnnouncements(
  findTask: (id: string) => PlacedTask | undefined,
  columnName: (columnId: string) => string,
): Announcements {
  const subject = (id: string | number) => findTask(String(id))?.card.subject ?? 'The task';
  const home = (id: string | number) => findTask(String(id))?.columnName ?? 'its column';
  const moved = (id: string | number, overId: string | number) =>
    overId !== findTask(String(id))?.card.columnId;

  return {
    onDragStart: ({ active }) => `Picked up ${subject(active.id)}, currently in ${home(active.id)}.`,
    onDragOver: ({ active, over }) =>
      over
        ? `${subject(active.id)} is over ${columnName(String(over.id))}.`
        : `${subject(active.id)} isn't over a column it can move to.`,
    onDragEnd: ({ active, over }) =>
      over && moved(active.id, over.id)
        ? `Moving ${subject(active.id)} to ${columnName(String(over.id))}.`
        : `${subject(active.id)} stays in ${home(active.id)}.`,
    onDragCancel: ({ active }) => `Move cancelled. ${subject(active.id)} stays in ${home(active.id)}.`,
  };
}
