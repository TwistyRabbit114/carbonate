import { CalendarCheck, Plus } from 'lucide-react';
import type { EventListItem } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Badge } from '@/components/Badge';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { KanbanBoard } from '@/components/kanban/KanbanBoard';
import { KanbanColumn } from '@/components/kanban/KanbanColumn';
import { KanbanSkeleton } from '@/components/kanban/KanbanSkeleton';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { useEventsBoard } from './api';
import { boardColumns, groupByStage } from './board';
import { EventCard } from './EventCard';

//----------------------------------------------------------\\
//                              PAGE
//----------------------------------------------------------\\

//FR-18: confirmed work only, in the three active stages
export default function EventsBoardPage() {
  const check = usePermissions();
  const board = useEventsBoard();
  const canCreate = check.can('event.create');

  return (
    <>
      <PageHead
        eyebrow="Events"
        title="Events Board"
        actions={
          canCreate && (
            <LinkButton to="/events/new" variant="primary">
              <Plus aria-hidden="true" />
              New event
            </LinkButton>
          )
        }
      />

      {board.data ? (
        <EventsBoard events={board.data} />
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

function EventsBoard({ events }: { events: EventListItem[] }) {
  const stages = groupByStage(events);
  const onBoard = boardColumns.reduce((total, column) => total + stages[column.status].length, 0);

  //says why the board is empty, so nobody goes looking for an enquiry that isn't meant to be here
  if (onBoard === 0) {
    return (
      <EmptyState title="No confirmed events yet" icon={CalendarCheck}>
        <p>
          Events show up here once their PO or deposit is recorded. New enquiries stay off the board until
          then.
        </p>
      </EmptyState>
    );
  }

  return (
    <KanbanBoard label="Events by stage">
      {boardColumns.map((column) => (
        <KanbanColumn
          key={column.status}
          title={column.label}
          count={stages[column.status].length}
          startsOpenOnPhone={column.status === 'InProgress'}
          emptyText="No events"
          badge={
            column.automatic && (
              <Badge tone="auto">
                Auto<span className="visually-hidden">, events move here on their own</span>
              </Badge>
            )
          }
        >
          {stages[column.status].map((event) => (
            <EventCard key={event.eventId} event={event} />
          ))}
        </KanbanColumn>
      ))}
    </KanbanBoard>
  );
}
