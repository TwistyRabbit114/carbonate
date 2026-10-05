import { useState } from 'react';
import { Link } from 'react-router';
import type { EventListItem } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { LinkButton } from '@/components/LinkButton';
import { Panel } from '@/components/Panel';
import { NumberCell, Table } from '@/components/Table';
import { useMyEvents } from '@/features/events/api';
import { formatDateOnly, formatMoney } from '@/lib/format';
import { currentQuote, useLatestCostings } from './api';
import { quoteStatusLabels } from './labels';
import { StartCostingDialog } from './StartCostingDialog';
import styles from './FinancePage.module.scss';

const day = 24 * 3_600_000;

//events still to come and those finished in the last two months. records are kept forever, so
//older ones are reached from the event itself
function recentEvents(events: readonly EventListItem[]) {
  const since = Date.now() - 60 * day;
  return events
    .filter((event) => event.status !== 'Cancelled' && new Date(event.endsAt).getTime() >= since)
    .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
}

//each event's current costing and where it's got to (FR-11, FR-14). enquiries are here too, they
//get costed before they're confirmed
export function CostingsPanel() {
  const events = useMyEvents();
  const shown = recentEvents(events.data ?? []);
  const costings = useLatestCostings(shown);
  const canEdit = usePermissions().can('quote.edit');
  const [starting, setStarting] = useState<EventListItem | null>(null);

  const quoteFor = (index: number) => currentQuote(costings[index]?.data);
  //$price: when the api leaves it off, the column isn't there (NFR-17)
  const showsPrice = shown.some((_event, index) => quoteFor(index)?.subtotalExVat != null);

  return (
    <Panel title="Costings">
      {events.isError ? (
        <ErrorState message="We couldn't load the events." onRetry={() => void events.refetch()} />
      ) : events.isPending ? (
        <p role="status">Loading the costings…</p>
      ) : shown.length === 0 ? (
        <p>No events coming up. Costings start from an event.</p>
      ) : (
        <Table label="Costing for each event">
          <thead>
            <tr>
              <th scope="col">Event</th>
              <th scope="col">Date</th>
              <NumberCell head>Version</NumberCell>
              {showsPrice && <NumberCell head>Price excl. VAT</NumberCell>}
              <th scope="col">Status</th>
              <th scope="col">
                <span className={styles.hidden}>Open</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {shown.map((event, index) => {
              const costing = costings[index];
              const quote = quoteFor(index);
              return (
                <tr key={event.eventId}>
                  <th scope="row">
                    <Link to={`/events/${event.eventId}`}>{event.name}</Link>
                    <span className={styles.sub}>{event.eventCode}</span>
                  </th>
                  <td className={styles.nowrap}>{formatDateOnly(event.eventDate)}</td>
                  <NumberCell>{quote && `v${quote.version}`}</NumberCell>
                  {showsPrice && (
                    <NumberCell>
                      {quote?.subtotalExVat != null && formatMoney(quote.subtotalExVat)}
                    </NumberCell>
                  )}
                  <td>
                    {costing?.isError
                      ? "Couldn't load"
                      : costing?.isPending
                        ? 'Loading…'
                        : quote
                          ? quoteStatusLabels[quote.status]
                          : 'Not costed yet'}
                    {quote?.status === 'PendingApproval' && quote.requiresApproval && (
                      <span className={styles.warning}>Above the approval limit</span>
                    )}
                  </td>
                  <td>
                    {quote ? (
                      <LinkButton
                        variant="ghost"
                        to={`/quotes/${quote.quoteId}`}
                        aria-label={`Open the ${event.name} costing`}
                      >
                        Open
                      </LinkButton>
                    ) : (
                      canEdit &&
                      costing?.isSuccess && (
                        <Button
                          variant="ghost"
                          onClick={() => setStarting(event)}
                          aria-label={`Start a costing for ${event.name}`}
                        >
                          Start costing
                        </Button>
                      )
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </Table>
      )}

      {starting && <StartCostingDialog event={starting} onClose={() => setStarting(null)} />}
    </Panel>
  );
}
