import { useState } from 'react';
import type { SiteVisit, Venue } from '@/api/types';
import { usePermissions } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { FieldList, FieldRow } from '@/components/FieldList';
import { Item, ItemList } from '@/components/ItemList';
import { Panel } from '@/components/Panel';
import { formatDateOnly } from '@/lib/format';
import { useSiteVisits, useVenue } from './api';
import { mapLink, openingHours } from './places';
import { SiteVisitDialog } from './SiteVisitDialog';
import styles from './VenuePanel.module.scss';

//----------------------------------------------------------\\
//                              HELPERS
//----------------------------------------------------------\\

//only the requirements that apply, as one line
function requirementsOf(venue: Venue) {
  const needed = [
    venue.requiresSecurityClearance && 'Security clearance',
    venue.requiresHealthSafetyFile && 'Health and safety file',
  ].filter(Boolean);
  return needed.length > 0 ? needed.join(', ') : undefined;
}

//----------------------------------------------------------\\
//                              PANEL
//----------------------------------------------------------\\

type VenuePanelProps = {
  eventId: string;
  venueId: string | null | undefined;
};

//where the event is and how to get in (FR-32), with what the recce found (FR-33). everything
//here is plain text typed by people, so react escapes it and line breaks are kept.
//venue.edit adds and corrects recces
export function VenuePanel({ eventId, venueId }: VenuePanelProps) {
  const venue = useVenue(venueId);
  const visits = useSiteVisits(eventId);
  const canEdit = usePermissions().can('venue.edit');
  //undefined is closed, null is a new recce
  const [editing, setEditing] = useState<SiteVisit | null | undefined>(undefined);

  return (
    <Panel
      title="Venue and recce"
      actions={
        canEdit && (
          <Button variant="ghost" onClick={() => setEditing(null)}>
            Add recce
          </Button>
        )
      }
    >
      {!venueId ? (
        <p>No venue chosen yet.</p>
      ) : venue.isError ? (
        <ErrorState message="We couldn't load the venue." onRetry={() => void venue.refetch()} />
      ) : venue.isPending ? (
        <p role="status">Loading the venue…</p>
      ) : (
        <VenueDetails venue={venue.data} />
      )}

      <h3 className={styles.subhead}>Recce</h3>
      {visits.isError ? (
        <ErrorState message="We couldn't load the recce notes." onRetry={() => void visits.refetch()} />
      ) : visits.isPending ? (
        <p role="status">Loading the recce notes…</p>
      ) : visits.data.length === 0 ? (
        <p>No recce yet.</p>
      ) : (
        <ItemList label="Recces">
          {visits.data.map((visit) => (
            <VisitItem
              key={visit.siteVisitId}
              visit={visit}
              onEdit={canEdit ? () => setEditing(visit) : undefined}
            />
          ))}
        </ItemList>
      )}

      {editing !== undefined && (
        <SiteVisitDialog eventId={eventId} visit={editing} onClose={() => setEditing(undefined)} />
      )}
    </Panel>
  );
}

export function VenueDetails({ venue }: { venue: Venue }) {
  return (
    <FieldList>
      <FieldRow label="Venue">
        {venue.name}
        <br />
        <a href={mapLink(venue.address)} target="_blank" rel="noopener noreferrer" className={styles.mapLink}>
          {venue.address}
        </a>
      </FieldRow>
      <FieldRow label="Access route">
        {venue.accessRoute && <span className={styles.text}>{venue.accessRoute}</span>}
      </FieldRow>
      <FieldRow label="Loading bay">
        {venue.loadingBayDetails && <span className={styles.text}>{venue.loadingBayDetails}</span>}
      </FieldRow>
      <FieldRow label="Open">{openingHours(venue)}</FieldRow>
      <FieldRow label="Needs">{requirementsOf(venue)}</FieldRow>
      <FieldRow label="PPE">
        {venue.ppeRequirements && <span className={styles.text}>{venue.ppeRequirements}</span>}
      </FieldRow>
    </FieldList>
  );
}

function VisitItem({ visit, onEdit }: { visit: SiteVisit; onEdit?: () => void }) {
  const vehicle = [visit.vehicleType, visit.licencePlate].filter(Boolean).join(', ');

  return (
    <Item
      title={formatDateOnly(visit.visitDate)}
      meta={`By ${visit.conductedBy.fullName}`}
      actions={
        onEdit && (
          <Button
            variant="ghost"
            onClick={onEdit}
            aria-label={`Edit the recce of ${formatDateOnly(visit.visitDate)}`}
          >
            Edit
          </Button>
        )
      }
    >
      <FieldList>
        <FieldRow label="Vehicle">{vehicle || undefined}</FieldRow>
        <FieldRow label="Driver">{visit.driverName}</FieldRow>
        <FieldRow label="Driver details">{visit.requiredDriverDetails}</FieldRow>
        <FieldRow label="Crew">{visit.crewNames}</FieldRow>
        <FieldRow label="Sign-in">{visit.signInProcedure}</FieldRow>
        <FieldRow label="Security checkpoint">{visit.securityCheckpoint}</FieldRow>
        <FieldRow label="H&S file">{visit.healthSafetyFileRef}</FieldRow>
        <FieldRow label="Notes">{visit.notes}</FieldRow>
      </FieldList>
    </Item>
  );
}
