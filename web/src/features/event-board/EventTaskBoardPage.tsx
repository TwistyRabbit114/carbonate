import { useState, type ReactNode } from 'react';
import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import { ClipboardList, EllipsisVertical, GripVertical, Plus, SearchX } from 'lucide-react';
import { useParams } from 'react-router';
import { isApiError } from '@/api/problem';
import type { Board, BoardColumn, Milestone, TaskCard } from '@/api/types';
import { usesDeskLayout } from '@/app/navigation';
import { usePermissions, useSignedIn } from '@/auth/AuthContext';
import { BackLink } from '@/components/BackLink';
import { Button } from '@/components/Button';
import { ConfidentialBanner } from '@/components/ConfidentialBanner';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { columnCollision, columnKeyboardCoordinates } from '@/components/kanban/dragging';
import { KanbanBoard } from '@/components/kanban/KanbanBoard';
import { KanbanColumn, type DropState } from '@/components/kanban/KanbanColumn';
import { KanbanSkeleton } from '@/components/kanban/KanbanSkeleton';
import { useFollowFocus, type FocusPlan } from '@/components/kanban/useFollowFocus';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { taskAnnouncements, taskDragInstructions, type CardInColumn } from '@/features/boards/announcements';
import { cx } from '@/lib/cx';
import { AddCardDialog } from './AddCardDialog';
import { useEventBoard, useEventSummary, useMilestones, useMoveCard } from './api';
import { EventTaskCard, EventTaskCardOverlay } from './EventTaskCard';
import { MoveCardDialog } from './MoveCardDialog';
import cardStyles from '@/components/kanban/KanbanCard.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//an event's own checklist, set up from its template when the event was created (FR-25). crew
//only get the cards assigned to them (FR-21), and column limits are shown, never enforced (FR-23)
export default function EventTaskBoardPage() {
  const { eventId = '' } = useParams();
  const check = usePermissions();
  const event = useEventSummary(eventId);
  const board = useEventBoard(eventId);
  const milestones = useMilestones(eventId);
  const [adding, setAdding] = useState(false);

  //crew hold task.edit for their own cards only, so adding cards is for the desk roles
  const canAdd = check.can('task.edit') && usesDeskLayout(check);
  const boardMissing = board.isError && isApiError(board.error) && board.error.status === 404;
  const eventMissing = event.isError && isApiError(event.error) && event.error.status === 404;

  return (
    <>
      <PageHead
        eyebrow={<BackLink to={`/events/${eventId}`}>{event.data?.name ?? 'Event'}</BackLink>}
        title="Task board"
        actions={
          canAdd &&
          board.data && (
            <Button variant="primary" onClick={() => setAdding(true)}>
              <Plus aria-hidden="true" />
              Add card
            </Button>
          )
        }
      />

      {event.data?.isConfidential && <ConfidentialBanner crew={!usesDeskLayout(check)} />}

      {board.data ? (
        <TaskBoard board={board.data} eventId={eventId} milestones={milestones.data} canAdd={canAdd} />
      ) : boardMissing && eventMissing ? (
        <EmptyState title="We can't find that event" icon={SearchX}>
          <p>It may have been removed, or it isn't one of yours.</p>
        </EmptyState>
      ) : boardMissing ? (
        <EmptyState title="This event has no task board" icon={ClipboardList}>
          <p>
            Boards are set up from a template when an event is created, and there wasn't one for this kind of
            event.
          </p>
          <LinkButton to={`/events/${eventId}`}>Back to the event</LinkButton>
        </EmptyState>
      ) : board.isError ? (
        <ErrorState message="We couldn't load the task board." onRetry={() => void board.refetch()} />
      ) : (
        <KanbanSkeleton columns={3} />
      )}

      {board.data && (
        <AddCardDialog
          open={adding}
          eventId={eventId}
          board={board.data}
          milestones={milestones.data}
          onClose={() => setAdding(false)}
        />
      )}
    </>
  );
}

//----------------------------------------------------------\\
//                              BOARD
//----------------------------------------------------------\\

//drag a card's handle to another column, or use its "Move to…" menu. any column is fair game on
//an event board, the done column just marks the card done
type CardControl = 'handle' | 'menu';

//back on the same button once the card settles in its new column
function focusPlanFor(card: TaskCard, control: CardControl): FocusPlan {
  const ofCard = `[data-card-id="${card.cardId}"]`;
  return { cardId: card.cardId, selector: `${ofCard}[data-control="${control}"]`, fallback: `li${ofCard}` };
}

type TaskBoardProps = {
  board: Board;
  eventId: string;
  milestones: readonly Milestone[] | undefined;
  canAdd: boolean;
};

function TaskBoard({ board, eventId, milestones, canAdd }: TaskBoardProps) {
  const me = useSignedIn().user.userId;
  const canMove = usePermissions().can('task.move');
  const move = useMoveCard(eventId);
  const followFocus = useFollowFocus(board);
  const [dragging, setDragging] = useState<TaskCard | null>(null);
  const [menuFor, setMenuFor] = useState<TaskCard | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: columnKeyboardCoordinates }),
  );

  const columns = [...board.columns].sort((a, b) => a.position - b.position);
  const cards: CardInColumn[] = columns.flatMap((column) =>
    column.cards.map((card) => ({ card, columnName: column.name })),
  );

  if (cards.length === 0) {
    return (
      <EmptyState
        title={canAdd ? 'No cards on this board yet' : 'Nothing on this board for you yet'}
        icon={ClipboardList}
      >
        <p>
          {canAdd
            ? 'Use Add card to put the first one on.'
            : 'Cards show up here once someone assigns them to you.'}
        </p>
      </EmptyState>
    );
  }

  const findCard = (cardId: string) => cards.find((entry) => entry.card.cardId === cardId);
  const columnName = (columnId: string) =>
    columns.find((column) => column.columnId === columnId)?.name ?? 'that column';
  const milestoneOf = (card: TaskCard) =>
    milestones?.find((milestone) => milestone.milestoneId === card.milestoneId);

  function moveTo(card: TaskCard, to: BoardColumn, control: CardControl) {
    if (to.columnId === card.columnId) return;
    followFocus(focusPlanFor(card, control));
    move.mutate({ card, to });
  }

  function handleDragEnd({ active, over }: DragEndEvent) {
    const entry = findCard(String(active.id));
    const to = columns.find((column) => column.columnId === over?.id);
    setDragging(null);
    if (entry && to) moveTo(entry.card, to, 'handle');
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={columnCollision}
      accessibility={{
        announcements: taskAnnouncements(findCard, columnName),
        screenReaderInstructions: { draggable: taskDragInstructions },
      }}
      onDragStart={({ active }) => setDragging(findCard(String(active.id))?.card ?? null)}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setDragging(null)}
    >
      <KanbanBoard label="Event tasks by column">
        {columns.map((column, index) => (
          <DroppableColumn
            key={column.columnId}
            column={column}
            dropState={dragging && dragging.columnId !== column.columnId ? 'allowed' : undefined}
            startsOpenOnPhone={!column.isDoneColumn || index === 0}
          >
            {column.cards.map((card) =>
              canMove ? (
                <DraggableCard
                  key={card.cardId}
                  card={card}
                  eventId={eventId}
                  milestone={milestoneOf(card)}
                  me={me}
                  lifted={dragging?.cardId === card.cardId}
                  onOpenMenu={setMenuFor}
                />
              ) : (
                <EventTaskCard
                  key={card.cardId}
                  card={card}
                  eventId={eventId}
                  milestone={milestoneOf(card)}
                  me={me}
                />
              ),
            )}
          </DroppableColumn>
        ))}
      </KanbanBoard>

      <DragOverlay dropAnimation={null}>
        {dragging && <EventTaskCardOverlay card={dragging} milestone={milestoneOf(dragging)} me={me} />}
      </DragOverlay>

      <MoveCardDialog
        card={menuFor}
        columns={columns}
        onClose={() => setMenuFor(null)}
        onMove={(card, to) => {
          setMenuFor(null);
          moveTo(card, to, 'menu');
        }}
      />
    </DndContext>
  );
}

//----------------------------------------------------------\\
//                              PIECES
//----------------------------------------------------------\\

type DroppableColumnProps = {
  column: BoardColumn;
  dropState?: DropState;
  startsOpenOnPhone: boolean;
  children: ReactNode;
};

//the column a card started in stays a target, so dropping it back is a no-op rather than a miss
function DroppableColumn({ column, dropState, startsOpenOnPhone, children }: DroppableColumnProps) {
  const { setNodeRef, isOver } = useDroppable({ id: column.columnId });

  return (
    <KanbanColumn
      title={column.name}
      count={column.cards.length}
      limit={column.wipLimit}
      emptyText={column.isDoneColumn ? 'Nothing done yet' : 'Nothing here'}
      startsOpenOnPhone={startsOpenOnPhone}
      columnRef={setNodeRef}
      dropState={isOver && dropState === 'allowed' ? 'over' : dropState}
    >
      {children}
    </KanbanColumn>
  );
}

type DraggableCardProps = {
  card: TaskCard;
  eventId: string;
  milestone?: Milestone;
  me: string;
  lifted: boolean;
  onOpenMenu: (card: TaskCard) => void;
};

function DraggableCard({ card, eventId, milestone, me, lifted, onOpenMenu }: DraggableCardProps) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef } = useDraggable({ id: card.cardId });

  return (
    <EventTaskCard
      card={card}
      eventId={eventId}
      milestone={milestone}
      me={me}
      cardRef={setNodeRef}
      dragging={lifted}
      actions={
        <>
          <button
            type="button"
            ref={setActivatorNodeRef}
            className={cx(cardStyles.action, cardStyles.handle)}
            {...attributes}
            {...listeners}
            aria-label={`Drag ${card.subject} to another column`}
            data-card-id={card.cardId}
            data-control="handle"
          >
            <GripVertical aria-hidden="true" />
          </button>
          <button
            type="button"
            className={cardStyles.action}
            aria-label={`Move ${card.subject} to…`}
            title="Move to…"
            data-card-id={card.cardId}
            data-control="menu"
            onClick={() => onOpenMenu(card)}
          >
            <EllipsisVertical aria-hidden="true" />
          </button>
        </>
      }
    />
  );
}
