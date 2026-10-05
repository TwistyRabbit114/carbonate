import type { ReactNode } from 'react';
import type { CardPriority, TaskCard, UserRef } from '@/api/types';
import { Badge } from '@/components/Badge';
import { cx } from '@/lib/cx';
import { formatDate, formatList } from '@/lib/format';
import type { AdminStage } from './rules';
import styles from '@/components/kanban/KanbanCard.module.scss';

//----------------------------------------------------------\\
//                              CARD
//----------------------------------------------------------\\

type AdminTaskCardProps = {
  card: TaskCard;
  stage: AdminStage | undefined;
  me: string;
  actions?: ReactNode; //drag handle and "Move…" button, only when this user has a step to take
  cardRef?: (element: HTMLLIElement | null) => void;
  dragging?: boolean; //the card left behind while its copy follows the pointer
};

//focusable from code only, so focus has somewhere to land when a card moves and loses its buttons
export function AdminTaskCard({ card, stage, me, actions, cardRef, dragging = false }: AdminTaskCardProps) {
  return (
    <li
      ref={cardRef}
      className={cx(styles.card, dragging && styles.dragging)}
      tabIndex={-1}
      data-card-id={card.cardId}
    >
      <AdminTaskCardContent card={card} stage={stage} me={me} />
      {actions && <div className={styles.actions}>{actions}</div>}
    </li>
  );
}

//the copy that follows the pointer while dragging
export function AdminTaskCardOverlay({ card, stage, me }: Omit<AdminTaskCardProps, 'actions'>) {
  return (
    <div className={cx(styles.card, styles.overlay)}>
      <AdminTaskCardContent card={card} stage={stage} me={me} />
    </div>
  );
}

//----------------------------------------------------------\\
//                              CONTENT
//----------------------------------------------------------\\

//only the two levels worth a second look get a badge, as in the prototype
const priorityTones: Partial<Record<CardPriority, 'warning' | 'danger'>> = {
  High: 'warning',
  Critical: 'danger',
};

function AdminTaskCardContent({ card, stage, me }: { card: TaskCard; stage: AdminStage | undefined; me: string }) {
  const tone = priorityTones[card.priority];

  return (
    <div className={styles.content}>
      <span className={styles.title}>
        <span>{card.subject}</span>
        {tone && <Badge tone={tone}>{card.priority}</Badge>}
        {stage === 'assigned' && card.returnedAt && <Badge tone="warning">Returned</Badge>}
      </span>
      <span className={styles.meta}>
        {metaLines(card, stage, me).map((line) => (
          <span key={line}>{line}</span>
        ))}
      </span>
    </div>
  );
}

//who has it and what happens next, the prototype's two meta lines for each column
function metaLines(card: TaskCard, stage: AdminStage | undefined, me: string) {
  const names = (people: UserRef[]) =>
    formatList(people.map((person) => (person.userId === me ? 'you' : person.fullName))) || 'nobody yet';
  const assignees = names(card.assignees);

  if (stage === 'review') {
    const reviewer =
      card.createdBy.userId === me ? 'Waiting for your review' : `Waiting for ${card.createdBy.fullName} to review`;
    return [`Handed in by ${assignees}`, reviewer];
  }
  if (stage === 'complete') {
    return [`Completed by ${assignees}`, ...(card.completedAt ? [`Signed off ${formatDate(card.completedAt)}`] : [])];
  }
  return [`Assigned to ${assignees}`, ...(card.dueAt ? [`Due ${formatDate(card.dueAt)}`] : [])];
}
