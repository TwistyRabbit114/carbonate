import { useEffect, useRef, useState } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router';
import { isApiError } from '@/api/problem';
import type { DivisionCode, EventDetail } from '@/api/types';
import { usesDeskLayout } from '@/app/navigation';
import { usePermissions } from '@/auth/AuthContext';
import { Alert } from '@/components/Alert';
import { BackLink } from '@/components/BackLink';
import { Button } from '@/components/Button';
import { ConfidentialBanner } from '@/components/ConfidentialBanner';
import { DetailColumns } from '@/components/DetailColumns';
import { Dialog, DialogFooter } from '@/components/Dialog';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Panel } from '@/components/Panel';
import { useToast } from '@/components/toast/ToastContext';
import { TaskBoardPanel } from '@/features/event-board/TaskBoardPanel';
import { CostingPanel } from '@/features/finance/CostingPanel';
import { EventIncidentsPanel } from '@/features/incidents/EventIncidentsPanel';
import { EventStockPanel } from '@/features/stock/EventStockPanel';
import { VenuePanel } from '@/features/venues/VenuePanel';
import { formatCount, formatDateOnly, formatDateTime } from '@/lib/format';
import { useDeleteEvent, useEvent, useMoveEvent } from './api';
import { CrewPanel } from './CrewPanel';
import {
  divisionLabels,
  eventTypeLabels,
  infrastructureLabels,
  paymentModeLabels,
  stageLabels,
} from './labels';
import { MilestonesPanel } from './MilestonesPanel';
import { MissingEvent } from './MissingEvent';
import { MoveEventDialog } from './MoveEventDialog';
import { RecordConfirmationDialog } from './RecordConfirmationDialog';
import { ServiceSchedulePanel } from './ServiceSchedulePanel';
import styles from './EventDetailPage.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//the whole event for the desk (FR-01 to FR-10). there's one page for every role: what's on it
//comes from what the api sends back, so a role without the money gets no costing panel at all,
//rather than an empty one (NFR-17). crew have their own phone-first view of the same event
export default function EventDetailPage() {
  const { eventId = '' } = useParams();
  if (!usesDeskLayout(usePermissions())) return <Navigate to={`/my/events/${eventId}`} replace />;
  return <DeskEventPage eventId={eventId} />;
}

function DeskEventPage({ eventId }: { eventId: string }) {
  const event = useEvent(eventId);

  if (event.isError && isApiError(event.error) && event.error.status === 404) return <MissingEvent />;
  if (event.isError) {
    return (
      <>
        <PageHead title="Event" eyebrow={<BackLink to="/events">Events</BackLink>} />
        <ErrorState message="We couldn't load this event." onRetry={() => void event.refetch()} />
      </>
    );
  }
  if (!event.data) return <PageSkeleton />;
  return <EventDetail event={event.data} />;
}

//----------------------------------------------------------\\
//                              DETAIL
//----------------------------------------------------------\\

type EventAction = 'confirm' | 'move' | 'delete';

function EventDetail({ event }: { event: EventDetail }) {
  const check = usePermissions();
  const move = useMoveEvent();
  const [action, setAction] = useState<EventAction | null>(null);

  const enquiry = event.status === 'Enquired';
  const live = event.status === 'ConfirmedInPlanning' || event.status === 'InProgress';
  const canConfirm = enquiry && check.can('confirmation.record');
  const canMove = live && check.can('event.transition');
  const canEdit = check.can('event.edit') && event.status !== 'Cancelled';

  return (
    <>
      <PageHead
        eyebrow={
          <>
            <BackLink to="/events">Events</BackLink> · {stageLabels[event.status]}
          </>
        }
        title={event.name}
        actions={
          <>
            {canConfirm && (
              <Button variant="primary" onClick={() => setAction('confirm')}>
                Record PO or deposit
              </Button>
            )}
            {canMove && <Button onClick={() => setAction('move')}>Change stage…</Button>}
            {canEdit && <LinkButton to={`/events/${event.eventId}/edit`}>Edit event</LinkButton>}
            {check.can('event.delete') && (
              <Button variant="ghost" onClick={() => setAction('delete')}>
                Delete
              </Button>
            )}
          </>
        }
      />

      {event.isConfidential && <ConfidentialBanner />}
      {enquiry && (
        <div className={styles.notice}>
          <Alert>
            This is still an enquiry. It goes on the events board once the client's PO or deposit is recorded.
          </Alert>
        </div>
      )}

      <DetailColumns
        main={
          <>
            <OverviewPanel event={event} />
            <ServiceSchedulePanel schedule={event.serviceSchedule} />
            <MilestonesPanel event={event} canReschedule={check.can('event.edit')} />
            <VenuePanel eventId={event.eventId} venueId={event.venueId} />
            <EventStockPanel eventId={event.eventId} canPlan={check.can('stock.plan')} />
            <EventIncidentsPanel
              eventId={event.eventId}
              canView={check.can('incident.view')}
              canReport={check.can('incident.create') && event.status !== 'Cancelled'}
            />
          </>
        }
        side={
          <>
            <CostingPanel event={event} />
            <CrewPanel event={event} canAssign={check.can('crew.assign')} />
            <TaskBoardPanel eventId={event.eventId} />
          </>
        }
      />

      {action === 'confirm' && <RecordConfirmationDialog event={event} onClose={() => setAction(null)} />}
      <MoveEventDialog
        event={action === 'move' ? event : null}
        onClose={() => setAction(null)}
        onMove={(moving, to) => {
          setAction(null);
          move.mutate({ event: moving, to });
        }}
      />
      {action === 'delete' && <DeleteEventDialog event={event} onClose={() => setAction(null)} />}
    </>
  );
}

//----------------------------------------------------------\\
//                              OVERVIEW
//----------------------------------------------------------\\

//pack size reads "180 guests (confirmed)" once the actual number is in (FR-08)
function packSize(event: EventDetail) {
  if (event.packSizeActual != null) return `${formatCount(event.packSizeActual)} guests (confirmed)`;
  return `${formatCount(event.packSizeEstimated)} guests (estimated)`;
}

function OverviewPanel({ event }: { event: EventDetail }) {
  return (
    <Panel title="Overview">
      <FieldList>
        <FieldRow label="Client">{event.clientName}</FieldRow>
        <FieldRow label="Venue">{event.venueName ?? 'Not chosen yet'}</FieldRow>
        <FieldRow label="Date">{formatDateOnly(event.eventDate)}</FieldRow>
        <FieldRow label="Starts">{formatDateTime(event.startsAt)}</FieldRow>
        <FieldRow label="Ends">{formatDateTime(event.endsAt)}</FieldRow>
        <FieldRow label="Event code">{event.eventCode}</FieldRow>
        <FieldRow label="Pack size">{packSize(event)}</FieldRow>
        <FieldRow label="Expected headcount">
          {event.headcountExpected != null ? formatCount(event.headcountExpected) : undefined}
        </FieldRow>
        <FieldRow label="Event type">{eventTypeLabels[event.eventType]}</FieldRow>
        <FieldRow label="Division">
          {divisionLabels[event.divisionCode as DivisionCode] ?? event.divisionCode}
        </FieldRow>
        <FieldRow label="Payment">{paymentModeLabels[event.paymentMode]}</FieldRow>
        <FieldRow label="Infrastructure">{infrastructureLabels[event.infrastructureMode]}</FieldRow>
        <FieldRow label="Staff required">{formatCount(event.staffRequired)}</FieldRow>
      </FieldList>
    </Panel>
  );
}

//----------------------------------------------------------\\
//                              DELETE
//----------------------------------------------------------\\

//director only, and only a soft delete: the record is kept but stops showing (FR-09)
function DeleteEventDialog({ event, onClose }: { event: EventDetail; onClose: () => void }) {
  const remove = useDeleteEvent(event.eventId);
  const navigate = useNavigate();
  const toast = useToast();
  const keep = useRef<HTMLButtonElement>(null);
  const [failed, setFailed] = useState(false);

  //focus starts on the safe choice
  useEffect(() => keep.current?.focus(), []);

  function deleteEvent() {
    setFailed(false);
    remove.mutate(undefined, {
      onSuccess: () => {
        toast.success(`${event.name} was deleted.`);
        void navigate('/events', { replace: true });
      },
      onError: () => setFailed(true),
    });
  }

  return (
    <Dialog open title={`Delete ${event.name}?`} onClose={onClose}>
      {failed && (
        <div className={styles.notice}>
          <Alert tone="danger">It wasn't deleted. Check your connection and try again.</Alert>
        </div>
      )}
      <p>It comes off the events board and every list. Carbonate keeps the record, it just stops showing.</p>
      <DialogFooter>
        <Button ref={keep} onClick={onClose}>
          Keep event
        </Button>
        <Button variant="danger" busy={remove.isPending} onClick={deleteEvent}>
          Delete event
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
