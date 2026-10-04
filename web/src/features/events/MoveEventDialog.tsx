import { useEffect, useRef, useState } from 'react';
import type { EventListItem, EventStatus } from '@/api/types';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { Dialog } from '@/components/Dialog';
import { useAllowedTransitions } from './api';
import { stageLabels } from './labels';
import styles from './MoveEventDialog.module.scss';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

type MoveEventDialogProps = {
  event: EventListItem | null; //open while an event is set
  onClose: () => void;
  onMove: (event: EventListItem, to: EventStatus) => void;
};

//why someone might move a card by hand into a column the server normally fills itself
const moveHints: Partial<Record<EventStatus, string>> = {
  InProgress: 'Start it early. It moves here on its own at its start time.',
  Finished: 'Finish it now. It moves here on its own at its end time.',
};

//----------------------------------------------------------\\
//                              DIALOG
//----------------------------------------------------------\\

//the "Move to…" alternative to dragging (keyboard, screen reader, or just easier on a phone).
//cancelling lives here too, it's an action rather than a column (FR-02)
export function MoveEventDialog({ event, onClose, onMove }: MoveEventDialogProps) {
  if (!event) return null;
  return <MoveDialogBody key={event.eventId} event={event} onClose={onClose} onMove={onMove} />;
}

function MoveDialogBody({ event, onClose, onMove }: MoveEventDialogProps & { event: EventListItem }) {
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const allowed = useAllowedTransitions(event.eventId);
  const stages = (allowed.data ?? []).filter((to) => to !== 'Cancelled');
  const canCancel = allowed.data?.includes('Cancelled') ?? false;

  return (
    <Dialog open title={confirmingCancel ? `Cancel ${event.name}?` : `Move ${event.name}`} onClose={onClose}>
      {confirmingCancel ? (
        <ConfirmCancel
          onKeep={() => setConfirmingCancel(false)}
          onCancel={() => onMove(event, 'Cancelled')}
        />
      ) : (
        <>
          <p className={styles.current}>
            It's in <strong>{stageLabels[event.status]}</strong> now.
          </p>

          {allowed.isPending && <p role="status">Checking where it can move…</p>}
          {allowed.isError && (
            <Alert tone="danger">Couldn't check where it can move. Close this and try again.</Alert>
          )}
          {allowed.data && stages.length === 0 && !canCancel && (
            <p className={styles.none}>
              {event.status === 'Finished'
                ? 'Finished events stay where they are.'
                : `It can't move from ${stageLabels[event.status]} right now.`}
            </p>
          )}

          {stages.length > 0 && (
            <ul className={styles.options}>
              {stages.map((to) => (
                <li key={to}>
                  <button type="button" className={styles.option} onClick={() => onMove(event, to)}>
                    <span className={styles.optionTitle}>Move to {stageLabels[to]}</span>
                    {moveHints[to] && <span className={styles.optionHint}>{moveHints[to]}</span>}
                  </button>
                </li>
              ))}
            </ul>
          )}

          {canCancel && (
            <Button variant="danger" block onClick={() => setConfirmingCancel(true)}>
              Cancel event…
            </Button>
          )}

          <div className={styles.footer}>
            <Button onClick={onClose}>Close</Button>
          </div>
        </>
      )}
    </Dialog>
  );
}

//----------------------------------------------------------\\
//                              CANCEL
//----------------------------------------------------------\\

function ConfirmCancel({ onKeep, onCancel }: { onKeep: () => void; onCancel: () => void }) {
  const keep = useRef<HTMLButtonElement>(null);

  //the button that was focused has just gone, so focus lands on the safe choice
  useEffect(() => keep.current?.focus(), []);

  return (
    <>
      <p>Cancelling takes it off the board for good. A cancelled event can't be reopened.</p>
      <div className={styles.footer}>
        <Button ref={keep} onClick={onKeep}>
          Keep event
        </Button>
        <Button variant="danger" onClick={onCancel}>
          Cancel event
        </Button>
      </div>
    </>
  );
}
