import { isApiError } from '@/api/problem';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { LinkButton } from '@/components/LinkButton';
import { Panel } from '@/components/Panel';
import { useEventBoard } from './api';

type TaskBoardPanelProps = {
  eventId: string;
  crew?: boolean; //crew only get their own cards, so the counts are theirs
};

//how far along the event's checklist is, column by column, and the way in to the board
export function TaskBoardPanel({ eventId, crew = false }: TaskBoardPanelProps) {
  const board = useEventBoard(eventId);
  const missing = board.isError && isApiError(board.error) && board.error.status === 404;
  const boardUrl = `/events/${eventId}/board`;

  if (missing) {
    return (
      <Panel title="Task board">
        <p>
          This event has no task board. Boards are set up from a template when an event is created, and there
          wasn't one for this kind of event.
        </p>
      </Panel>
    );
  }

  const columns = [...(board.data?.columns ?? [])].sort((a, b) => a.position - b.position);

  return (
    <Panel title={crew ? 'My tasks for this event' : 'Task board'}>
      {board.isError ? (
        <ErrorState message="We couldn't load the task board." onRetry={() => void board.refetch()} />
      ) : board.isPending ? (
        <p role="status">Loading the task board…</p>
      ) : (
        <>
          <FieldList>
            {columns.map((column) => (
              <FieldRow key={column.columnId} label={column.name}>
                {column.cards.length === 1 ? '1 card' : `${column.cards.length} cards`}
                {column.wipLimit != null && ` (limit ${column.wipLimit})`}
              </FieldRow>
            ))}
          </FieldList>
          <p>
            <LinkButton to={boardUrl}>Open board</LinkButton>
          </p>
        </>
      )}
    </Panel>
  );
}
