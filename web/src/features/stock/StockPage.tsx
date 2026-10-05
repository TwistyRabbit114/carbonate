import { useSearchParams } from 'react-router';
import { usePermissions } from '@/auth/AuthContext';
import { ErrorState } from '@/components/ErrorState';
import { LinkButton } from '@/components/LinkButton';
import { PageHead } from '@/components/PageHead';
import { Panel } from '@/components/Panel';
import { useMyEvents } from '@/features/events/api';
import { OrderListsPanel } from './OrderListsPanel';
import { RequirementsPanel } from './RequirementsPanel';

//stock planned for each confirmed event and the order lists made from it (FR-26 to FR-30).
//planning takes stock.plan, the order lists stock.view
export default function StockPage() {
  const check = usePermissions();

  return (
    <>
      <PageHead
        title="Stock & Orders"
        eyebrow="Confirmed events, this period"
        actions={<LinkButton to="/stock/catalogue">Catalogue</LinkButton>}
      />
      {check.can('stock.plan') && <PlanningPanel />}
      <OrderListsPanel />
    </>
  );
}

//the events still ahead or on now, soonest first. ?event= picks one, the event page links that way
function PlanningPanel() {
  const events = useMyEvents();
  const [params, setParams] = useSearchParams();

  if (events.isError) {
    return (
      <Panel title="Stock requirements">
        <ErrorState message="We couldn't load the events." onRetry={() => void events.refetch()} />
      </Panel>
    );
  }
  if (events.isPending) {
    return (
      <Panel title="Stock requirements">
        <p role="status">Loading the events…</p>
      </Panel>
    );
  }

  const planned = events.data
    .filter((event) => event.status === 'ConfirmedInPlanning' || event.status === 'InProgress')
    .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
  if (planned.length === 0) {
    return (
      <Panel title="Stock requirements">
        <p>No confirmed events to plan stock for. Events show here once they're confirmed.</p>
      </Panel>
    );
  }

  const asked = params.get('event');
  const eventId = planned.some((event) => event.eventId === asked) ? asked! : planned[0]!.eventId;
  return (
    <RequirementsPanel
      events={planned}
      eventId={eventId}
      onPickEvent={(picked) => setParams({ event: picked }, { replace: true })}
    />
  );
}
