import type { Milestone } from '@/api/types';
import { milestoneLabels } from '@/features/events/labels';

const minute = 60_000;
const hour = 60 * minute;
const day = 24 * hour;

//"2h", "1h 30m", "45m", or whole days once it's two days or more
function amount(ms: number) {
  if (ms >= 2 * day) {
    const days = Math.round(ms / day);
    return `${days} days`;
  }
  const totalMinutes = Math.round(ms / minute);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours === 0) return `${minutes}m`;
  return minutes === 0 ? `${hours}h` : `${hours}h ${minutes}m`;
}

//a card's due time against the milestone it hangs off, the way crew talk about it: "2h before load-in".
//templates set card due times as an offset from a milestone, so this is the number the crew know
export function dueAgainstMilestone(dueAt: string, milestone: Milestone): string {
  const name = milestoneLabels[milestone.milestoneType].toLowerCase();
  const gap = new Date(dueAt).getTime() - new Date(milestone.scheduledStart).getTime();
  if (Math.abs(gap) < minute) return `At ${name}`;
  return `${amount(Math.abs(gap))} ${gap < 0 ? 'before' : 'after'} ${name}`;
}
