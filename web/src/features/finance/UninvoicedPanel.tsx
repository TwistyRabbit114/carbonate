import { useState } from 'react';
import type { UninvoicedEvent } from '@/api/types';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { Panel } from '@/components/Panel';
import { Table } from '@/components/Table';
import { formatDateOnly } from '@/lib/format';
import { useUninvoicedEvents } from './api';
import { RaiseInvoiceDialog } from './RaiseInvoiceDialog';
import styles from './FinancePage.module.scss';

type Raisable = UninvoicedEvent & { confirmationId: string };

const canRaise = (event: UninvoicedEvent): event is Raisable => Boolean(event.confirmationId);

//a month after the event is late to still be invoicing it
const overdueAfterDays = 30;

const ago = (days: number) => (days === 0 ? 'today' : days === 1 ? 'yesterday' : `${days} days ago`);

//delivered but not invoiced, oldest first (FR-16), for whoever holds invoice.manage. an event with
//no po or deposit on record can't be invoiced until the confirmation is recorded
export function UninvoicedPanel() {
  const events = useUninvoicedEvents();
  const [raising, setRaising] = useState<Raisable | null>(null);

  return (
    <Panel title="Delivered, not invoiced">
      {events.isError ? (
        <ErrorState
          message="We couldn't load the events waiting on an invoice."
          onRetry={() => void events.refetch()}
        />
      ) : events.isPending ? (
        <p role="status">Loading the events waiting on an invoice…</p>
      ) : events.data.length === 0 ? (
        <p>Every finished event has an invoice.</p>
      ) : (
        <Table label="Finished events with no invoice">
          <thead>
            <tr>
              <th scope="col">Event</th>
              <th scope="col">Client</th>
              <th scope="col">Held</th>
              <th scope="col">Against</th>
              <th scope="col">
                <span className={styles.hidden}>Raise</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {events.data.map((event) => (
              <tr key={event.eventId}>
                <th scope="row">
                  {event.name}
                  <span className={styles.sub}>{event.eventCode}</span>
                </th>
                <td>{event.clientName}</td>
                <td className={styles.nowrap}>
                  {formatDateOnly(event.eventDate)}
                  <span className={styles.sub}>{ago(event.daysSinceEvent)}</span>
                  {event.daysSinceEvent > overdueAfterDays && <Badge tone="warning">Over 30 days</Badge>}
                </td>
                <td>{event.confirmationReference ?? 'No PO or deposit recorded'}</td>
                <td>
                  {canRaise(event) && (
                    <Button
                      variant="ghost"
                      onClick={() => setRaising(event)}
                      aria-label={`Raise an invoice for ${event.eventCode}`}
                    >
                      Raise invoice
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {raising && <RaiseInvoiceDialog event={raising} onClose={() => setRaising(null)} />}
    </Panel>
  );
}
