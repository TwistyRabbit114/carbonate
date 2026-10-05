import { TriangleAlert } from 'lucide-react';
import { useParams } from 'react-router';
import { isApiError } from '@/api/problem';
import type { EventDetail, MilestoneType } from '@/api/types';
import { usePermissions, useSignedIn } from '@/auth/AuthContext';
import { BackLink } from '@/components/BackLink';
import { ConfidentialBanner } from '@/components/ConfidentialBanner';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Panel } from '@/components/Panel';
import { useEventCrew } from '@/features/event-board/api';
import { useEvent } from '@/features/events/api';
import { MissingEvent } from '@/features/events/MissingEvent';
import { eventTypeLabels, stageLabels } from '@/features/events/labels';
import { MilestonesPanel } from '@/features/events/MilestonesPanel';
import { ServiceSchedulePanel } from '@/features/events/ServiceSchedulePanel';
import { useSiteVisits, useVenue } from '@/features/venues/api';
import { mapLink } from '@/features/venues/places';
import { formatCount, formatDateOnly, formatSpan, formatTime } from '@/lib/format';
import { myFirstShift } from './schedule';
import { EventTaskList } from './TaskLists';
import styles from './CrewEventPage.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//the event as crew see it, the no-cost variant: no money anywhere, because the api never sends
//crew any. what's here is what's needed on the day, read on a phone in a car park
export default function CrewEventPage() {
  const { eventId = '' } = useParams();
  const event = useEvent(eventId);

  if (event.isError && isApiError(event.error) && event.error.status === 404) {
    return <MissingEvent backTo="/my/events" backLabel="My events" />;
  }
  if (event.isError) {
    return (
      <>
        <PageHead title="Event" eyebrow={<BackLink to="/my/events">My events</BackLink>} />
        <ErrorState message="We couldn't load this event." onRetry={() => void event.refetch()} />
      </>
    );
  }
  if (!event.data) return <PageSkeleton />;
  return <CrewEvent event={event.data} />;
}

//load-in, doors and strike are the ones that happen on site
const onSite: readonly MilestoneType[] = ['LoadIn', 'Doors', 'Strike'];

function CrewEvent({ event }: { event: EventDetail }) {
  const check = usePermissions();
  const me = useSignedIn().user.userId;
  const crew = useEventCrew(event.eventId);
  const venue = useVenue(event.venueId);
  const visits = useSiteVisits(event.eventId);
  const shift = myFirstShift(crew.data, me);
  const recce = visits.data?.[0];
  const canReport =
    check.can('incident.create') && (event.status === 'ConfirmedInPlanning' || event.status === 'InProgress');

  return (
    <>
      <PageHead
        eyebrow={
          <>
            <BackLink to="/my/events">My events</BackLink> · {stageLabels[event.status]}
          </>
        }
        title={event.name}
      />

      {event.isConfidential && <ConfidentialBanner crew />}

      {shift && (
        <section className={styles.shift} aria-labelledby="my-shift">
          <h2 id="my-shift" className={styles.shiftLabel}>
            Your call time
          </h2>
          <p className={styles.callValue}>{formatTime(shift.shiftStart)}</p>
          <p className={styles.shiftNote}>
            {shift.crewRole} · {formatSpan(shift.shiftStart, shift.shiftEnd)}
          </p>
        </section>
      )}

      {canReport && (
        <p className={styles.report}>
          <LinkButton to={`/my/report?event=${event.eventId}`} variant="primary">
            <TriangleAlert aria-hidden="true" />
            Report breakage or shortfall
          </LinkButton>
        </p>
      )}

      <Panel title="Overview">
        <FieldList>
          <FieldRow label="Venue">
            {venue.data ? (
              <>
                {venue.data.name}
                <br />
                <a
                  href={mapLink(venue.data.address)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={styles.mapLink}
                >
                  {venue.data.address}
                </a>
              </>
            ) : (
              event.venueName
            )}
          </FieldRow>
          <FieldRow label="Date">{formatDateOnly(event.eventDate)}</FieldRow>
          <FieldRow label="Pack size">{`${formatCount(event.packSizeActual ?? event.packSizeEstimated)} guests`}</FieldRow>
          <FieldRow label="Event type">{eventTypeLabels[event.eventType]}</FieldRow>
          <FieldRow label="Access route">
            {venue.data?.accessRoute && <span className={styles.text}>{venue.data.accessRoute}</span>}
          </FieldRow>
          <FieldRow label="Loading bay">
            {venue.data?.loadingBayDetails && (
              <span className={styles.text}>{venue.data.loadingBayDetails}</span>
            )}
          </FieldRow>
          <FieldRow label="Sign-in">
            {recce?.signInProcedure && <span className={styles.text}>{recce.signInProcedure}</span>}
          </FieldRow>
          <FieldRow label="Security">{recce?.securityCheckpoint}</FieldRow>
          <FieldRow label="PPE required">
            {venue.data?.ppeRequirements && <span className={styles.text}>{venue.data.ppeRequirements}</span>}
          </FieldRow>
        </FieldList>
      </Panel>

      <ServiceSchedulePanel schedule={event.serviceSchedule} crew />
      <MilestonesPanel event={event} canReschedule={false} only={onSite} />

      <Panel title="My tasks for this event">
        <EventTaskList eventId={event.eventId} empty="Nothing assigned to you on this event yet." />
      </Panel>

      {/*TODO(plan): crew leads should see stock quantities here (stock.view), but the stock
      requirements endpoint takes stock.plan, which crew don't hold. ask D*/}
    </>
  );
}
