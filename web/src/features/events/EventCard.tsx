import { Link } from 'react-router';
import type { EventListItem } from '@/api/types';
import { Badge } from '@/components/Badge';
import { formatCount, formatDateOnly } from '@/lib/format';
import { eventTypeLabels } from './labels';
import styles from './EventCard.module.scss';

type EventCardProps = {
  event: EventListItem;
};

//title, then "date · venue" and "guests · type" as in the prototype. a live event says so instead of the date
export function EventCard({ event }: EventCardProps) {
  const when = event.status === 'InProgress' ? 'Live now' : formatDateOnly(event.eventDate);
  const guests = event.packSizeActual ?? event.packSizeEstimated;

  return (
    <li>
      <Link to={`/events/${event.eventId}`} className={styles.card}>
        <span className={styles.title}>
          {event.name}
          {event.isConfidential && <Badge tone="confidential">Confidential</Badge>}
        </span>
        <span className={styles.meta}>
          <span>
            {when} · {event.venueName}
          </span>
          <span>
            {formatCount(guests)} guests · {eventTypeLabels[event.eventType]}
          </span>
        </span>
      </Link>
    </li>
  );
}
