import { useParams } from 'react-router';
import { isApiError } from '@/api/problem';
import { BackLink } from '@/components/BackLink';
import { PageHead } from '@/components/PageHead';
import { PageSkeleton } from '@/components/PageSkeleton';
import { useEvent } from '@/features/events/api';
import { MissingEvent } from '@/features/events/MissingEvent';
import { IncidentForm } from './IncidentForm';

//reporting from an event's own page at the desk, the same form crew use (FR-31)
export default function ReportIncidentPage() {
  const { eventId = '' } = useParams();
  const event = useEvent(eventId);
  const eventUrl = `/events/${eventId}`;

  if (event.isError && isApiError(event.error) && event.error.status === 404) return <MissingEvent />;
  if (event.isPending) return <PageSkeleton />;

  return (
    <>
      <PageHead
        title="Report an incident"
        eyebrow={<BackLink to={eventUrl}>{event.data?.name ?? 'Event'}</BackLink>}
      />
      <IncidentForm eventId={eventId} doneTo={eventUrl} />
    </>
  );
}
