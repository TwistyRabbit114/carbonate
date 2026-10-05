import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { EventListItem } from '@/api/types';
import { Badge } from '@/components/Badge';
import { cx } from '@/lib/cx';
import { formatCount, formatDateOnly } from '@/lib/format';
import { eventTypeLabels } from './labels';
import styles from '@/components/kanban/KanbanCard.module.scss';

//----------------------------------------------------------\\
//                              CARD
//----------------------------------------------------------\\

type EventCardProps = {
  event: EventListItem;
  actions?: ReactNode; //drag handle and move button, only for roles that can move events
  cardRef?: (element: HTMLLIElement | null) => void;
  dragging?: boolean; //the card left behind while its copy follows the pointer
};

//the title link stretches over the whole card, so the card still opens the event while the
//buttons sit on top of it rather than nested inside a link
export function EventCard({ event, actions, cardRef, dragging = false }: EventCardProps) {
  return (
    <li ref={cardRef} className={cx(styles.card, dragging && styles.dragging)}>
      <EventCardContent event={event} linked />
      {actions && <div className={styles.actions}>{actions}</div>}
    </li>
  );
}

//the copy that follows the pointer while dragging
export function EventCardOverlay({ event }: { event: EventListItem }) {
  return (
    <div className={cx(styles.card, styles.overlay)}>
      <EventCardContent event={event} linked={false} />
    </div>
  );
}

//----------------------------------------------------------\\
//                              CONTENT
//----------------------------------------------------------\\

//title, then "date · venue" and "guests · type" as in the prototype. a live event says so instead of
//the date, and the venue is left off when the api has none for the event
function EventCardContent({ event, linked }: { event: EventListItem; linked: boolean }) {
  const when = event.status === 'InProgress' ? 'Live now' : formatDateOnly(event.eventDate);
  const guests = event.packSizeActual ?? event.packSizeEstimated;

  return (
    <div className={styles.content}>
      <span className={styles.title}>
        {linked ? (
          <Link to={`/events/${event.eventId}`} className={styles.link}>
            {event.name}
          </Link>
        ) : (
          <span>{event.name}</span>
        )}
        {event.isConfidential && <Badge tone="confidential">Confidential</Badge>}
      </span>
      <span className={styles.meta}>
        <span>{event.venueName ? `${when} · ${event.venueName}` : when}</span>
        <span>
          {formatCount(guests)} guests · {eventTypeLabels[event.eventType]}
        </span>
      </span>
    </div>
  );
}
