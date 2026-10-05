import { useState } from 'react';
import { CalendarX2 } from 'lucide-react';
import { useSearchParams } from 'react-router';
import { EmptyState } from '@/components/EmptyState';
import { ErrorState } from '@/components/ErrorState';
import { Field } from '@/components/Field';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { useMyEvents } from '@/features/events/api';
import { IncidentForm } from '@/features/incidents/IncidentForm';
import { formatDateOnly, sastCalendarDate } from '@/lib/format';
import { crewSchedule, defaultReportEvent } from './schedule';
import styles from './ReportPage.module.scss';

//the crew report tab: pick one of my current events, today's already picked, then the form (FR-31)
export default function ReportPage() {
  const events = useMyEvents();
  const [params] = useSearchParams();
  const [chosen, setChosen] = useState<string | null>(params.get('event'));

  if (events.isError) {
    return (
      <>
        <PageHead title="Report a breakage or shortfall" />
        <ErrorState message="We couldn't load your events." onRetry={() => void events.refetch()} />
      </>
    );
  }
  if (!events.data) return <PageSkeleton />;

  const { next, upcoming } = crewSchedule(events.data);
  const open = [next, ...upcoming].filter((event) => event !== undefined);
  const fallback = defaultReportEvent(events.data, sastCalendarDate(new Date().toISOString()));
  const eventId = open.some((event) => event.eventId === chosen) ? chosen : (fallback?.eventId ?? null);

  return (
    <>
      <PageHead title="Report a breakage or shortfall" />

      {open.length === 0 || !eventId ? (
        <EmptyState title="Nothing to report against" icon={CalendarX2}>
          <p>You're not on any events right now. Reports are made against an event you're working.</p>
        </EmptyState>
      ) : (
        <>
          <div className={styles.event}>
            <Field label="Event" required>
              {(field) => (
                <select {...field} value={eventId} onChange={(change) => setChosen(change.target.value)}>
                  {open.map((event) => (
                    <option key={event.eventId} value={event.eventId}>
                      {event.name} · {formatDateOnly(event.eventDate)}
                    </option>
                  ))}
                </select>
              )}
            </Field>
          </div>
          <IncidentForm key={eventId} eventId={eventId} doneTo="/my/events" />
        </>
      )}
    </>
  );
}
