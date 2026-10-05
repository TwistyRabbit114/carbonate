import { CalendarX2, ChevronRight, MapPin } from 'lucide-react';
import { Link } from 'react-router';
import type { EventListItem } from '@/api/types';
import { useSignedIn } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { useEventCrew } from '@/features/event-board/api';
import { useEvent, useMyEvents } from '@/features/events/api';
import { stageLabels } from '@/features/events/labels';
import { useSiteVisits, useVenue } from '@/features/venues/api';
import { mapLink } from '@/features/venues/places';
import { formatDateOnly, formatDateTime, formatTime } from '@/lib/format';
import { crewSchedule, myFirstShift } from './schedule';
import styles from './MyEventsPage.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//where crew land: the next event up top with the call time big, then everything else (NFR-01).
//crew only ever get their own events back from the api (FR-36)
export default function MyEventsPage() {
  const events = useMyEvents();

  if (events.isError) {
    return (
      <>
        <PageHead title="My events" />
        <ErrorState message="We couldn't load your events." onRetry={() => void events.refetch()} />
      </>
    );
  }
  if (!events.data) return <PageSkeleton />;

  const { next, upcoming, past } = crewSchedule(events.data);

  return (
    <>
      <PageHead title="My events" />

      {!next && past.length === 0 ? (
        <EmptyState title="You're not on any events yet" icon={CalendarX2}>
          <p>Your manager will add you.</p>
        </EmptyState>
      ) : (
        <>
          {next ? (
            <NextEvent event={next} />
          ) : (
            <p className={styles.nothingNext}>Nothing coming up for you.</p>
          )}

          {upcoming.length > 0 && (
            <section aria-labelledby="coming-up">
              <h2 id="coming-up" className={styles.heading}>
                Coming up
              </h2>
              <ul className={styles.list}>
                {upcoming.map((event) => (
                  <EventRow key={event.eventId} event={event} />
                ))}
              </ul>
            </section>
          )}

          {past.length > 0 && (
            <details className={styles.past}>
              <summary>Past events ({past.length})</summary>
              <ul className={styles.list}>
                {past.map((event) => (
                  <EventRow key={event.eventId} event={event} past />
                ))}
              </ul>
            </details>
          )}
        </>
      )}
    </>
  );
}

//----------------------------------------------------------\\
//                              NEXT EVENT
//----------------------------------------------------------\\

//everything needed to get there: when to arrive, where, and how to get in
function NextEvent({ event }: { event: EventListItem }) {
  const me = useSignedIn().user.userId;
  const detail = useEvent(event.eventId);
  const crew = useEventCrew(event.eventId);
  const venue = useVenue(detail.data?.venueId);
  const visits = useSiteVisits(event.eventId);
  const shift = myFirstShift(crew.data, me);
  const signIn = visits.data?.find((visit) => visit.signInProcedure)?.signInProcedure;
  const live = event.status === 'InProgress';

  return (
    <article className={styles.next} aria-labelledby="next-event">
      <p className={styles.eyebrow}>{live ? 'On now' : 'Next up'}</p>
      <h2 id="next-event" className={styles.nextName}>
        <Link to={`/my/events/${event.eventId}`}>{event.name}</Link>
      </h2>
      <p className={styles.badges}>
        {event.isConfidential && <Badge tone="confidential">Confidential</Badge>}
        {live && <Badge tone="auto">Live now</Badge>}
      </p>
      <p className={styles.date}>{formatDateOnly(event.eventDate)}</p>

      <div className={styles.callTime}>
        <p className={styles.callLabel}>Your call time</p>
        {shift ? (
          <>
            <p className={styles.callValue}>{formatTime(shift.shiftStart)}</p>
            <p className={styles.callNote}>
              {formatDateTime(shift.shiftStart)} · {shift.crewRole}
            </p>
          </>
        ) : (
          <p className={styles.callNote}>
            {crew.isPending ? 'Loading…' : 'Not set yet. Check with your manager.'}
          </p>
        )}
      </div>

      <dl className={styles.facts}>
        <div>
          <dt>Venue</dt>
          <dd>
            {venue.data ? (
              <>
                {venue.data.name}
                <br />
                <a
                  href={mapLink(venue.data.address)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={styles.map}
                >
                  <MapPin aria-hidden="true" />
                  {venue.data.address}
                </a>
              </>
            ) : (
              (event.venueName ?? 'Not chosen yet')
            )}
          </dd>
        </div>
        {venue.data?.accessRoute && (
          <div>
            <dt>Access route</dt>
            <dd>{venue.data.accessRoute}</dd>
          </div>
        )}
        {signIn && (
          <div>
            <dt>Sign-in</dt>
            <dd>{signIn}</dd>
          </div>
        )}
        {venue.data?.ppeRequirements && (
          <div>
            <dt>PPE</dt>
            <dd>{venue.data.ppeRequirements}</dd>
          </div>
        )}
      </dl>

      <Link to={`/my/events/${event.eventId}`} className={styles.more}>
        Everything for this event
        <ChevronRight aria-hidden="true" />
      </Link>
    </article>
  );
}

//----------------------------------------------------------\\
//                              OTHER EVENTS
//----------------------------------------------------------\\

function EventRow({ event, past = false }: { event: EventListItem; past?: boolean }) {
  const me = useSignedIn().user.userId;
  //only upcoming events need a call time
  const crew = useEventCrew(event.eventId, !past);
  const shift = past ? undefined : myFirstShift(crew.data, me);

  return (
    <li>
      <Link to={`/my/events/${event.eventId}`} className={styles.row}>
        <span className={styles.rowMain}>
          <span className={styles.rowName}>
            {event.name}
            {event.isConfidential && <Badge tone="confidential">Confidential</Badge>}
          </span>
          <span className={styles.rowMeta}>
            {formatDateOnly(event.eventDate)}
            {event.venueName && ` · ${event.venueName}`}
            {past && ` · ${stageLabels[event.status]}`}
          </span>
          {shift && <span className={styles.rowCall}>Call time {formatTime(shift.shiftStart)}</span>}
        </span>
        <ChevronRight className={styles.chevron} aria-hidden="true" />
      </Link>
    </li>
  );
}
