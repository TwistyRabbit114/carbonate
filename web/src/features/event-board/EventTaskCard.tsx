import type { ReactNode } from 'react';
import { Paperclip } from 'lucide-react';
import { Link } from 'react-router';
import type { CardPriority, Milestone, TaskCard } from '@/api/types';
import { Avatars } from '@/components/Avatars';
import { Badge } from '@/components/Badge';
import { cx } from '@/lib/cx';
import { formatDate, formatDateTime } from '@/lib/format';
import { dueAgainstMilestone } from './due';
import styles from '@/components/kanban/KanbanCard.module.scss';
import own from './EventTaskCard.module.scss';

//----------------------------------------------------------\\
//                              CARD
//----------------------------------------------------------\\

type EventTaskCardProps = {
  card: TaskCard;
  eventId: string;
  milestone?: Milestone; //the one the card hangs off, when it has one and they've loaded
  me: string;
  actions?: ReactNode; //drag handle and "Move to…" button, for people who can move cards
  cardRef?: (element: HTMLLIElement | null) => void;
  dragging?: boolean; //the card left behind while its copy follows the pointer
};

//focusable from code only, so focus has somewhere to land when a card moves and loses its buttons
export function EventTaskCard({
  card,
  eventId,
  milestone,
  me,
  actions,
  cardRef,
  dragging = false,
}: EventTaskCardProps) {
  return (
    <li
      ref={cardRef}
      className={cx(styles.card, dragging && styles.dragging)}
      tabIndex={-1}
      data-card-id={card.cardId}
    >
      <CardContent
        card={card}
        milestone={milestone}
        me={me}
        href={`/events/${eventId}/tasks/${card.cardId}`}
      />
      {actions && <div className={styles.actions}>{actions}</div>}
    </li>
  );
}

//the copy that follows the pointer while dragging
export function EventTaskCardOverlay({
  card,
  milestone,
  me,
}: Pick<EventTaskCardProps, 'card' | 'milestone' | 'me'>) {
  return (
    <div className={cx(styles.card, styles.overlay)}>
      <CardContent card={card} milestone={milestone} me={me} />
    </div>
  );
}

//----------------------------------------------------------\\
//                              CONTENT
//----------------------------------------------------------\\

//only the two levels worth a second look get a badge, the same as the admin board
const priorityTones: Partial<Record<CardPriority, 'warning' | 'danger'>> = {
  High: 'warning',
  Critical: 'danger',
};

type ContentProps = {
  card: TaskCard;
  milestone?: Milestone;
  me: string;
  href?: string;
};

function CardContent({ card, milestone, me, href }: ContentProps) {
  const tone = priorityTones[card.priority];

  return (
    <div className={styles.content}>
      <span className={styles.title}>
        {href ? (
          <Link to={href} className={styles.link}>
            {card.subject}
          </Link>
        ) : (
          <span>{card.subject}</span>
        )}
        {tone && <Badge tone={tone}>{card.priority}</Badge>}
      </span>
      <span className={styles.meta}>
        {metaLines(card, milestone).map((line) => (
          <span key={line}>{line}</span>
        ))}
      </span>
      <span className={own.footer}>
        <Avatars people={card.assignees} me={me} />
        {card.attachmentCount > 0 && (
          <span className={own.attachments}>
            <Paperclip aria-hidden="true" />
            {card.attachmentCount}
            <span className="visually-hidden">
              {' '}
              {card.attachmentCount === 1 ? 'attachment' : 'attachments'}
            </span>
          </span>
        )}
      </span>
    </div>
  );
}

//when it's due, and how that sits against the milestone it hangs off: "2h before load-in"
function metaLines(card: TaskCard, milestone: Milestone | undefined) {
  if (card.status === 'Done') return card.completedAt ? [`Done ${formatDate(card.completedAt)}`] : ['Done'];
  if (!card.dueAt) return [];
  return [
    `Due ${formatDateTime(card.dueAt)}`,
    ...(milestone ? [dueAgainstMilestone(card.dueAt, milestone)] : []),
  ];
}
