import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { Board, BoardColumn, TaskCard } from '@/api/types';
import { usePermissions, useSignedIn } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { useTaskStep } from '@/features/admin-tasks/api';
import { useEventBoard, useMoveCard } from '@/features/event-board/api';
import { formatDateTime } from '@/lib/format';
import styles from './TaskLists.module.scss';

//a crew member's cards as a list with buttons rather than a board to drag on, which is easier
//one-handed on a phone. the api only sends crew their own cards (FR-21), desk roles get the whole
//board, so the list keeps to the ones assigned to the viewer

//----------------------------------------------------------\\
//                              HELPERS
//----------------------------------------------------------\\

const sortedColumns = (board: Board) => [...board.columns].sort((a, b) => a.position - b.position);

function myCards(board: Board, me: string) {
  return sortedColumns(board).flatMap((column) =>
    column.cards
      .filter((card) => card.assignees.some((person) => person.userId === me))
      .map((card) => ({ card, column })),
  );
}

const byDue = (a: { card: TaskCard }, b: { card: TaskCard }) =>
  (a.card.dueAt ?? '9999').localeCompare(b.card.dueAt ?? '9999');

type TaskRowProps = {
  to: string;
  card: TaskCard;
  status: ReactNode;
  note?: string;
  actions?: ReactNode;
};

function TaskRow({ to, card, status, note, actions }: TaskRowProps) {
  return (
    <li className={styles.task}>
      <div className={styles.main}>
        <Link to={to} className={styles.subject}>
          {card.subject}
        </Link>
        <p className={styles.meta}>
          {status}
          {card.dueAt && <span>Due {formatDateTime(card.dueAt)}</span>}
        </p>
        {note && <p className={styles.note}>{note}</p>}
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
    </li>
  );
}

//----------------------------------------------------------\\
//                              EVENT CARDS
//----------------------------------------------------------\\

type EventTaskListProps = {
  eventId: string;
  empty: string;
};

//start moves a card into the next column, done into the done column. the server checks each move
export function EventTaskList({ eventId, empty }: EventTaskListProps) {
  const me = useSignedIn().user.userId;
  const canMove = usePermissions().can('task.move');
  const board = useEventBoard(eventId);
  const move = useMoveCard(eventId);

  if (board.isError)
    return <ErrorState message="We couldn't load these tasks." onRetry={() => void board.refetch()} />;
  if (!board.data) return <p role="status">Loading tasks…</p>;

  const columns = sortedColumns(board.data);
  const doneColumn = columns.find((column) => column.isDoneColumn);
  const working = columns.filter((column) => !column.isDoneColumn);
  const cards = myCards(board.data, me).sort(byDue);
  if (cards.length === 0) return <p className={styles.empty}>{empty}</p>;

  //the column after this one, if it's still a working column
  const nextFrom = (column: BoardColumn) => working[working.indexOf(column) + 1];

  return (
    <ul className={styles.list}>
      {cards.map(({ card, column }) => {
        const next = column.isDoneColumn ? undefined : nextFrom(column);
        return (
          <TaskRow
            key={card.cardId}
            to={`/events/${eventId}/tasks/${card.cardId}`}
            card={card}
            status={column.isDoneColumn ? <Badge>Done</Badge> : <span>{column.name}</span>}
            actions={
              canMove &&
              !column.isDoneColumn && (
                <>
                  {next && working.indexOf(column) === 0 && (
                    <Button
                      onClick={() => move.mutate({ card, to: next })}
                      aria-label={`Start ${card.subject}`}
                    >
                      Start
                    </Button>
                  )}
                  {doneColumn && (
                    <Button
                      variant="primary"
                      onClick={() => move.mutate({ card, to: doneColumn })}
                      aria-label={`Mark ${card.subject} done`}
                    >
                      Done
                    </Button>
                  )}
                </>
              )
            }
          />
        );
      })}
    </ul>
  );
}

//----------------------------------------------------------\\
//                              ADMIN TASKS
//----------------------------------------------------------\\

type AdminTaskListProps = {
  board: Board;
  empty: string;
};

//handing in is the assignee's only move: the manager who gave out the task signs it off (FR-20)
export function AdminTaskList({ board, empty }: AdminTaskListProps) {
  const me = useSignedIn().user.userId;
  const step = useTaskStep();
  const [assigned, review] = sortedColumns(board);
  const cards = myCards(board, me).sort(byDue);
  if (cards.length === 0) return <p className={styles.empty}>{empty}</p>;

  return (
    <ul className={styles.list}>
      {cards.map(({ card, column }) => {
        const waiting = column.position === 0;
        return (
          <TaskRow
            key={card.cardId}
            to={`/admin-tasks/${card.cardId}`}
            card={card}
            status={
              card.status === 'Complete' ? (
                <Badge>Signed off</Badge>
              ) : card.status === 'InProgressOrNeedsReview' ? (
                <Badge tone="auto">With {card.createdBy.fullName} for review</Badge>
              ) : card.returnedAt ? (
                <Badge tone="warning">Sent back</Badge>
              ) : (
                <span>To do</span>
              )
            }
            note={waiting && card.reviewNotes ? `Notes: ${card.reviewNotes}` : undefined}
            actions={
              waiting &&
              assigned &&
              review && (
                <Button
                  variant="primary"
                  onClick={() => step.mutate({ card, action: 'handIn', to: review })}
                  aria-label={`Submit ${card.subject} for review`}
                >
                  Submit for review
                </Button>
              )
            }
          />
        );
      })}
    </ul>
  );
}
