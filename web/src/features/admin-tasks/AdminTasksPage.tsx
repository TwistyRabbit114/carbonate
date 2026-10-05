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
import { ClipboardCheck, EllipsisVertical, GripVertical, Plus } from 'lucide-react';
import type { Board, BoardColumn, TaskCard } from '@/api/types';
import { usePermissions, useSignedIn } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { columnCollision, columnKeyboardCoordinates } from '@/components/kanban/dragging';
import { KanbanBoard } from '@/components/kanban/KanbanBoard';
import { KanbanColumn, type DropState } from '@/components/kanban/KanbanColumn';
import { KanbanSkeleton } from '@/components/kanban/KanbanSkeleton';
import { useFollowFocus, type FocusPlan } from '@/components/kanban/useFollowFocus';
import { PageHead } from '@/components/PageHead';
import { cx } from '@/lib/cx';
import { AdminTaskCard, AdminTaskCardOverlay } from './AdminTaskCard';
import { useAdminBoard, useTaskStep } from './api';
import { AssignTaskDialog } from './AssignTaskDialog';
import { taskAnnouncements, taskDragInstructions } from './moves';
import { ReturnTaskDialog } from './ReturnTaskDialog';
import { actionsFor, actionTarget, stageOf, type AdminStage, type PlacedTask, type TaskAction } from './rules';
import { TaskActionsDialog } from './TaskActionsDialog';
import cardStyles from '@/components/kanban/KanbanCard.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//FR-19: the one board for work that isn't tied to an event
export default function AdminTasksPage() {
  const check = usePermissions();
  const board = useAdminBoard();
  const [assigning, setAssigning] = useState(false);
  const canAssign = check.can('admin_task.assign');

  return (
    <>
      <PageHead
        eyebrow="Not tied to any event"
        title="Admin Tasks"
        actions={
          canAssign &&
          board.data && (
            <Button variant="primary" onClick={() => setAssigning(true)}>
              <Plus aria-hidden="true" />
              Assign task
            </Button>
          )
        }
      />

      {board.data ? (
        <TasksBoard board={board.data} canAssign={canAssign} />
      ) : board.isError ? (
        <ErrorState message="We couldn't load the admin tasks." onRetry={() => void board.refetch()} />
      ) : (
        <KanbanSkeleton columns={3} />
      )}

      {board.data && (
        <AssignTaskDialog open={assigning} boardId={board.data.boardId} onClose={() => setAssigning(false)} />
      )}
    </>
  );
}

//----------------------------------------------------------\\
//                              BOARD
//----------------------------------------------------------\\

//drag a card's handle to another column, or use its "Move…" menu. a column only lights up as a
//target when this user has that step to take: the assignee hands in, the manager who handed the
//task out signs it off or returns it (FR-20)
type CardControl = 'handle' | 'menu';

const emptyText: Record<AdminStage, string> = {
  assigned: 'Nothing waiting to be started',
  review: 'Nothing waiting for review',
  complete: 'Nothing signed off yet',
};

//back on the same button once the card settles, or on the card itself if that button has gone
function focusPlanFor(card: TaskCard, control: CardControl): FocusPlan {
  const ofCard = `[data-card-id="${card.cardId}"]`;
  return { cardId: card.cardId, selector: `${ofCard}[data-control="${control}"]`, fallback: `li${ofCard}` };
}

function TasksBoard({ board, canAssign }: { board: Board; canAssign: boolean }) {
  const me = useSignedIn().user.userId;
  const canReview = usePermissions().can('admin_task.review');
  const step = useTaskStep();
  const followFocus = useFollowFocus(board);
  const [dragging, setDragging] = useState<PlacedTask | null>(null);
  const [menuFor, setMenuFor] = useState<PlacedTask | null>(null);
  const [returning, setReturning] = useState<{ card: TaskCard; control: CardControl } | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: columnKeyboardCoordinates }),
  );

  const tasks: PlacedTask[] = board.columns.flatMap((column) => {
    const stage = stageOf(column);
    return column.cards.map((card) => ({
      card,
      stage,
      columnName: column.name,
      actions: actionsFor(card, stage, me, canReview),
    }));
  });

  if (tasks.length === 0) {
    return (
      <EmptyState title="No admin tasks yet" icon={ClipboardCheck}>
        <p>
          {canAssign
            ? 'Use Assign task to hand one out. It starts here in Assigned.'
            : 'Tasks handed out by the Director or Operations Manager show up here.'}
        </p>
      </EmptyState>
    );
  }

  const findTask = (cardId: string) => tasks.find((task) => task.card.cardId === cardId);
  const columnName = (columnId: string) =>
    board.columns.find((column) => column.columnId === columnId)?.name ?? 'that column';
  const columnAt = (stage: AdminStage) => board.columns.find((column) => stageOf(column) === stage);

  //returning needs notes first, so it opens a dialog. the other two steps happen at once
  function act(task: PlacedTask, action: TaskAction, control: CardControl) {
    if (action === 'return') {
      setReturning({ card: task.card, control });
      return;
    }
    const to = columnAt(actionTarget[action]);
    if (!to) return;
    followFocus(focusPlanFor(task.card, control));
    step.mutate({ card: task.card, action, to });
  }

  function dropStateFor(column: BoardColumn): DropState | undefined {
    if (!dragging || column.columnId === dragging.card.columnId) return undefined;
    const stage = stageOf(column);
    return dragging.actions.some((action) => actionTarget[action] === stage) ? 'allowed' : 'blocked';
  }

  function handleDragEnd({ active, over }: DragEndEvent) {
    const task = findTask(String(active.id));
    const column = board.columns.find((candidate) => candidate.columnId === over?.id);
    setDragging(null);
    if (!task || !column) return;

    const action = task.actions.find((candidate) => actionTarget[candidate] === stageOf(column));
    if (action) act(task, action, 'handle');
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={columnCollision}
      accessibility={{
        announcements: taskAnnouncements(findTask, columnName),
        screenReaderInstructions: { draggable: taskDragInstructions },
      }}
      onDragStart={({ active }) => setDragging(findTask(String(active.id)) ?? null)}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setDragging(null)}
    >
      <KanbanBoard label="Admin tasks by stage">
        {board.columns.map((column) => (
          <DroppableColumn key={column.columnId} column={column} dropState={dropStateFor(column)}>
            {column.cards.map((card) => {
              const task = findTask(card.cardId)!;
              return task.actions.length > 0 ? (
                <DraggableTaskCard
                  key={card.cardId}
                  task={task}
                  me={me}
                  lifted={dragging?.card.cardId === card.cardId}
                  onOpenMenu={setMenuFor}
                />
              ) : (
                <AdminTaskCard key={card.cardId} card={card} stage={task.stage} me={me} />
              );
            })}
          </DroppableColumn>
        ))}
      </KanbanBoard>

      <DragOverlay dropAnimation={null}>
        {dragging && <AdminTaskCardOverlay card={dragging.card} stage={dragging.stage} me={me} />}
      </DragOverlay>

      <TaskActionsDialog
        task={menuFor}
        onClose={() => setMenuFor(null)}
        onAct={(task, action) => {
          setMenuFor(null);
          act(task, action, 'menu');
        }}
      />

      <ReturnTaskDialog
        card={returning?.card ?? null}
        onClose={() => {
          if (returning) followFocus(focusPlanFor(returning.card, returning.control));
          setReturning(null);
        }}
      />
    </DndContext>
  );
}

//----------------------------------------------------------\\
//                              PIECES
//----------------------------------------------------------\\

//the column a card started in stays a target, so dropping it back is a no-op rather than a miss
function DroppableColumn({
  column,
  dropState,
  children,
}: {
  column: BoardColumn;
  dropState?: DropState;
  children: ReactNode;
}) {
  const stage = stageOf(column);
  const disabled = dropState !== undefined && dropState !== 'allowed';
  const { setNodeRef, isOver } = useDroppable({ id: column.columnId, disabled });

  return (
    <KanbanColumn
      title={column.name}
      count={column.cards.length}
      emptyText={stage && emptyText[stage]}
      startsOpenOnPhone={stage !== 'complete'}
      columnRef={setNodeRef}
      dropState={isOver && dropState === 'allowed' ? 'over' : dropState}
    >
      {children}
    </KanbanColumn>
  );
}

function DraggableTaskCard({
  task,
  me,
  lifted,
  onOpenMenu,
}: {
  task: PlacedTask;
  me: string;
  lifted: boolean;
  onOpenMenu: (task: PlacedTask) => void;
}) {
  const { card } = task;
  const { attributes, listeners, setNodeRef, setActivatorNodeRef } = useDraggable({ id: card.cardId });

  return (
    <AdminTaskCard
      card={card}
      stage={task.stage}
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
            aria-label={`Move ${card.subject}…`}
            title="Move…"
            data-card-id={card.cardId}
            data-control="menu"
            onClick={() => onOpenMenu(task)}
          >
            <EllipsisVertical aria-hidden="true" />
          </button>
        </>
      }
    />
  );
}
