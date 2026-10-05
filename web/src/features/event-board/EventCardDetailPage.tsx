import { useState } from 'react';
import { SearchX } from 'lucide-react';
import { useParams } from 'react-router';
import { isApiError } from '@/api/problem';
import { queryKeys } from '@/api/queryKeys';
import type { Board, EventDetail, Milestone, TaskCard } from '@/api/types';
import { usesDeskLayout } from '@/app/navigation';
import { usePermissions, useSignedIn } from '@/auth/AuthContext';
import { BackLink } from '@/components/BackLink';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ConfidentialBanner } from '@/components/ConfidentialBanner';
import { DetailColumns } from '@/components/DetailColumns';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Panel } from '@/components/Panel';
import { SanitisedDescription } from '@/components/SanitisedDescription';
import { AssigneesDialog } from '@/features/boards/AssigneesDialog';
import { useCard } from '@/features/boards/cards';
import { EditCardDialog } from '@/features/boards/EditCardDialog';
import { milestoneLabels } from '@/features/events/labels';
import { formatDate, formatDateTime, formatList } from '@/lib/format';
import { useCrewPeople, useEventBoard, useEventSummary, useMilestones, useMoveCard } from './api';
import { dueAgainstMilestone } from './due';
import { MoveCardDialog } from './MoveCardDialog';
import styles from './EventCardDetailPage.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//one card on an event's task board (FR-21). files on cards (FR-22) come later
export default function EventCardDetailPage() {
  const { eventId = '', cardId = '' } = useParams();
  const card = useCard(cardId);
  const board = useEventBoard(eventId);
  const event = useEventSummary(eventId);
  const milestones = useMilestones(eventId);
  const boardUrl = `/events/${eventId}/board`;

  const notFound =
    (card.isError && isApiError(card.error) && card.error.status === 404) ||
    (board.isError && isApiError(board.error) && board.error.status === 404) ||
    //a card from another event's board, or the admin board, isn't found at this address
    (card.data && board.data && card.data.boardId !== board.data.boardId);

  if (notFound) return <MissingCard boardUrl={boardUrl} />;
  if (card.isError || board.isError) {
    return (
      <>
        <PageHead title="Card" eyebrow={<BackLink to={boardUrl}>Task board</BackLink>} />
        <ErrorState
          message="We couldn't load this card."
          onRetry={() => {
            void card.refetch();
            void board.refetch();
          }}
        />
      </>
    );
  }
  if (!card.data || !board.data) return <PageSkeleton />;

  return (
    <CardDetail
      card={card.data}
      board={board.data}
      event={event.data}
      milestones={milestones.data}
      eventId={eventId}
      boardUrl={boardUrl}
    />
  );
}

function MissingCard({ boardUrl }: { boardUrl: string }) {
  return (
    <>
      <PageHead title="Card not found" eyebrow={<BackLink to={boardUrl}>Task board</BackLink>} />
      <EmptyState title="We can't find that card" icon={SearchX}>
        <p>It may have come off the board, or it isn't one of yours.</p>
        <LinkButton to={boardUrl}>Back to the task board</LinkButton>
      </EmptyState>
    </>
  );
}

//----------------------------------------------------------\\
//                              DETAIL
//----------------------------------------------------------\\

type CardDetailProps = {
  card: TaskCard;
  board: Board;
  event: EventDetail | undefined;
  milestones: readonly Milestone[] | undefined;
  eventId: string;
  boardUrl: string;
};

function CardDetail({ card, board, event, milestones, eventId, boardUrl }: CardDetailProps) {
  const me = useSignedIn().user.userId;
  const check = usePermissions();
  const move = useMoveCard(eventId);
  const [editing, setEditing] = useState(false);
  const [assigning, setAssigning] = useState(false);
  const [moving, setMoving] = useState(false);

  const canEdit = check.can('task.edit');
  const canMove = check.can('task.move');
  const column = board.columns.find((candidate) => candidate.columnId === card.columnId);
  const milestone = milestones?.find((candidate) => candidate.milestoneId === card.milestoneId);
  const boardKey = queryKeys.eventBoard(eventId);

  return (
    <>
      <PageHead
        eyebrow={
          <>
            <BackLink to={boardUrl}>Task board</BackLink>
            {column && ` · ${column.name}`}
          </>
        }
        title={card.subject}
        actions={
          (canEdit || canMove) && (
            <>
              {canMove && <Button onClick={() => setMoving(true)}>Move to…</Button>}
              {canEdit && <Button onClick={() => setAssigning(true)}>Assign people</Button>}
              {canEdit && <Button onClick={() => setEditing(true)}>Edit card</Button>}
            </>
          )
        }
      />

      {event?.isConfidential && <ConfidentialBanner crew={!usesDeskLayout(check)} />}

      <DetailColumns
        main={
          <Panel title="Card">
            <FieldList>
              <FieldRow label="Description">
                {card.description ? (
                  <SanitisedDescription html={card.description} />
                ) : (
                  <span className={styles.muted}>No description</span>
                )}
              </FieldRow>
              <FieldRow label="Priority">
                {card.priority === 'High' || card.priority === 'Critical' ? (
                  <Badge tone={card.priority === 'High' ? 'warning' : 'danger'}>{card.priority}</Badge>
                ) : (
                  card.priority
                )}
              </FieldRow>
              <FieldRow label="Due">
                {card.dueAt ? (
                  <>
                    {formatDateTime(card.dueAt)}
                    {milestone && (
                      <span className={styles.muted}> · {dueAgainstMilestone(card.dueAt, milestone)}</span>
                    )}
                  </>
                ) : (
                  'No due date'
                )}
              </FieldRow>
              <FieldRow label="Milestone">
                {milestone
                  ? `${milestoneLabels[milestone.milestoneType]} · ${formatDateTime(milestone.scheduledStart)}`
                  : card.milestoneId
                    ? undefined
                    : 'None'}
              </FieldRow>
              {card.status === 'Done' && card.completedAt && (
                <FieldRow label="Done">{formatDate(card.completedAt)}</FieldRow>
              )}
            </FieldList>
          </Panel>
        }
        side={
          <Panel title="People">
            <FieldList>
              <FieldRow label="Assigned to">
                {formatList(
                  card.assignees.map((person) => (person.userId === me ? 'you' : person.fullName)),
                ) || 'Nobody yet'}
              </FieldRow>
              <FieldRow label="Added by">
                {card.createdBy.userId === me ? 'You' : card.createdBy.fullName}
              </FieldRow>
              <FieldRow label="Event">{event?.name}</FieldRow>
            </FieldList>
          </Panel>
        }
      />

      <EditCardDialog
        card={editing ? card : null}
        due="time"
        milestones={milestones}
        boardKey={boardKey}
        onClose={() => setEditing(false)}
      />
      {assigning && <AssignPeopleDialog card={card} eventId={eventId} onClose={() => setAssigning(false)} />}
      <MoveCardDialog
        card={moving ? card : null}
        columns={[...board.columns].sort((a, b) => a.position - b.position)}
        onClose={() => setMoving(false)}
        onMove={(moved, to) => {
          setMoving(false);
          move.mutate({ card: moved, to });
        }}
      />
    </>
  );
}

//the event's crew, the only people the api lets a card go to
function AssignPeopleDialog({
  card,
  eventId,
  onClose,
}: {
  card: TaskCard;
  eventId: string;
  onClose: () => void;
}) {
  const crew = useCrewPeople(eventId);

  return (
    <AssigneesDialog
      card={card}
      people={crew.people}
      failed={crew.failed}
      required={false}
      hint="Only people on the event's crew are listed. Add someone to the crew first to give them a card."
      boardKey={queryKeys.eventBoard(eventId)}
      onClose={onClose}
    />
  );
}
