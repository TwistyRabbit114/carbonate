import { Plus } from 'lucide-react';
import type { Incident } from '@/api/types';
import { ErrorState } from '@/components/ErrorState';
import { Item, ItemList } from '@/components/ItemList';
import { LinkButton } from '@/components/LinkButton';
import { Panel } from '@/components/Panel';
import { formatDateTime, formatMoney } from '@/lib/format';
import { useEventIncidents } from './api';
import { incidentTypeLabels } from './labels';

type EventIncidentsPanelProps = {
  eventId: string;
  canView: boolean;
  canReport: boolean;
};

//breakages, failures and shortfalls logged against the event (FR-31). the replacement cost is
//$cost and only shows when the api sent it
export function EventIncidentsPanel({ eventId, canView, canReport }: EventIncidentsPanelProps) {
  const incidents = useEventIncidents(eventId, canView);
  if (!canView) return null;

  return (
    <Panel
      title="Incidents"
      actions={
        canReport && (
          <LinkButton to={`/events/${eventId}/incidents/new`}>
            <Plus aria-hidden="true" />
            Report incident
          </LinkButton>
        )
      }
    >
      {incidents.isError ? (
        <ErrorState message="We couldn't load the incidents." onRetry={() => void incidents.refetch()} />
      ) : incidents.isPending ? (
        <p role="status">Loading incidents…</p>
      ) : incidents.data.length === 0 ? (
        <p>Nothing reported.</p>
      ) : (
        <ItemList label="Incidents reported">
          {incidents.data.map((incident) => (
            <IncidentItem key={incident.incidentId} incident={incident} />
          ))}
        </ItemList>
      )}
    </Panel>
  );
}

function IncidentItem({ incident }: { incident: Incident }) {
  const details = [
    incident.description,
    incident.resolutionNotes ? `Resolved: ${incident.resolutionNotes}` : null,
    incident.replacementCost != null ? `Replacement cost ${formatMoney(incident.replacementCost)}` : null,
  ].filter(Boolean);

  return (
    <Item
      title={`${incidentTypeLabels[incident.incidentType]}: ${incident.subjectName}${incident.quantity != null ? `, ${incident.quantity}` : ''}`}
      meta={`${incident.reportedByName} · ${formatDateTime(incident.reportedAt)}`}
      actions={
        incident.photoUrl && (
          <a href={incident.photoUrl} target="_blank" rel="noopener noreferrer">
            Photo
          </a>
        )
      }
    >
      {details.join('\n')}
    </Item>
  );
}
