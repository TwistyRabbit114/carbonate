import { useState } from 'react';
import type { EventDetail, Milestone, MilestoneType } from '@/api/types';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Item, ItemList } from '@/components/ItemList';
import { Panel } from '@/components/Panel';
import { useMilestones } from '@/features/event-board/api';
import { formatDateTime, formatSpan } from '@/lib/format';
import { milestoneLabels } from './labels';
import { RescheduleDialog } from './RescheduleDialog';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type MilestonesPanelProps = {
  event: EventDetail;
  canReschedule: boolean;
  only?: readonly MilestoneType[]; //crew only need the ones that happen on site
};

//moving a milestone that has already started is refused by the api, so the button isn't offered.
//a finished or cancelled event's dates are history
const reschedulable = (event: EventDetail, milestone: Milestone) =>
  !milestone.actualStart &&
  (event.status === 'ConfirmedInPlanning' || event.status === 'Enquired' || event.status === 'InProgress');

//----------------------------------------------------------\\
//                              PANEL
//----------------------------------------------------------\\

//the chain from recce to reconciliation, in order (FR-04)
export function MilestonesPanel({ event, canReschedule, only }: MilestonesPanelProps) {
  const milestones = useMilestones(event.eventId);
  const [moving, setMoving] = useState<Milestone | null>(null);

  const shown = [...(milestones.data ?? [])]
    .filter((milestone) => !only || only.includes(milestone.milestoneType))
    .sort((a, b) => a.scheduledStart.localeCompare(b.scheduledStart));

  return (
    <Panel title="Milestones">
      {milestones.isError ? (
        <ErrorState message="We couldn't load the milestones." onRetry={() => void milestones.refetch()} />
      ) : milestones.isPending ? (
        <p role="status">Loading milestones…</p>
      ) : shown.length === 0 ? (
        <p>No milestones yet.</p>
      ) : (
        <ItemList label="Milestones in order">
          {shown.map((milestone) => {
            const label = milestoneLabels[milestone.milestoneType];
            return (
              <Item
                key={milestone.milestoneId}
                title={label}
                badge={statusBadge(milestone)}
                meta={timesOf(milestone)}
                actions={
                  canReschedule &&
                  reschedulable(event, milestone) && (
                    <Button onClick={() => setMoving(milestone)} aria-label={`Reschedule ${label}`}>
                      Reschedule
                    </Button>
                  )
                }
              />
            );
          })}
        </ItemList>
      )}

      <RescheduleDialog
        event={event}
        milestone={moving}
        milestones={milestones.data ?? []}
        onClose={() => setMoving(null)}
      />
    </Panel>
  );
}

function statusBadge(milestone: Milestone) {
  if (milestone.status === 'Done') return <Badge>Done</Badge>;
  if (milestone.status === 'InProgress') return <Badge tone="auto">Under way</Badge>;
  return undefined;
}

//once something has happened, when it really happened is what matters
function timesOf(milestone: Milestone) {
  if (milestone.actualStart && milestone.actualEnd) {
    return `Happened ${formatSpan(milestone.actualStart, milestone.actualEnd)}`;
  }
  if (milestone.actualStart) return `Started ${formatDateTime(milestone.actualStart)}`;
  return formatSpan(milestone.scheduledStart, milestone.scheduledEnd);
}
