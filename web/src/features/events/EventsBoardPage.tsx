import { useEffect, useState, type ReactNode } from 'react';
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
import { useQueryClient } from '@tanstack/react-query';
import { CalendarCheck, EllipsisVertical, GripVertical, Plus, SearchX } from 'lucide-react';
import { useSearchParams } from 'react-router';
import type { EventListItem, EventStatus } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { KanbanBoard } from '@/components/kanban/KanbanBoard';
import { KanbanColumn, type DropState } from '@/components/kanban/KanbanColumn';
import { KanbanSkeleton } from '@/components/kanban/KanbanSkeleton';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { cx } from '@/lib/cx';
import { allowedTransitionsQuery, useAllowedTransitions, useEventsBoard, useMoveEvent } from './api';
import { BoardFilterBar } from './BoardFilterBar';
import { boardColumns, groupByStage, type BoardStatus } from './board';
import { EventCard, EventCardOverlay } from './EventCard';
import { applyBoardFilters, readBoardFilters, writeBoardFilters, type BoardFilters } from './filters';
import { columnCollision, columnKeyboardCoordinates, dragInstructions, moveAnnouncements } from './moves';
import { MoveEventDialog } from './MoveEventDialog';
import cardStyles from './EventCard.module.scss';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//FR-18: confirmed work only, in the three active stages
export default function EventsBoardPage() {
  const check = usePermissions();
  const board = useEventsBoard();

  return (
    <>
      <PageHead
        eyebrow="Events"
        title="Events Board"
        actions={
          check.can('event.create') && (
            <LinkButton to="/events/new" variant="primary">
              <Plus aria-hidden="true" />
              New event
            </LinkButton>
          )
        }
      />

      {board.data ? (
        <EventsBoard events={board.data} canMove={check.can('event.transition')} />
      ) : board.isError ? (
        <ErrorState message="We couldn't load the events board." onRetry={() => void board.refetch()} />
      ) : (
        <KanbanSkeleton columns={boardColumns.length} />
      )}
    </>
  );
}

//----------------------------------------------------------\\
//                              BOARD
//----------------------------------------------------------\\

type Stages = ReturnType<typeof groupByStage>;

const countOnBoard = (stages: Stages) =>
  boardColumns.reduce((total, column) => total + stages[column.status].length, 0);

function EventsBoard({ events, canMove }: { events: EventListItem[]; canMove: boolean }) {
  const [params, setParams] = useSearchParams();
  const filters = readBoardFilters(params);
  const total = countOnBoard(groupByStage(events));

  //says why the board is empty, so nobody goes looking for an enquiry that isn't meant to be here
  if (total === 0) {
    return (
      <EmptyState title="No confirmed events yet" icon={CalendarCheck}>
        <p>
          Events show up here once their PO or deposit is recorded. New enquiries stay off the board until
          then.
        </p>
      </EmptyState>
    );
  }

  const visible = applyBoardFilters(events, filters);
  const stages = groupByStage(visible);
  const shown = countOnBoard(stages);

  //replace rather than push, so the back button leaves the board instead of undoing each filter
  const changeFilters = (next: BoardFilters) => setParams(writeBoardFilters(next), { replace: true });

  return (
    <>
      <BoardFilterBar filters={filters} shown={shown} total={total} onChange={changeFilters} />
      {shown === 0 ? (
        <EmptyState title="No events match these filters" icon={SearchX}>
          <p>Try another division or event type, or widen the dates.</p>
        </EmptyState>
      ) : canMove ? (
        <MovableBoard events={visible} stages={stages} />
      ) : (
        <StaticBoard stages={stages} />
      )}
    </>
  );
}

//accounts and anyone else without event.transition get the board with no handles or move menus
function StaticBoard({ stages }: { stages: Stages }) {
  return (
    <KanbanBoard label="Events by stage">
      {boardColumns.map((column) => (
        <StageColumn key={column.status} status={column.status} stages={stages}>
          {stages[column.status].map((event) => (
            <EventCard key={event.eventId} event={event} />
          ))}
        </StageColumn>
      ))}
    </KanbanBoard>
  );
}

function StageColumn({
  status,
  stages,
  columnRef,
  dropState,
  children,
}: {
  status: BoardStatus;
  stages: Stages;
  columnRef?: (element: HTMLElement | null) => void;
  dropState?: DropState;
  children: ReactNode;
}) {
  const column = boardColumns.find((candidate) => candidate.status === status)!;

  return (
    <KanbanColumn
      title={column.label}
      count={stages[status].length}
      startsOpenOnPhone={status === 'InProgress'}
      emptyText="No events"
      columnRef={columnRef}
      dropState={dropState}
      badge={
        column.automatic && (
          <Badge tone="auto">
            Auto<span className="visually-hidden">, events move here on their own</span>
          </Badge>
        )
      }
    >
      {children}
    </KanbanColumn>
  );
}

//----------------------------------------------------------\\
//                              MOVING
//----------------------------------------------------------\\

//drag a card's handle to another column, or use its "Move to…" menu. both end in the same
//optimistic move, and only columns the server allows light up as targets
type CardControl = 'handle' | 'menu';

//where keyboard focus should land once a moved card has settled
type FocusPlan = { eventId: string; selector: string };

function MovableBoard({ events, stages }: { events: EventListItem[]; stages: Stages }) {
  const queryClient = useQueryClient();
  const move = useMoveEvent();
  const [dragging, setDragging] = useState<EventListItem | null>(null);
  const [menuFor, setMenuFor] = useState<EventListItem | null>(null);
  const [focusPlan, setFocusPlan] = useState<FocusPlan | null>(null);
  const allowed = useAllowedTransitions(dragging?.eventId ?? null);

  //a moved card is drawn fresh in its new column, which drops keyboard focus on the page body.
  //put it back on the same button of that card, and again if a rejected move sends it home.
  //it only ever picks focus up off the body, never takes it from something the user chose
  useEffect(() => {
    if (!focusPlan) return;
    const target = document.querySelector<HTMLElement>(focusPlan.selector);
    const focusLost = !document.activeElement || document.activeElement === document.body;
    if (target && focusLost) target.focus();
  }, [focusPlan, events]);

  //stop following the card once the user clicks or tabs somewhere else
  useEffect(() => {
    if (!focusPlan) return;
    const stop = (event: Event) => {
      const element = event.target instanceof HTMLElement ? event.target : null;
      const ours = element?.dataset.eventId === focusPlan.eventId || element?.matches(focusPlan.selector);
      if (event.type === 'pointerdown' || !ours) setFocusPlan(null);
    };
    document.addEventListener('focusin', stop);
    document.addEventListener('pointerdown', stop);
    return () => {
      document.removeEventListener('focusin', stop);
      document.removeEventListener('pointerdown', stop);
    };
  }, [focusPlan]);

  function moveEvent(event: EventListItem, to: EventStatus, control: CardControl) {
    //a cancelled card leaves the board for good, so focus goes to the start of the page content
    const selector =
      to === 'Cancelled' ? '#main' : `[data-event-id="${event.eventId}"][data-control="${control}"]`;
    setFocusPlan({ eventId: event.eventId, selector });
    move.mutate({ event, to });
  }

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: columnKeyboardCoordinates }),
  );

  const findEvent = (id: string) => events.find((event) => event.eventId === id);

  //fetch what's allowed as soon as someone reaches for a card, so targets light up on pick-up
  const prefetch = (eventId: string) => void queryClient.prefetchQuery(allowedTransitionsQuery(eventId));

  function dropStateFor(status: EventStatus): DropState | undefined {
    if (!dragging || status === dragging.status) return undefined;
    if (!allowed.data) return 'checking';
    return allowed.data.includes(status) ? 'allowed' : 'blocked';
  }

  function handleDragEnd({ active, over }: DragEndEvent) {
    const event = findEvent(String(active.id));
    const to = over ? (String(over.id) as EventStatus) : null;
    setDragging(null);
    if (event && to && to !== event.status && allowed.data?.includes(to)) moveEvent(event, to, 'handle');
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={columnCollision}
      accessibility={{
        announcements: moveAnnouncements(findEvent),
        screenReaderInstructions: { draggable: dragInstructions },
      }}
      onDragStart={({ active }) => setDragging(findEvent(String(active.id)) ?? null)}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setDragging(null)}
    >
      <KanbanBoard label="Events by stage">
        {boardColumns.map((column) => (
          <DroppableStage
            key={column.status}
            status={column.status}
            stages={stages}
            dropState={dropStateFor(column.status)}
          >
            {stages[column.status].map((event) => (
              <DraggableEventCard
                key={event.eventId}
                event={event}
                lifted={dragging?.eventId === event.eventId}
                onReach={prefetch}
                onOpenMenu={setMenuFor}
              />
            ))}
          </DroppableStage>
        ))}
      </KanbanBoard>

      <DragOverlay dropAnimation={null}>{dragging && <EventCardOverlay event={dragging} />}</DragOverlay>

      <MoveEventDialog
        event={menuFor}
        onClose={() => setMenuFor(null)}
        onMove={(event, to) => {
          setMenuFor(null);
          moveEvent(event, to, 'menu');
        }}
      />
    </DndContext>
  );
}

//the column a card started in stays a target, so dropping it back is a no-op rather than a miss
function DroppableStage({
  status,
  stages,
  dropState,
  children,
}: {
  status: BoardStatus;
  stages: Stages;
  dropState?: DropState;
  children: ReactNode;
}) {
  const disabled = dropState !== undefined && dropState !== 'allowed';
  const { setNodeRef, isOver } = useDroppable({ id: status, disabled });

  return (
    <StageColumn
      status={status}
      stages={stages}
      columnRef={setNodeRef}
      dropState={isOver && dropState === 'allowed' ? 'over' : dropState}
    >
      {children}
    </StageColumn>
  );
}

function DraggableEventCard({
  event,
  lifted,
  onReach,
  onOpenMenu,
}: {
  event: EventListItem;
  lifted: boolean;
  onReach: (eventId: string) => void;
  onOpenMenu: (event: EventListItem) => void;
}) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef } = useDraggable({ id: event.eventId });
  const reach = () => onReach(event.eventId);

  return (
    <EventCard
      event={event}
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
            aria-label={`Drag ${event.name} to another stage`}
            data-event-id={event.eventId}
            data-control="handle"
            onPointerEnter={reach}
            onFocus={reach}
          >
            <GripVertical aria-hidden="true" />
          </button>
          <button
            type="button"
            className={cardStyles.action}
            aria-label={`Move ${event.name} to…`}
            data-event-id={event.eventId}
            data-control="menu"
            title="Move to…"
            onClick={() => onOpenMenu(event)}
            onPointerEnter={reach}
            onFocus={reach}
          >
            <EllipsisVertical aria-hidden="true" />
          </button>
        </>
      }
    />
  );
}
