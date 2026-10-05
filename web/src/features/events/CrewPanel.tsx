import { useEffect, useRef, useState } from 'react';
import { Plus } from 'lucide-react';
import type { CrewAssignment, EventDetail } from '@/api/types';
import { useSignedIn } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { ErrorState } from '@/components/ErrorState';
import { Item, ItemList } from '@/components/ItemList';
import { Panel } from '@/components/Panel';
import { useEventCrew } from '@/features/event-board/api';
import { formatMoney, formatSpan } from '@/lib/format';
import { useRemoveCrew } from './api';
import { AssignCrewDialog } from './AssignCrewDialog';

type CrewPanelProps = {
  event: EventDetail;
  canAssign: boolean;
};

//who's working the event and when (FR-07). an hourly rate is $staff: it's only in the response
//for director and accounts, or on the viewer's own shift, so it shows only when it's there
export function CrewPanel({ event, canAssign }: CrewPanelProps) {
  const me = useSignedIn().user.userId;
  const crew = useEventCrew(event.eventId);
  const remove = useRemoveCrew(event.eventId);
  const [assigning, setAssigning] = useState(false);
  const [removing, setRemoving] = useState<CrewAssignment | null>(null);

  const shifts = [...(crew.data ?? [])].sort((a, b) => a.shiftStart.localeCompare(b.shiftStart));
  const open = event.status !== 'Finished' && event.status !== 'Cancelled';

  return (
    <Panel
      title="Crew"
      actions={
        canAssign &&
        open && (
          <Button onClick={() => setAssigning(true)}>
            <Plus aria-hidden="true" />
            Assign crew
          </Button>
        )
      }
    >
      {crew.isError ? (
        <ErrorState message="We couldn't load the crew." onRetry={() => void crew.refetch()} />
      ) : crew.isPending ? (
        <p role="status">Loading the crew…</p>
      ) : shifts.length === 0 ? (
        <p>Nobody on the crew yet.</p>
      ) : (
        <ItemList label="Crew shifts">
          {shifts.map((shift) => (
            <Item
              key={shift.assignmentId}
              title={shift.userId === me ? `${shift.fullName} (you)` : shift.fullName}
              badge={!shift.confirmed && <Badge tone="warning">Not confirmed</Badge>}
              meta={[shift.crewRole, formatSpan(shift.shiftStart, shift.shiftEnd)].join(' · ')}
              actions={
                canAssign &&
                open && (
                  <Button
                    variant="ghost"
                    onClick={() => setRemoving(shift)}
                    aria-label={`Take ${shift.fullName} off the crew`}
                  >
                    Remove
                  </Button>
                )
              }
            >
              {shift.hourlyRate != null && `${formatMoney(shift.hourlyRate)} an hour`}
            </Item>
          ))}
        </ItemList>
      )}

      {assigning && <AssignCrewDialog event={event} onClose={() => setAssigning(false)} />}
      <RemoveShiftDialog
        shift={removing}
        onKeep={() => setRemoving(null)}
        onRemove={(shift) => {
          setRemoving(null);
          remove.mutate(shift);
        }}
      />
    </Panel>
  );
}

//----------------------------------------------------------\\
//                              REMOVE
//----------------------------------------------------------\\

type RemoveShiftDialogProps = {
  shift: CrewAssignment | null;
  onKeep: () => void;
  onRemove: (shift: CrewAssignment) => void;
};

function RemoveShiftDialog({ shift, onKeep, onRemove }: RemoveShiftDialogProps) {
  if (!shift) return null;
  return <RemoveShiftBody shift={shift} onKeep={onKeep} onRemove={onRemove} />;
}

function RemoveShiftBody({ shift, onKeep, onRemove }: RemoveShiftDialogProps & { shift: CrewAssignment }) {
  const keep = useRef<HTMLButtonElement>(null);

  //focus starts on the safe choice
  useEffect(() => keep.current?.focus(), []);

  return (
    <Dialog open title={`Take ${shift.fullName} off the crew?`} onClose={onKeep}>
      <p>Their {shift.crewRole.toLowerCase()} shift comes off this event.</p>
      <DialogFooter>
        <Button ref={keep} onClick={onKeep}>
          Keep them on
        </Button>
        <Button variant="danger" onClick={() => onRemove(shift)}>
          Remove
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
